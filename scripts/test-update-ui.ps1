param([ValidateSet('dark','light')][string]$Appearance='dark')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:MALODY_THEMELOADER_DISABLE_UPDATES='1'
$env:MALODY_THEMELOADER_PREFERENCES=Join-Path $repo ('artifacts/update-ui-preferences-'+$Appearance)
$dll=Join-Path $repo 'src/MalodyVThemeLoader.ThemeManager/bin/Release/net8.0-windows/win-x64'
$manager=[Reflection.Assembly]::LoadFrom((Join-Path $dll 'MalodyVThemeLoader.ThemeManager.dll'))
$windows=[Reflection.Assembly]::LoadFrom((Join-Path $dll 'MalodyVThemeLoader.Windows.dll'))
$windows.GetType('MalodyVThemeLoader.Windows.AppAppearance').GetMethod('SetDarkMode').Invoke($null,@([bool]($Appearance -eq 'dark'),[bool]$false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
function Field($c,$name){$c.GetType().GetField($name,$flags).GetValue($c)}
function Invoke($c,$name,[object[]]$arguments=@()){$c.GetType().GetMethod($name,$flags).Invoke($c,$arguments)}
function Assert($value,$message){if(!$value){throw $message}}
. (Join-Path $repo 'scripts/assert-ui-fit.ps1')
$out=Join-Path $repo ('.impeccable/review/update-'+$Appearance)
New-Item -ItemType Directory -Path $out -Force|Out-Null
function Capture($form,$name){[Windows.Forms.Application]::DoEvents();Assert-UiFits $form $name;$b=[Drawing.Bitmap]::new($form.Width,$form.Height);try{$form.DrawToBitmap($b,[Drawing.Rectangle]::new(0,0,$b.Width,$b.Height));$b.Save((Join-Path $out ($name+'.png')))}finally{$b.Dispose()}}
$progressType=$manager.GetType('MalodyVThemeLoader.ThemeManager.UpdateProgress')
foreach($case in @('desktop','minimum','scale150','font125')){
    $form=[Activator]::CreateInstance($manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm'),@([string](Join-Path $repo ('artifacts/resource-editor-'+$Appearance))))
    try{
        $form.Show();[Windows.Forms.Application]::DoEvents();$strip=Field $form 'updateStrip'
        if($case -eq 'minimum' -or $case -eq 'font125'){$form.Size=$form.MinimumSize}
        if($case -eq 'scale150'){$form.Scale([Drawing.SizeF]::new(1.5,1.5))}
        if($case -eq 'font125'){
            function Walk($c){foreach($child in $c.Controls){$child;Walk $child}}
            $fonts=@(Walk $form)|ForEach-Object{[pscustomobject]@{Control=$_.PSObject.BaseObject;Family=$_.Font.FontFamily.Name;Size=$_.Font.Size;Style=$_.Font.Style}}
            foreach($f in $fonts){$f.Control.Font=[Drawing.Font]::new($f.Family,[single]($f.Size*1.25),$f.Style)}
            Invoke $form 'FitNavigation'|Out-Null
        }
        $p=[Activator]::CreateInstance($progressType,@('正在下载 2.6.3',[long]38797312,[long]104857600));Invoke $strip 'ShowProgress' @($p)|Out-Null
        Capture $form ($case+'-download');Assert ((Field $strip 'progress').AccessibilityObject.Value -eq '37%') 'Progress not accessible.'
        if($case -eq 'minimum'){
            $unknown=[Activator]::CreateInstance($progressType,@('正在下载 2.6.3',[long]121634816,$null));Invoke $strip 'ShowProgress' @($unknown)|Out-Null;Capture $form 'minimum-unknown'
            $verify=[Activator]::CreateInstance($progressType,@('正在校验安装包…',$null,$null));Invoke $strip 'ShowProgress' @($verify)|Out-Null;Capture $form 'minimum-verify'
            $resultType=$manager.GetType('MalodyVThemeLoader.ThemeManager.UpdateResult');$failure=$resultType.GetMethod('Failed').Invoke($null,@('更新连接超时 · 请重试更新'))
            Invoke $strip 'ShowResult' @($failure)|Out-Null;Capture $form 'minimum-failure'
            Assert ((Field $strip 'action').Text -eq '重试更新') 'Failure has no retry action.'
        }
    }finally{$form.Dispose()}
}
Write-Output 'PASS update UI: download, unknown length, verification, retry, normal/minimum and synthetic font/DPI.'
