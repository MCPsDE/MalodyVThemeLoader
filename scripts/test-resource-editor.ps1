param(
    [ValidateSet('dark','light')][string]$Appearance = 'dark',
    [string]$ManagerDll = '',
    [string]$FixtureRoot = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$FixtureRoot) { $FixtureRoot = Join-Path $repo 'artifacts\resource-fixture' }
if (!$ManagerDll) { $ManagerDll = Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.ThemeManager.dll' }
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
if (!$FixtureRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'UI fixtures must stay inside artifacts.' }
$referenceFixture = $FixtureRoot
$FixtureRoot = Join-Path $repo ('artifacts\resource-editor-' + $Appearance)
$taskDefault = Join-Path $FixtureRoot 'ThemeLoader\themes\Default'
$taskCatalog = Join-Path $FixtureRoot 'BepInEx\plugins\MalodyVThemeLoader'
New-Item -ItemType Directory -Path (Split-Path $taskDefault),$taskCatalog -Force | Out-Null
if (!(Test-Path $taskDefault)) { New-Item -ItemType Junction -Path $taskDefault -Target (Join-Path $referenceFixture 'ThemeLoader\themes\Default') | Out-Null }
Copy-Item -LiteralPath (Join-Path $referenceFixture 'BepInEx\plugins\MalodyVThemeLoader\catalog.json') -Destination $taskCatalog -Force
Copy-Item -LiteralPath (Join-Path $repo 'src\MalodyVThemeLoader.Plugin\bin\Release\net6.0\MalodyVThemeLoader.Plugin.dll') -Destination $taskCatalog -Force
$env:MALODY_THEMELOADER_DISABLE_UPDATES = '1'
$env:MALODY_THEMELOADER_PREFERENCES = Join-Path $repo ('artifacts\resource-preferences-' + $Appearance)
$run = Join-Path $FixtureRoot ('checks-' + $Appearance)
$key = 'Author-' + $Appearance
$theme = Join-Path $FixtureRoot ('ThemeLoader\themes\' + $key)
New-Item -ItemType Directory -Path $run,$theme,(Join-Path $FixtureRoot 'Malody V_Data') -Force | Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')) { [IO.File]::WriteAllBytes((Join-Path $FixtureRoot $name),[byte[]](1,2,3)) }
if (Test-Path (Join-Path $theme 'resources.json')) { Remove-Item -LiteralPath (Join-Path $theme 'resources.json') }
[IO.File]::WriteAllText((Join-Path $theme 'theme.json'), ('{"formatVersion":1,"id":"' + $key + '","name":"创作主题 · ' + $Appearance + '"}'))
[IO.File]::WriteAllText((Join-Path $FixtureRoot 'ThemeLoader\active-theme.txt'),$key)
$out = Join-Path $repo ('.impeccable\review\resources-' + $Appearance)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$manager = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ManagerDll))
$shared = [Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $ManagerDll) 'MalodyVThemeLoader.Shared.dll'))
$windows = [Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $ManagerDll) 'MalodyVThemeLoader.Windows.dll'))
$windows.GetType('MalodyVThemeLoader.Windows.AppAppearance').GetMethod('SetDarkMode').Invoke($null,@([bool]($Appearance -eq 'dark'),[bool]$false))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Field($object,$name) { $object.GetType().GetField($name,$flags).GetValue($object) }
function Invoke($object,$name,[object[]]$arguments=@()) {
    $method=$object.GetType().GetMethod($name,[Reflection.BindingFlags]'Instance,NonPublic,Public')
    $method.Invoke($object,$arguments)
}
function Assert($value,$message) { if (!$value) { throw $message } }
function Await($task) {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while (!$task.IsCompleted) { [Windows.Forms.Application]::DoEvents(); if ($watch.Elapsed.TotalSeconds -gt 60) { throw 'Editor operation timed out.' }; Start-Sleep -Milliseconds 10 }
    $task.GetAwaiter().GetResult() | Out-Null
}
function Capture($form,$name) {
    [Windows.Forms.Application]::DoEvents()
    $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
    try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $out ($name+'.png'))) }
    finally { $bitmap.Dispose() }
}
function Leaf($nodes,$logical) {
    foreach($node in $nodes) { if ($node.Tag -and $node.Tag.LogicalId -eq $logical) { return $node }; $found=Leaf $node.Nodes $logical; if ($found) { return $found } }
}
$form=[Activator]::CreateInstance($manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm'),@([string]$FixtureRoot))
try {
    $form.Show(); [Windows.Forms.Application]::DoEvents(); Invoke $form 'Navigate' @('resources') | Out-Null
    $editor=Field $form 'resourceEditor'; $workspace=Field $editor 'workspace'; $catalog=$workspace.Catalog
    Assert ($catalog.Audio.Count -eq 10 -and ($catalog.Images | ForEach-Object { $_.Usages.Count } | Measure-Object -Sum).Sum -gt 300) 'Source scene/UI audio catalog is incomplete.'
    Assert (($catalog.Audio | Where-Object Event -eq Cancel).LogicalId -eq ($catalog.Audio | Where-Object Event -eq MsgFail).LogicalId) 'Shared audio slots were not resolved from the source.'
    $entry=$catalog.Images | Where-Object LogicalId -eq 'Textures/Malody/editor/btn_editsys_base.png' | Select-Object -First 1
    Assert ($null -ne $entry) 'No source sliced image found.'
    $source=Join-Path $FixtureRoot ('ThemeLoader\themes\Default\images\' + $entry.LogicalId)
    $alternative=$catalog.Images | Where-Object LogicalId -eq 'Textures/Malody/ui/border_up.png' | Select-Object -First 1
    $alternativePath=Join-Path $FixtureRoot ('ThemeLoader\themes\Default\images\' + $alternative.LogicalId)
    $tree=Field $editor 'tree'; $tree.SelectedNode=Leaf $tree.Nodes $entry.LogicalId
    Assert ((Field $editor 'image').LogicalId -eq $entry.LogicalId) 'Tree selection did not open the image.'
    Await (Invoke $editor 'ImportFileAsync' @([string]$source))
    $slice=Field $editor 'sliceToggle'; $slice.Checked=$true
    $numbers=Field $editor 'borders'; foreach($side in @('左','右','上','下')) { $numbers[$side].Value=8 }
    Await (Invoke $editor 'SaveAsync')
    Assert (!(Field $editor 'dirty')) 'Saving did not clear the pending image draft.'
    Invoke $editor 'SetScene' @('SceneSong') | Out-Null
    Await (Invoke $editor 'ImportFileAsync' @([string]$alternativePath))
    foreach($side in @('左','右','上','下')) { $numbers[$side].Value=12 }
    Await (Invoke $editor 'SaveAsync')
    $rules=$workspace.Rules
    $global=$rules.ResolveImage($entry.LogicalId,'SceneStore','','')
    $scoped=$rules.ResolveImage($entry.LogicalId,'SceneSong','','')
    Assert ($global.File -ne $scoped.File -and $scoped.Border.X -eq 12 -and $scoped.Mode -eq 'sliced') 'Reused image did not keep distinct global/scene files and slice borders.'
    Invoke $editor 'SetScene' @('SceneLobby') | Out-Null
    Await (Invoke $editor 'ImportFileAsync' @([string]$source)); $numbers['左'].Value=6; Await (Invoke $editor 'SaveAsync')
    $savedScopes=Field $editor 'savedScopes'
    Assert ($savedScopes.Items.Count -eq 4) 'The resource did not list global and both saved scene replacements.'
    $savedScopes.SelectedItem=@($savedScopes.Items | Where-Object { $_.Scene -eq 'SceneSong' })[0]
    Assert ((Field $editor 'loadedScene') -eq 'SceneSong' -and $numbers['左'].Value -eq 12) 'Switching a saved scene lost its image/slices.'
    Assert ($workspace.Rules.ResolveImage($entry.LogicalId,'SceneLobby','','').Border.X -eq 6) 'Adding another scene overwrote the first replacement.'
    $savedScopes.SelectedItem=@($savedScopes.Items | Where-Object { $_.Scene -eq 'SceneLobby' })[0]
    Invoke $editor 'RestoreScope' | Out-Null
    Assert ($workspace.Rules.ResolveImage($entry.LogicalId,'SceneSong','','').Border.X -eq 12) 'Restoring one scene removed a different scene replacement.'
    Invoke $editor 'SetScene' @('SceneSong') | Out-Null
    $hierarchy=Field $editor 'hierarchyBox'; $hierarchy.Text='Canvas/Panel/Target'; (Field $editor 'componentBox').SelectedItem='Image'
    Assert (!(Field $editor 'save').Enabled -and !(Field $editor 'import').Enabled) 'An uncommitted manual scope allowed edits.'
    Invoke $editor 'CommitScope' | Out-Null
    Await (Invoke $editor 'ImportFileAsync' @([string]$source))
    $numbers['左'].Value=4
    Await (Invoke $editor 'SaveAsync')
    Assert ($workspace.Rules.ResolveImage($entry.LogicalId,'SceneSong','Canvas/Panel/Target','Image').Border.X -eq 4) 'Per-component override priority failed.'
    Assert ($workspace.Rules.ResolveImage($entry.LogicalId,'SceneSong','Canvas/Other','Image').Border.X -eq 12) 'Component replacement leaked to another control.'
    $hierarchy.Text=''; (Field $editor 'componentBox').SelectedIndex=0; Invoke $editor 'CommitScope' | Out-Null
    Assert ($numbers['左'].Value -eq 12) 'Committing a scene scope did not reload its margins.'
    $hierarchy.Text='Canvas/Panel/Target'; (Field $editor 'componentBox').SelectedItem='Image'; Invoke $editor 'CommitScope' | Out-Null
    Assert ($numbers['左'].Value -eq 4 -and (Field $editor 'currentRuleFile') -eq $global.File) 'Reopening a manual exact-control scope kept another scope file or border.'
    Capture $form 'resources-control'
    $hierarchy.Text='Canvas/Other'; (Field $editor 'cancelScope').PerformClick()
    Assert ($hierarchy.Text -eq 'Canvas/Panel/Target' -and $numbers['左'].Value -eq 4) 'Cancelling scope input lost the loaded scope.'
    Invoke $editor 'RestoreScope' | Out-Null
    Assert ($workspace.Rules.ResolveImage($entry.LogicalId,'SceneSong','Canvas/Panel/Target','Image').Border.X -eq 12) 'Removing a component override did not restore scene inheritance.'
    $hierarchy.Text=''; (Field $editor 'componentBox').SelectedIndex=0
    # Text changes create a new scope draft. Reload the persisted scene before capture.
    Invoke $editor 'LoadSelection' | Out-Null
    $external=$shared.GetType('MalodyVThemeLoader.Shared.ThemeResources').GetMethod('Read').Invoke($null,@([string]$theme))
    $external.ResolveImage($entry.LogicalId,'SceneSong','','').Border.X=9
    $external.Write($theme)
    Invoke $form 'RefreshThemes' @([string]$key) | Out-Null
    Assert ($numbers['左'].Value -eq 9) 'A clean same-theme refresh retained stale resource rules.'
    $numbers['左'].Value=12; Await (Invoke $editor 'SaveAsync')
    [Windows.Forms.Application]::DoEvents()
    $surface=Field $editor 'preview'; $sourceBounds=Field $surface 'sourceBounds'
    $down=$surface.GetType().GetMethod('OnMouseDown',$flags); $move=$surface.GetType().GetMethod('OnMouseMove',$flags); $up=$surface.GetType().GetMethod('OnMouseUp',$flags)
    $x=[int]($sourceBounds.Left+12/$surface.ImageSize.Width*$sourceBounds.Width); $y=[int]($sourceBounds.Top+$sourceBounds.Height/2)
    $down.Invoke($surface,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,$x,$y,0))) | Out-Null
    $move.Invoke($surface,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,0,$x+5,$y,0))) | Out-Null
    $up.Invoke($surface,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,$x+5,$y,0))) | Out-Null
    Assert ($numbers['左'].Value -ne 12) 'Dragging a slice guide did not update the pixel margin.'
    Await (Invoke $editor 'SaveAsync')
    Capture $form 'resources-images'
    $form.Size=$form.MinimumSize; Capture $form 'resources-minimum'
    $form.ClientSize=[Drawing.Size]::new(1120,800)
    # Validation cannot overwrite the last working resource document.
    $before=(Get-FileHash (Join-Path $theme 'resources.json')).Hash
    $invalid=[Activator]::CreateInstance($shared.GetType('MalodyVThemeLoader.Shared.ThemeImageRule'))
    $invalid.LogicalId=$entry.LogicalId; $invalid.Border=[Activator]::CreateInstance($shared.GetType('MalodyVThemeLoader.Shared.Float4')); $invalid.Border.X=9999; $invalid.Mode='sliced'
    $rejected=$false; try{$workspace.SaveImage($invalid,$source)}catch{$rejected=$true}
    Assert ($rejected -and (Get-FileHash (Join-Path $theme 'resources.json')).Hash -eq $before) 'Invalid slicing overwrote the saved theme.'
    foreach($format in @('wav','ogg','mp3')) {
        $file=Join-Path $run ('silence.'+$format)
        & ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'anullsrc=r=22050:cl=stereo' -t 0.15 $file
        if ($LASTEXITCODE -ne 0) { throw 'Audio fixture generation failed.' }
        $decoded=$shared.GetType('MalodyVThemeLoader.Shared.Live2dAudio').GetMethod('Decode').Invoke($null,@([string]$file))
        Assert ($decoded.Samples.Length -gt 0 -and $decoded.Channels -eq 2) ($format+' audio decoding failed.')
    }
    $audio=$catalog.Audio | Where-Object Event -eq Click | Select-Object -First 1
    $tree.SelectedNode=Leaf $tree.Nodes $audio.LogicalId
    $tree.SelectedNode.EnsureVisible()
    Await (Invoke $editor 'ImportFileAsync' @([string](Join-Path $run 'silence.ogg')))
    Await (Invoke $editor 'SaveAsync')
    Assert ($workspace.Rules.ResolveAudio('Click','SceneSong').File.EndsWith('.ogg')) 'UI audio replacement was not saved.'
    Assert ($null -eq $workspace.Rules.ResolveAudio('Click','ScenePlay')) 'UI audio replacement entered gameplay.'
    Invoke $editor 'Audition' | Out-Null
    Assert ($null -ne (Field $editor 'player')) 'Audio audition was not prepared.'
    (Field $editor 'stop').PerformClick()
    Assert ($null -eq (Field $editor 'player')) 'Audio audition did not release playback.'
    Capture $form 'resources-audio'
    $form.Scale([Drawing.SizeF]::new(1.5,1.5)); $clamped=$form.ClientSize
    $form.ClientSize=[Drawing.Size]::new($clamped.Width-1,$clamped.Height-1); $form.ClientSize=$clamped
    Capture $form 'resources-scale150'
    $saved=$shared.GetType('MalodyVThemeLoader.Shared.ThemeResources').GetMethod('Read').Invoke($null,@([string]$theme))
    Assert ($saved.Images.Count -eq 2 -and $saved.Audio.Count -eq 1) 'Resource rule persistence failed.'
    $badAudio=[Activator]::CreateInstance($shared.GetType('MalodyVThemeLoader.Shared.ThemeAudioRule')); $badAudio.Event='HitSound'; $badAudio.File='../outside.ogg'; $saved.Audio.Add($badAudio)
    $rejected=$false; try{$saved.Validate()}catch{$rejected=$true}
    Assert $rejected 'An unsupported event/escaping path was accepted.'
    $settings=Get-Content (Join-Path $FixtureRoot 'ThemeLoader/settings.json') -Raw | ConvertFrom-Json
    Assert ($settings.realtimeMode -and (Get-Content (Join-Path $FixtureRoot 'ThemeLoader/active-theme.txt') -Raw).Trim() -eq $key) 'Save did not activate realtime and the edited theme.'
    # A legacy ZIP is materialized by the editor. Activation must refresh the
    # main library to the canonical directory instead of retaining the deleted ZIP.
    $legacyKey='Legacy-Edit-'+$Appearance
    $legacyDirectory=Join-Path $FixtureRoot ('ThemeLoader\themes\'+$legacyKey)
    if (Test-Path $legacyDirectory) {
        if (!( [IO.Path]::GetFullPath($legacyDirectory).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) -or
            ((Get-Item -LiteralPath $legacyDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe legacy test directory.' }
        Remove-Item -LiteralPath $legacyDirectory -Recurse -Force
    }
    $legacySource=Join-Path $run 'legacy-input'; New-Item -ItemType Directory -Path $legacySource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $legacySource 'theme.json'),('{"id":"'+$legacyKey+'","name":"Legacy edit fixture"}'))
    $legacyZip=Join-Path $FixtureRoot ('ThemeLoader\themes\'+$legacyKey+'.zip')
    $packageType=$shared.GetType('MalodyVThemeLoader.Shared.ThemePackage')
    $packageType.GetMethod('CreateFromDirectory').Invoke($null,@([string]$legacySource,[string]$legacyZip)) | Out-Null
    $legacyInfo=$packageType.GetMethod('Inspect').Invoke($null,@([string]$legacyZip))
    $legacyItem=[Activator]::CreateInstance($manager.GetType('MalodyVThemeLoader.ThemeManager.ThemeItem'),@([string]$legacyKey,[string]$legacyZip,[bool]$false,$legacyInfo))
    $choices=Field $form 'resourceThemes'; $choices.Items.Add($legacyItem) | Out-Null; $choices.SelectedItem=$legacyItem
    Assert (!(Test-Path $legacyZip) -and (Test-Path (Join-Path $legacyDirectory 'theme.json'))) 'Legacy theme was not materialized.'
    $tree.SelectedNode=Leaf $tree.Nodes $entry.LogicalId
    Await (Invoke $editor 'ImportFileAsync' @([string]$source)); Await (Invoke $editor 'SaveAsync')
    $selected=$form.GetType().GetProperty('SelectedTheme',$flags).GetValue($form)
    Assert ($selected.Key -eq $legacyKey -and $selected.IsLegacyDirectory -and $selected.SourcePath -eq $legacyDirectory) 'Activation retained an obsolete ZIP library item.'
    Write-Output 'PASS: source audio/use catalog, tree editing, global/scene/component overrides, sliced preview/dragging, atomic validation, OGG/WAV/MP3, audition/stop, persistence, inheritance restore and realtime activation.'
} finally { $form.Dispose() }
