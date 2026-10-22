param([string]$BuildRoot = '', [string]$OutputRoot = '', [switch]$Baseline, [ValidateSet('dark','light')][string]$Appearance = 'dark', [string]$Live2dSample = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputRoot) { $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) 'MalodyVThemeLoader-ui' }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$env:MALODY_THEMELOADER_DISABLE_UPDATES = '1'
$env:MALODY_THEMELOADER_PREFERENCES = Join-Path $repo ('artifacts\ui-preferences-' + $Appearance)
New-Item -ItemType Directory -Path $env:MALODY_THEMELOADER_PREFERENCES -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $env:MALODY_THEMELOADER_PREFERENCES 'appearance.json'), ('{"mode":"' + $Appearance + '"}'))
$fakeGame = Join-Path $repo 'artifacts\ui-fixture\MalodyV'
New-Item -ItemType Directory -Path (Join-Path $fakeGame 'ThemeLoader\themes'),(Join-Path $fakeGame 'Malody V_Data\il2cpp_data') -Force | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')) { [IO.File]::WriteAllBytes((Join-Path $fakeGame $name),[byte[]](1,2,3)) }
$fixtureCatalog=Join-Path $repo 'artifacts\payload\BepInEx\plugins\MalodyVThemeLoader\catalog.json'
if(Test-Path $fixtureCatalog) {
    $fixturePlugin=Join-Path $fakeGame 'BepInEx\plugins\MalodyVThemeLoader'
    New-Item -ItemType Directory -Path $fixturePlugin -Force | Out-Null
    Copy-Item -LiteralPath $fixtureCatalog -Destination $fixturePlugin -Force
}
if (Test-Path (Join-Path $repo 'artifacts\payload\ThemeLoader\themes\Default')) {
    $fixtureDefault = [IO.Path]::GetFullPath((Join-Path $fakeGame 'ThemeLoader\themes\Default'))
    $fixtureAllowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
    if (!$fixtureDefault.StartsWith($fixtureAllowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe UI fixture path.' }
    if (Test-Path -LiteralPath $fixtureDefault) { Remove-Item -LiteralPath $fixtureDefault -Force -Recurse }
    Copy-Item -LiteralPath (Join-Path $repo 'artifacts\payload\ThemeLoader\themes\Default') -Destination $fixtureDefault -Recurse -Force
} elseif (Test-Path (Join-Path $repo 'Default.mtp')) {
    Copy-Item -LiteralPath (Join-Path $repo 'Default.mtp') -Destination (Join-Path $fakeGame 'ThemeLoader\themes\Default.mtp') -Force
}
$assemblies = @{}
if ($Live2dSample) {
    $fixtureModel = Join-Path $fakeGame 'ThemeLoader\themes\Murasame\live2d\models'
    New-Item -ItemType Directory -Path $fixtureModel -Force | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $Live2dSample) { Copy-Item -LiteralPath $entry.FullName -Destination $fixtureModel -Recurse -Force }
    [IO.File]::WriteAllText((Join-Path $fakeGame 'ThemeLoader\themes\Murasame\theme.json'), '{"formatVersion":1,"id":"murasame","name":"Murasame","live2dRoot":"live2d"}')
    [IO.File]::WriteAllText((Join-Path $fakeGame 'ThemeLoader\active-theme.txt'), 'Murasame')
}
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
function Reach-Live2dFields($form) {
    $idle = Get-Field $form 'live2dIdle'
    $tap = Get-Field $form 'live2dTap'
    if (!$idle.Enabled) { return }
    $scroll = $idle.Parent
    while ($scroll -and !($scroll -is [Windows.Forms.Panel] -and $scroll.AutoScroll)) { $scroll = $scroll.Parent }
    if (!$scroll) { throw 'Binding fields have no scroll parent.' }
    $scroll.ScrollControlIntoView($idle)
    $idle.Focus() | Out-Null
    [Windows.Forms.Application]::DoEvents()
    if (!$form.SelectNextControl($idle,$true,$true,$true,$true) -or !$tap.Focused) { throw 'Tab cannot reach tap animation from idle.' }
    $scale = Get-Field $form 'live2dScale'
    $animationEditor = Get-Field $form 'live2dEditor'
    if (!$form.SelectNextControl($tap,$true,$true,$true,$true) -or !$animationEditor.Focused) { throw 'Tab cannot reach the animation editor from tap animation.' }
    if (!$form.SelectNextControl($animationEditor,$true,$true,$true,$true) -or !$scale.ContainsFocus) { throw 'Tab cannot reach model size from the animation editor.' }
    [Windows.Forms.Application]::DoEvents()
    $viewport = $scroll.RectangleToScreen($scroll.ClientRectangle)
    if (!$scroll.Parent.RectangleToScreen($scroll.Parent.ClientRectangle).Contains($viewport)) {
        throw 'Editor scroll viewport exceeds its page bounds.'
    }
    foreach ($field in @($idle, $tap, $scale)) {
        $bounds = $field.RectangleToScreen($field.ClientRectangle)
        if (!$viewport.Contains($bounds)) { throw "Binding field is clipped after scrolling: $($field.AccessibleName)" }
    }
    $theme = Get-Field $form 'live2dTheme'
    if ($theme.Width -gt $scroll.ClientSize.Width) { throw 'Theme selector exceeds scroll viewport.' }
    Write-Output 'Live2D idle/tap/size reached with scrolling and Tab; field rectangles fit the viewport.'
}
foreach ($name in $assemblies.Keys) {
    $windows = [Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetDirectoryName($assemblies[$name].Location)) 'MalodyVThemeLoader.Windows.dll'))
    $windows.GetType('MalodyVThemeLoader.Windows.AppAppearance').GetMethod('SetDarkMode').Invoke(
        $null,[object[]]@([bool]($Appearance -eq 'dark'),[bool]$false))
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
        }
        if ($isManager -and !$Baseline) {
            Invoke-Method $form 'Navigate' @('live2d')
            $form.Size = $form.MinimumSize
            Capture $form 'manager-live2d-minimum'
            Reach-Live2dFields $form
            Capture $form 'manager-live2d-minimum-fields'
            $windows = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'MalodyVThemeLoader.Windows' } | Select-Object -First 1
            $switcher = $windows.GetType('MalodyVThemeLoader.Windows.AppAppearance')
            $switcher.GetMethod('SetDarkMode').Invoke($null, [object[]]@([bool]($Appearance -ne 'dark'),[bool]$false))
            Capture $form 'manager-switched'
            $switcher.GetMethod('SetDarkMode').Invoke($null, [object[]]@([bool]($Appearance -eq 'dark'),[bool]$false))
            Capture $form 'manager-restored'
        }
        if (!$Baseline) {
            $form.ClientSize = [Drawing.Size]::new($(if ($isManager) {1120} else {880}), $(if ($isManager) {800} else {720}))
            $form.Scale([Drawing.SizeF]::new(1.5,1.5))
            # Synthetic Scale may be clamped to the desktop mid-layout. Trigger
            # a real client resize afterwards so Dock/AutoScroll recompute from
            # the actual available size rather than the unclamped target.
            $clamped = $form.ClientSize
            $form.ClientSize = [Drawing.Size]::new($clamped.Width - 1, $clamped.Height - 1)
            $form.ClientSize = $clamped
            $form.PerformLayout()
            if ($isManager) {
                $themeField = Get-Field $form 'live2dTheme'
                $themeField.Focus() | Out-Null
                $editorScroll = $themeField.Parent
                while ($editorScroll -and !($editorScroll -is [Windows.Forms.Panel] -and $editorScroll.AutoScroll)) { $editorScroll = $editorScroll.Parent }
                if ($editorScroll) { $editorScroll.AutoScrollPosition = [Drawing.Point]::Empty }
            }
            Capture $form "$label-scale150"
            if ($isManager) {
                Reach-Live2dFields $form
                Capture $form 'manager-scale150-fields'
            }
        }
    } finally { $form.Close(); $form.Dispose() }
}
