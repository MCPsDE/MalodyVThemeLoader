param([ValidateSet('dark','light')][string]$Appearance='dark',[switch]$Capture)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:MALODY_THEMELOADER_DISABLE_UPDATES='1'
$env:MALODY_THEMELOADER_PREFERENCES=Join-Path $repo ('artifacts\controls-preferences-'+$Appearance)
$managerPath=Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64'
$manager=[Reflection.Assembly]::LoadFrom((Join-Path $managerPath 'MalodyVThemeLoader.ThemeManager.dll'))
$windows=[Reflection.Assembly]::LoadFrom((Join-Path $managerPath 'MalodyVThemeLoader.Windows.dll'))
$appearanceType=$windows.GetType('MalodyVThemeLoader.Windows.AppAppearance')
$appearanceType.GetMethod('SetDarkMode').Invoke($null,@([bool]($Appearance -eq 'dark'),[bool]$false))
$numberType=$windows.GetType('MalodyVThemeLoader.Windows.NumberField')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field($c,$name) { $c.GetType().GetField($name,$flags).GetValue($c) }
function Invoke($c,$name,[object[]]$arguments=@()) { $c.GetType().GetMethod($name,[Reflection.BindingFlags]'Instance,NonPublic,Public').Invoke($c,$arguments) }
function Assert($value,$message) { if(!$value){throw $message} }
function Walk($c) { foreach($child in $c.Controls){$child; Walk $child} }
function Capture-Form($form,$name) {
    if(!$Capture){return}
    $out=Join-Path $repo ('.impeccable\review\controls-'+$Appearance)
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    [Windows.Forms.Application]::DoEvents()
    $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
    try{$form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height));$bitmap.Save((Join-Path $out ($name+'.png')))}finally{$bitmap.Dispose()}
}
. (Join-Path $repo 'scripts/assert-ui-fit.ps1')
function Check-Controls($form,$caseName) { Assert-UiFits $form $caseName }
# Exercise the actual shared widget's input, focus, range and accessibility.
$probe=[Windows.Forms.Form]::new();$probe.ClientSize=[Drawing.Size]::new(320,140);$probe.Font=[Drawing.Font]::new('Microsoft YaHei UI',10)
$number=[Activator]::CreateInstance($numberType);$number.Minimum=-200;$number.Maximum=16384;$number.Value=10;$number.Width=220;$number.Location=[Drawing.Point]::new(20,25);$number.AccessibleName='测试数值'
$probe.Controls.Add($number);$appearanceType.GetMethod('Initialize').Invoke($null,@($probe))
try{
    $probe.Show();[Windows.Forms.Application]::DoEvents()
    $minus=Field $number 'minus';$plus=Field $number 'plus'
    $plus.PerformClick();Assert ($number.Value -eq 11) 'Plus did not increment by one.'
    $minus.PerformClick();Assert ($number.Value -eq 10) 'Minus did not decrement by one.'
    $number.Text='-';$number.PerformLayout();Assert ($number.Text -eq '-') 'Layout committed an incomplete numeric edit.'
    $number.Text='-17';$number.CommitEdit();Assert ($number.Value -eq -17) 'Native numeric parsing was lost.'
    $native=Field $number 'editor';$edit=@($native.Controls | Where-Object {$_ -is [Windows.Forms.TextBox]})[0]
    $edit.GetType().GetMethod('OnKeyDown',$flags).Invoke($edit,@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Up))) | Out-Null
    Assert ($number.Value -eq -16) 'Native Up key did not increment.'
    $number.Value=$number.Maximum;Assert (!$plus.Enabled) 'Maximum did not disable increment.'
    $number.Value=$number.Minimum;Assert (!$minus.Enabled) 'Minimum did not disable decrement.'
    $number.AccessibilityObject.Value='25';Assert ($number.Value -eq 25) 'Accessible numeric value was not writable.'
    $number.Enabled=$false;Assert (!$plus.Enabled -and !$minus.Enabled) 'Disabled number still steps.'
    $number.Enabled=$true;Check-Controls $probe 'widget';Capture-Form $probe 'number-field'
}finally{$probe.Dispose()}

