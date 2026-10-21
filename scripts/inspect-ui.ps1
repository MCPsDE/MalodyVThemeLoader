param([string]$BuildRoot = '', [string]$OutputRoot = '', [switch]$Baseline)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputRoot) { $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) 'MalodyVThemeLoader-ui' }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$fakeGame = Join-Path $repo 'artifacts\ui-fixture\MalodyV'
New-Item -ItemType Directory -Path (Join-Path $fakeGame 'ThemeLoader\themes'),(Join-Path $fakeGame 'Malody V_Data\il2cpp_data') -Force | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')) { [IO.File]::WriteAllBytes((Join-Path $fakeGame $name),[byte[]](1,2,3)) }
if (Test-Path (Join-Path $repo 'artifacts\payload\ThemeLoader\themes\Default')) {
    Copy-Item -LiteralPath (Join-Path $repo 'artifacts\payload\ThemeLoader\themes\Default') -Destination (Join-Path $fakeGame 'ThemeLoader\themes\Default') -Recurse -Force
} elseif (Test-Path (Join-Path $repo 'Default.mtp')) {
    Copy-Item -LiteralPath (Join-Path $repo 'Default.mtp') -Destination (Join-Path $fakeGame 'ThemeLoader\themes\Default.mtp') -Force
}
$assemblies = @{}
foreach ($name in @('MalodyVThemeLoader.ThemeManager','MalodyVThemeLoader.Installer','MalodyVBepInExInstaller')) {
    $dll = if ($BuildRoot) { Join-Path $BuildRoot "bin\$name\release_win-x64\$name.dll" } else { Join-Path $repo "src\$name\bin\Release\net8.0-windows\win-x64\$name.dll" }
    if ($BuildRoot -eq 'release') {
        $folder = switch($name) {
            'MalodyVThemeLoader.ThemeManager' { 'manager-build' }
            'MalodyVThemeLoader.Installer' { 'installer-build' }
            'MalodyVBepInExInstaller' { 'malody-bepinex-installer-build' }
        }
        $dll = Join-Path $repo "artifacts\$folder\bin\$name\release_win-x64\$name.dll"
    }
    if (Test-Path $dll) { $assemblies[$name] = [Reflection.Assembly]::LoadFrom($dll) }
}
function Get-Field($form, [string]$name) {
    $form.GetType().GetField($name, [Reflection.BindingFlags]'NonPublic,Instance').GetValue($form)
}
function Invoke-Method($form, [string]$name, [object[]]$values = @()) {
    $form.GetType().GetMethod($name, [Reflection.BindingFlags]'NonPublic,Instance').Invoke($form, $values) | Out-Null
}
function Capture($form, [string]$name) {
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 250
    # Control-level capture stays correct even when a fullscreen app covers the desktop.
    $bitmap = [Drawing.Bitmap]::new($form.Width, $form.Height)
    try {
        $form.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
        $bitmap.Save((Join-Path $OutputRoot "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Write-Output (Join-Path $OutputRoot "$name.png")
}
foreach ($name in $assemblies.Keys) {
    $isManager = $name.EndsWith('ThemeManager')
    $type = $assemblies[$name].GetType($name + $(if ($isManager) { '.ThemeManagerForm' } else { '.InstallerForm' }), $true)
    $arguments = if ($isManager) { [object[]]@($fakeGame) } else { [object[]]@() }
    $form = [Activator]::CreateInstance($type, [Reflection.BindingFlags]'Instance,Public,NonPublic', $null, $arguments, $null)
    try {
        $label = if ($isManager) {'manager'} elseif ($name -eq 'MalodyVBepInExInstaller') {'bepinex'} else {'installer'}
        if (!$isManager) { (Get-Field $form 'pathBox').Text = $fakeGame; Invoke-Method $form 'UpdatePathState' }
        Capture $form "$label-desktop"
        if (!$Baseline) {
            $form.Size = $form.MinimumSize
            Capture $form "$label-minimum"
            $form.ClientSize = [Drawing.Size]::new($(if ($isManager) {1120} else {880}), $(if ($isManager) {800} else {720}))
            if ($isManager) {
                Invoke-Method $form 'Navigate' @('settings')
                Capture $form 'manager-settings'
                Invoke-Method $form 'Navigate' @('live2d')
                Capture $form 'manager-live2d'
                Invoke-Method $form 'Navigate' @('maintenance')
                Capture $form 'manager-maintenance'
                Invoke-Method $form 'Navigate' @('themes')
                (Get-Field $form 'searchBox').Text = 'no-matching-theme-for-inspection'
                Capture $form 'manager-empty-search'
                (Get-Field $form 'searchBox').Text = ''
            } elseif ($label -eq 'installer') {
                $reuse = Join-Path $repo 'artifacts\runtime-reuse-test\MalodyV'
                if (Test-Path $reuse) {
                    (Get-Field $form 'pathBox').Text = $reuse
                    Invoke-Method $form 'UpdatePathState'
                    Capture $form 'installer-reuse'
                }
                (Get-Field $form 'pathBox').Text = 'Z:\Missing\MalodyV'
                Invoke-Method $form 'UpdatePathState'
                Capture $form 'installer-invalid'
                (Get-Field $form 'pathBox').Text = $fakeGame
                Invoke-Method $form 'UpdatePathState'
            }
            $form.Scale([Drawing.SizeF]::new(1.5,1.5))
            Capture $form "$label-scale150"
        }
    } finally { $form.Close(); $form.Dispose() }
}
