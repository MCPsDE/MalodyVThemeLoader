param([ValidateSet('Write','Reload')][string]$Phase = 'Write')
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
    $selector = $controls | Where-Object Name -eq 'appearanceSelector' | Select-Object -First 1
    $sponsor = $controls | Where-Object Name -eq 'sponsorButton' | Select-Object -First 1
    if (!$selector -or !$sponsor -or $sponsor.Text -ne '赞助支持') { throw 'Appearance selector or exact sponsor label missing.' }
    if (!($controls | Where-Object Text -eq 'by 猫猫虫小窝(451586654)')) { throw 'Required watermark missing.' }
    $darkBackground = $form.BackColor.ToArgb()
    $darkColors = @($controls | ForEach-Object { "$($_.BackColor.ToArgb()):$($_.ForeColor.ToArgb())" })
    $selector.SelectedIndex = 1
    [Windows.Forms.Application]::DoEvents()
    if ($appearance.GetProperty('IsDark').GetValue($null) -or $form.BackColor.ToArgb() -eq $darkBackground) { throw 'Selector did not apply light appearance.' }
    if ((Get-Content $preferences -Raw | ConvertFrom-Json).mode -ne 'light') { throw 'Light appearance was not persisted.' }
    $selector.SelectedIndex = 0
    [Windows.Forms.Application]::DoEvents()
    $restoredColors = @($controls | ForEach-Object { "$($_.BackColor.ToArgb()):$($_.ForeColor.ToArgb())" })
    if (Compare-Object $darkColors $restoredColors -SyncWindow 0) { throw 'Dark/light round trip changed control color roles.' }
    $formType.GetMethod('Navigate',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($form,[object[]]@('live2d')) | Out-Null
    if ((Field 'live2dImportAnimation').Enabled) { throw 'Animation import is enabled without a selected model.' }
    if ($controls | Where-Object { $_.Text -match 'Live2D rig|凭空生成|参数绑定|只能导入已有兼容格式' }) { throw 'Removed implementation explanation still appears in the UI.' }
    # Drive the real selector, leaving light mode saved for the cold reload phase.
    $selector.SelectedIndex = 1
    Write-Output 'Dark default, real selector, persistence, color round trip, watermark and sponsor label verified.'
} finally { $form.Close(); $form.Dispose() }