$game=Join-Path $repo ('artifacts\resource-editor-'+$Appearance)
if(!(Test-Path (Join-Path $game 'ThemeLoader/themes/Default/theme.json'))){throw 'Prepare the resource-editor fixture before running control checks.'}
$cases=@(
    @{Name='desktop';Scale=1.0;Minimum=$false;Font=1.0},
    @{Name='minimum';Scale=1.0;Minimum=$true;Font=1.0},
    @{Name='scale125';Scale=1.25;Minimum=$false;Font=1.0},
    @{Name='scale150';Scale=1.5;Minimum=$false;Font=1.0},
    @{Name='scale200';Scale=2.0;Minimum=$false;Font=1.0},
    @{Name='font125-minimum';Scale=1.0;Minimum=$true;Font=1.25}
)
foreach($case in $cases) {
    $form=[Activator]::CreateInstance($manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm'),@([string]$game))
    try{
        $form.Show();[Windows.Forms.Application]::DoEvents()
        if($case.Minimum){$form.Size=$form.MinimumSize}
        if($case.Scale -ne 1){$form.Scale([Drawing.SizeF]::new($case.Scale,$case.Scale));$size=$form.ClientSize;$form.ClientSize=[Drawing.Size]::new($size.Width-1,$size.Height-1);$form.ClientSize=$size}
        if($case.Font -ne 1){
            $fonts=@(Walk $form) | ForEach-Object {[pscustomobject]@{Control=$_.PSObject.BaseObject;Family=$_.Font.FontFamily.Name;Size=$_.Font.Size;Style=$_.Font.Style}}
            foreach($font in $fonts){$font.Control.Font=[Drawing.Font]::new($font.Family,[single]($font.Size*$case.Font),$font.Style)}
            Invoke $form 'FitNavigation' | Out-Null
        }
        foreach($page in @('themes','resources','live2d','settings','maintenance')) {
            Invoke $form 'Navigate' @($page) | Out-Null;[Windows.Forms.Application]::DoEvents()
            Check-Controls $form ($case.Name+'/'+$page)
            if($page -eq 'resources') {
                $editor=Field $form 'resourceEditor';$workspace=Field $editor 'workspace'
                $entry=$workspace.Catalog.Images | Where-Object LogicalId -eq 'Textures/Malody/editor/btn_editsys_base.png' | Select-Object -First 1
                Invoke $editor 'SelectResource' @($entry.PSObject.BaseObject) | Out-Null
                [Windows.Forms.Application]::DoEvents();Check-Controls $form ($case.Name+'/image-fields')
            }
            if($Capture -and (($case.Name -in @('desktop','minimum') -and $page -in @('themes','resources')) -or ($case.Name -eq 'scale150' -and $page -eq 'resources'))) {Capture-Form $form ($case.Name+'-'+$page)}
        }
    }finally{$form.Dispose()}
}
foreach($project in @('MalodyVThemeLoader.Installer','MalodyVBepInExInstaller')) {
    $dll=Join-Path $repo ("src/"+$project+"/bin/Release/net8.0-windows/win-x64/"+$project+".dll")
    $assembly=[Reflection.Assembly]::LoadFrom($dll)
    foreach($case in $cases) {
        $form=[Activator]::CreateInstance($assembly.GetType($project+'.InstallerForm'),$true)
        try {
            $form.Show();[Windows.Forms.Application]::DoEvents()
            if($case.Minimum){$form.Size=$form.MinimumSize}
            if($case.Scale -ne 1){$form.Scale([Drawing.SizeF]::new($case.Scale,$case.Scale));$size=$form.ClientSize;$form.ClientSize=[Drawing.Size]::new($size.Width-1,$size.Height-1);$form.ClientSize=$size}
            if($case.Font -ne 1) {
                $fonts=@(Walk $form) | ForEach-Object {[pscustomobject]@{Control=$_.PSObject.BaseObject;Family=$_.Font.FontFamily.Name;Size=$_.Font.Size;Style=$_.Font.Style}}
                foreach($font in $fonts){$font.Control.Font=[Drawing.Font]::new($font.Family,[single]($font.Size*$case.Font),$font.Style)}
            }
            [Windows.Forms.Application]::DoEvents();Check-Controls $form ($project+'/'+$case.Name)
            if($case.Name -in @('desktop','minimum')) {Capture-Form $form ($project+'-'+$case.Name)}
        }finally{$form.Dispose()}
    }
}
Write-Output 'PASS: numeric typing/keys/ranges/accessibility; full action/navigation labels on five manager pages and both installers at desktop/minimum, synthetic 125/150/200% and 125% font size.'
