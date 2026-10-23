param([ValidateSet('dark','light')][string]$Appearance='dark',[string]$CaptureDirectory='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fake=Join-Path $repo ('artifacts\inspector-editor-'+$Appearance)
$plugin=Join-Path $fake 'BepInEx\plugins\MalodyVThemeLoader'
$theme=Join-Path $fake 'ThemeLoader\themes\Inspector'
New-Item -ItemType Directory -Path $plugin,$theme,(Join-Path $fake 'Malody V_Data') -Force | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')){[IO.File]::WriteAllBytes((Join-Path $fake $name),[byte[]](1,2,3))}
Copy-Item -LiteralPath (Join-Path $repo 'artifacts\payload\BepInEx\plugins\MalodyVThemeLoader\catalog.json') -Destination $plugin -Force
[IO.File]::WriteAllText((Join-Path $theme 'theme.json'),'{"formatVersion":1,"id":"Inspector","name":"在线调试验证主题"}')
if(Test-Path (Join-Path $theme 'resources.json')){Remove-Item -LiteralPath (Join-Path $theme 'resources.json')}
$env:MALODY_THEMELOADER_DISABLE_UPDATES='1'
$env:MALODY_THEMELOADER_PREFERENCES=Join-Path $fake 'preferences'
$dllRoot=Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64'
$manager=[Reflection.Assembly]::LoadFrom((Join-Path $dllRoot 'MalodyVThemeLoader.ThemeManager.dll'))
$windows=[Reflection.Assembly]::LoadFrom((Join-Path $dllRoot 'MalodyVThemeLoader.Windows.dll'))
$shared=[Reflection.Assembly]::LoadFrom((Join-Path $dllRoot 'MalodyVThemeLoader.Shared.dll'))
$windows.GetType('MalodyVThemeLoader.Windows.AppAppearance').GetMethod('SetDarkMode').Invoke($null,@([bool]($Appearance -eq 'dark'),[bool]$false))
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
function Field($object,$name){$object.GetType().GetField($name,$flags).GetValue($object)}
function Invoke($object,$name,[object[]]$arguments=@()){$object.GetType().GetMethod($name,$flags).Invoke($object,$arguments)}
function Assert($value,$text){if(!$value){throw $text}}
function Await($task){while(!$task.IsCompleted){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 15};$task.GetAwaiter().GetResult()|Out-Null}
function Create($name){[Activator]::CreateInstance($shared.GetType('MalodyVThemeLoader.Shared.'+$name))}
. (Join-Path $repo 'scripts/assert-ui-fit.ps1')
$out=if($CaptureDirectory){[IO.Path]::GetFullPath((Join-Path $repo $CaptureDirectory))}else{Join-Path $repo ('.impeccable\review\inspector-'+$Appearance)}
New-Item -ItemType Directory -Path $out -Force | Out-Null
function Capture($form,$name){[Windows.Forms.Application]::DoEvents();Assert-UiFits $form $name;$b=[Drawing.Bitmap]::new($form.Width,$form.Height);try{$form.DrawToBitmap($b,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height));$b.Save((Join-Path $out ($name+'.png')))}finally{$b.Dispose()}}
$form=[Activator]::CreateInstance($manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm'),@([string]$fake))
try{
    $form.Show();[Windows.Forms.Application]::DoEvents();Invoke $form 'Navigate' @('resources')|Out-Null
    $editor=Field $form 'resourceEditor';Invoke $editor 'ShowInspector' @([bool]$true)|Out-Null
    $pane=Field $editor 'inspectorPane';$workspace=Field $editor 'workspace'
    (Field $pane 'timer').Stop()
    Capture $form 'disconnected'
    (Field $pane 'connect').Checked=$true
    $snapshot=Create 'InspectorSnapshot';$snapshot.Runtime='runtime-fixture';$snapshot.Client=(Field $pane 'control').Client;$snapshot.PluginVersion='2.6.0';$snapshot.Theme='Inspector';$snapshot.UpdatedUtc=[DateTime]::UtcNow
    $root=Create 'InspectorNode';$root.Id=1;$root.Name='Canvas';$root.Scene='SceneSong';$root.Address='Canvas#0:1';$root.Hierarchy='Canvas';$root.Active=$true;$root.CanDecorate=$true
    $snapshot.Nodes.Add($root)
    $node=Create 'InspectorNode';$node.Id=2;$node.ParentId=1;$node.Name='空白容器 · 可添加底图';$node.Scene='SceneSong';$node.Address='Canvas#0:1/Panel#0:1';$node.Hierarchy='Canvas/Panel';$node.Component='RectTransform';$node.Active=$true;$node.CanDecorate=$true;$node.Width=320;$node.Height=200
    $snapshot.Nodes.Add($node)
    $entry=$workspace.Catalog.Images | Where-Object LogicalId -eq 'Textures/Malody/editor/btn_editsys_base.png'|Select-Object -First 1
    foreach($i in 0,1){$n=Create 'InspectorNode';$n.Id=3+$i;$n.ParentId=1;$n.Name='复用图片';$n.Scene='SceneSong';$n.Address="Canvas#0:1/Image#$($i):2";$n.Hierarchy='Canvas/Image';$n.Component='Image';$n.LogicalId=$entry.LogicalId;$n.Active=$true;$n.CanDecorate=$true;$snapshot.Nodes.Add($n)}
    $hidden=Create 'InspectorNode';$hidden.Id=8;$hidden.ParentId=1;$hidden.Scene='SceneSong';$hidden.Name='隐藏节点';$hidden.Address='Canvas#0:1/Hidden#0:1';$hidden.Hierarchy='Canvas/Hidden';$snapshot.Nodes.Add($hidden)
    $snapshot.SelectedId=2;$snapshot.SelectionRevision=1;$snapshot.SelectionOrigin='editor'
    Invoke $pane 'AcceptSnapshot' @($snapshot)|Out-Null
    Assert ((Field $pane 'node').Id -eq 2) 'Live node selection failed.'
    Assert ((Field $pane 'addImage').Enabled) 'Empty node cannot receive a background.'
    Await (Invoke $pane 'ImportImageAsync' @([string](Join-Path $repo 'artifacts\inspector-media\background.png')))
    Await (Invoke $pane 'ImportVideoAsync' @([string](Join-Path $repo 'artifacts\inspector-media\vp8.webm')))
    (Field $pane 'opacity').Value=45
    (Field $pane 'fit').SelectedIndex=2
    Capture $form 'node-draft'
    Await (Invoke $pane 'SaveAsync')
    $rules=$workspace.Rules;$rule=$rules.Nodes[0]
    Assert ($rules.Nodes.Count -eq 1 -and $rule.ImageFile.Length -gt 0 -and $rule.VideoFile.EndsWith('.webm') -and [Math]::Abs($rule.VideoOpacity-.45) -lt .001) 'Node image/video/opacity were not persisted.'
    Assert (Test-Path ($workspace.NodeFile($rule.VideoFile))) 'Video was not stored within the theme.'
    Assert ((Get-Content (Join-Path $fake 'ThemeLoader\settings.json') -Raw|ConvertFrom-Json).realtimeMode) 'Saving a node did not enable realtime.'
    Assert ((Get-Content (Join-Path $fake 'ThemeLoader\active-theme.txt') -Raw).Trim() -eq 'Inspector') 'Saving did not activate the edited theme.'
    Capture $form 'node-media'
    $opacity=Field $pane 'opacity';$scroll=$opacity.Parent.Parent
    while($scroll -and !($scroll -is [Windows.Forms.Panel] -and $scroll.AutoScroll)){$scroll=$scroll.Parent}
    $scroll.ScrollControlIntoView($opacity);[Windows.Forms.Application]::DoEvents();Capture $form 'video-settings'
    $before=Get-Content (Join-Path $theme 'resources.json') -Raw
    Await (Invoke $pane 'ImportVideoAsync' @([string](Join-Path $repo 'artifacts\inspector-media\vp9.webm')))
    Assert ((Field $pane 'state').Text -match 'VP8') 'VP9 did not produce actionable validation.'
    Assert ((Get-Content (Join-Path $theme 'resources.json') -Raw) -eq $before) 'Invalid video changed saved rules.'
    $snapshot.SelectionRevision=2;$snapshot.SelectedId=3;$snapshot.SelectionOrigin='game';Invoke $pane 'AcceptSnapshot' @($snapshot)|Out-Null
    Assert ((Field $editor 'image').LogicalId -eq $entry.LogicalId -and (Field $editor 'liveNodeAddress') -eq 'Canvas#0:1/Image#0:2') 'Game pick did not locate the exact resource/node.'
    Assert ((Field $editor 'body').Visible) 'Game pick did not open resource editing.'
    Await (Invoke $editor 'ImportFileAsync' @([string](Join-Path $repo 'artifacts\inspector-media\background.png')))
    Await (Invoke $editor 'SaveAsync')
    Assert ($workspace.Rules.Images[0].NodeAddress -eq 'Canvas#0:1/Image#0:2') 'Exact node address was lost when saving the image.'
    Assert ($null -eq $workspace.Rules.ResolveImage($entry.LogicalId,'SceneSong','Canvas/Image','Image','Canvas#0:1/Image#1:2')) 'Node rule leaked to same-name sibling.'
    Capture $form 'picked-resource'
    Invoke $editor 'ShowInspector' @([bool]$true)|Out-Null
    $snapshot.SelectedId=2;$snapshot.SelectionRevision=3;$snapshot.SelectionOrigin='editor';Invoke $pane 'AcceptSnapshot' @($snapshot)|Out-Null
    $form.Size=$form.MinimumSize;$scroll.AutoScrollPosition=[Drawing.Point]::Empty;[Windows.Forms.Application]::DoEvents();Capture $form 'minimum'
    $scroll.ScrollControlIntoView($opacity);[Windows.Forms.Application]::DoEvents();Capture $form 'minimum-video'
    $form.ClientSize=[Drawing.Size]::new(1120,800);$form.Scale([Drawing.SizeF]::new(1.5,1.5));$scroll.ScrollControlIntoView($opacity);[Windows.Forms.Application]::DoEvents();Capture $form 'scale150'
    Invoke $pane 'Restore'|Out-Null;Assert ($workspace.Rules.Nodes.Count -eq 0 -and $workspace.Rules.Images.Count -eq 1) 'Restore removed unrelated image rules.'
    $snapshot.SelectedId=3;$snapshot.SelectionRevision=4;Invoke $pane 'AcceptSnapshot' @($snapshot)|Out-Null
    $search=Field $pane 'search';$search.Text='隐藏节点';(Field $pane 'inactive').Checked=$true
    Assert ((Field $pane 'tree').Nodes[0].Nodes.Count -eq 1) 'Hidden/search hierarchy filtering failed.'
    $package=Join-Path $fake 'Inspector.zip';$type=$shared.GetType('MalodyVThemeLoader.Shared.ThemePackage');$type.GetMethod('CreateFromDirectory').Invoke($null,@([string]$theme,[string]$package))|Out-Null
    $extracted=Join-Path $fake 'round-trip';$type.GetMethod('ExtractToDirectory').Invoke($null,@([string]$package,[string]$extracted))|Out-Null
    Assert (Test-Path (Join-Path $extracted 'resources.json')) 'ZIP did not retain resource rules.'
    Write-Output 'PASS: live tree/search/hidden nodes, empty-node image + VP8 alpha video, opacity/fit, invalid-codec preservation, exact game-pick resource navigation, duplicate isolation, realtime activation, restore, ZIP and UI geometry.'
}finally{$form.Close();$form.Dispose()}
