param([ValidateSet('Write','Reload','Installers')][string]$Phase = 'Write')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::EnableVisualStyles()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:MALODY_THEMELOADER_DISABLE_UPDATES = '1'
$env:MALODY_THEMELOADER_PREFERENCES = Join-Path $repo 'artifacts\appearance-test'
New-Item -ItemType Directory -Path $env:MALODY_THEMELOADER_PREFERENCES -Force | Out-Null
$preferences = Join-Path $env:MALODY_THEMELOADER_PREFERENCES 'appearance.json'
if ($Phase -eq 'Write' -and (Test-Path $preferences)) { Remove-Item -LiteralPath $preferences -Force }
$manager = [Reflection.Assembly]::LoadFrom((Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.ThemeManager.dll'))
$windows = [Reflection.Assembly]::LoadFrom((Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.Windows.dll'))
$appearance = $windows.GetType('MalodyVThemeLoader.Windows.AppAppearance',$true)
$branding = $windows.GetType('MalodyVThemeLoader.Windows.BrandBar',$true)
if ($branding.GetField('SponsorUrl').GetRawConstantValue() -ne 'https://ifdian.net/a/mcpsde') { throw 'Wrong sponsor URL.' }
if ($Phase -eq 'Reload') {
    if ($appearance.GetProperty('IsDark').GetValue($null)) { throw 'Saved light appearance was not restored in a new process.' }
    Write-Output 'Light appearance restored in a fresh process.'
    exit 0
}
if ($Phase -eq 'Installers') {
    # Installers must start light even with a persisted dark manager preference.
    $appearance.GetMethod('SetDarkMode').Invoke($null,[object[]]@([bool]$true,[bool]$true))
    foreach($project in @('MalodyVThemeLoader.Installer','MalodyVBepInExInstaller')) {
        $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repo "src\$project\bin\Release\net8.0-windows\win-x64\$project.dll"))
        $installer = [Activator]::CreateInstance($assembly.GetType($project+'.InstallerForm'),$true)
        try {
            $installer.Show()
            [Windows.Forms.Application]::DoEvents()
            $all = @(& {
                function Walk($c) { foreach($child in $c.Controls) { $child; Walk $child } }
                Walk $installer
            })
            if ($appearance.GetProperty('IsDark').GetValue($null) -or $installer.BackColor.GetBrightness() -lt .9) { throw 'Installer did not use fixed light appearance.' }
            if ($all | Where-Object { $_.Name -in @('appearanceToggle','appearanceSelector','sponsorButton') }) { throw 'Installer contains manager-only controls.' }
            if ((Get-Content $preferences -Raw | ConvertFrom-Json).mode -ne 'dark') { throw 'Installer overwrote the manager preference.' }
        } finally { $installer.Close(); $installer.Dispose() }
    }
    Write-Output 'Both installers use fixed light appearance without changing the manager preference, toggles or sponsorship.'
    exit 0
}
if (!$appearance.GetProperty('IsDark').GetValue($null)) { throw 'A fresh installation did not default to dark mode.' }
$formType = $manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm',$true)
$fake = Join-Path $repo 'artifacts\appearance-test\MalodyV'
$form = [Activator]::CreateInstance($formType,[Reflection.BindingFlags]'Instance,Public,NonPublic',$null,[object[]]@([string]$fake),$null)
function Controls-Under($control) {
    foreach($child in $control.Controls) { $child; Controls-Under $child }
}
function Field([string]$name) {
    $formType.GetField($name,[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form)
}
try {
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $controls = @(Controls-Under $form)
    $selector = $controls | Where-Object Name -eq 'appearanceToggle' | Select-Object -First 1
    $sponsor = $controls | Where-Object Name -eq 'sponsorButton' | Select-Object -First 1
    if (!$selector -or !$sponsor -or $sponsor.Text -ne '赞助支持') { throw 'Appearance selector or exact sponsor label missing.' }
    if (!($controls | Where-Object Text -eq 'by 猫猫虫小窝(451586654)')) { throw 'Required watermark missing.' }
    $darkBackground = $form.BackColor.ToArgb()
    $darkColors = @($controls | ForEach-Object { "$($_.BackColor.ToArgb()):$($_.ForeColor.ToArgb())" })
    $selector.PerformClick()
    [Windows.Forms.Application]::DoEvents()
    if ($appearance.GetProperty('IsDark').GetValue($null) -or $form.BackColor.ToArgb() -eq $darkBackground) { throw 'Selector did not apply light appearance.' }
    if ((Get-Content $preferences -Raw | ConvertFrom-Json).mode -ne 'light') { throw 'Light appearance was not persisted.' }
    $selector.PerformClick()
    [Windows.Forms.Application]::DoEvents()
    $restoredColors = @($controls | ForEach-Object { "$($_.BackColor.ToArgb()):$($_.ForeColor.ToArgb())" })
    if (Compare-Object $darkColors $restoredColors -SyncWindow 0) {
        for ($i = 0; $i -lt $controls.Count; $i++) {
            if ($darkColors[$i] -ne $restoredColors[$i]) { Write-Output "$($controls[$i].GetType().FullName): $($darkColors[$i]) -> $($restoredColors[$i])" }
        }
        throw 'Dark/light round trip changed control color roles.'
    }
    $formType.GetMethod('Navigate',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($form,[object[]]@('live2d')) | Out-Null
    if ((Field 'live2dImportAnimation').Enabled) { throw 'Animation import is enabled without a selected model.' }
    if ($controls | Where-Object { $_.Text -match 'Live2D rig|凭空生成|参数绑定|只能导入已有兼容格式' }) { throw 'Removed implementation explanation still appears in the UI.' }
    # Drive the real selector, leaving light mode saved for the cold reload phase.
    if (!$selector.AccessibilityObject.State.HasFlag([Windows.Forms.AccessibleStates]::Checked)) { throw 'Accessible toggle state is not checked in dark mode.' }
    $keyMethod = $selector.GetType().GetMethod('OnKeyDown',[Reflection.BindingFlags]'NonPublic,Instance')
    $keyMethod.Invoke($selector,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Left)))
    if ($selector.DarkSelected -or $appearance.GetProperty('IsDark').GetValue($null)) { throw 'Left arrow did not select sun/light.' }
    $keyMethod.Invoke($selector,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Right)))
    if (!$selector.DarkSelected) { throw 'Right arrow did not select moon/dark.' }
    $selector.PerformClick()
    Write-Output 'Sun/moon click and arrow keys, accessible state, preference persistence and color round trip verified.'
} finally { $form.Close(); $form.Dispose() }
