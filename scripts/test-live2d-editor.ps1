param(
    [string]$GameRoot = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV',
    [string]$SampleDirectory = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\ThemeLoader\themes\Murasame\live2d\models',
    [ValidateSet('dark','light')][string]$Appearance = 'dark',
    [string]$ManagerDll = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:MALODY_THEMELOADER_DISABLE_UPDATES = '1'
$env:MALODY_THEMELOADER_PREFERENCES = Join-Path $repo 'artifacts\editor-preferences'
$runRoot = Join-Path $repo ('artifacts\editor-test-' + $Appearance)
$modelRoot = Join-Path $runRoot 'ThemeLoader\themes\Murasame\live2d\models'
New-Item -ItemType Directory -Path $modelRoot -Force | Out-Null
foreach ($entry in Get-ChildItem -LiteralPath $SampleDirectory) { Copy-Item -LiteralPath $entry.FullName -Destination $modelRoot -Force -Recurse }
$nativeRoot = Join-Path $runRoot 'Malody V_Data\Plugins\x86_64'
New-Item -ItemType Directory -Path $nativeRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $GameRoot 'Malody V_Data\Plugins\x86_64\Live2DCubismCore.dll') -Destination $nativeRoot -Force
$root = Split-Path $modelRoot
$out = Join-Path $repo ('.impeccable\review\editor-' + $Appearance)
New-Item -ItemType Directory -Path $out -Force | Out-Null
if (!$ManagerDll) { $ManagerDll = Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.ThemeManager.dll' }
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ManagerDll))
$shared = [Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetDirectoryName($assembly.Location)) 'MalodyVThemeLoader.Shared.dll'))
$windows = [Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetDirectoryName($assembly.Location)) 'MalodyVThemeLoader.Windows.dll'))
$windows.GetType('MalodyVThemeLoader.Windows.AppAppearance').GetMethod('SetDarkMode').Invoke($null,@([bool]($Appearance -eq 'dark'),[bool]$false))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Field($object, $name) { $object.GetType().GetField($name, $flags).GetValue($object) }
function Invoke($object, $name, [object[]]$arguments = @()) { $object.GetType().GetMethod($name, $flags).Invoke($object, $arguments) | Out-Null }
function Assert($value, $message) { if (!$value) { throw $message } }
function Capture($form, $name) {
    [Windows.Forms.Application]::DoEvents()
    $bitmap = [Drawing.Bitmap]::new($form.Width,$form.Height)
    try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $out ($name + '.png'))) }
    finally { $bitmap.Dispose() }
}
$bindingType = $shared.GetType('MalodyVThemeLoader.Shared.Live2dBinding')
$binding = [Activator]::CreateInstance($bindingType)
$binding.Model = 'models/Murasame.model3.json'
$document = $shared.GetType('MalodyVThemeLoader.Shared.Live2dModelDocument').GetMethod('Read').Invoke($null,@([string]$root,[string]$binding.Model))
$editorType = $assembly.GetType('MalodyVThemeLoader.ThemeManager.Live2dAnimationEditor')
$editor = $editorType.GetConstructors()[0].Invoke(@([string]$runRoot,[string]$root,$binding,$document.Motions))
try {
    $editor.Show(); [Windows.Forms.Application]::DoEvents()
    $preview = Field $editor 'preview'
    $preview.GetType().GetMethod('RenderFrame').Invoke($preview,@()) | Out-Null
    $rules = Field $editor 'rules'; $assigned = Field $editor 'assignedMotion'
    Assert ($rules.Items.Count -eq 7 -and $assigned.SelectedIndex -eq 0) 'An automatic region/motion assignment was introduced.'
    $rules.SelectedIndex = 0; $assigned.SelectedIndex = 1
    $rules.SelectedIndex = 1; $assigned.SelectedIndex = 2
    for ($i=0; $i -lt 5; $i++) { $rules.SelectedIndex = $i + 2; $assigned.SelectedIndex = $i + 3 }
    Assert ($binding.ResultMotions.Count -eq 5) 'M1-M5 assignment failed.'
    $preview.GetType().GetMethod('BeginRegion').Invoke($preview,@()) | Out-Null
    $bounds = Field $preview 'modelRectangle'
    $x0 = [int]($bounds.X + $bounds.Width * .25); $y0 = [int]($bounds.Y + $bounds.Height * .08)
    $x1 = [int]($bounds.X + $bounds.Width * .75); $y1 = [int]($bounds.Y + $bounds.Height * .28)
    Invoke $preview 'OnMouseDown' @([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,$x0,$y0,0))
    Invoke $preview 'OnMouseMove' @([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,0,$x1,$y1,0))
    Invoke $preview 'OnMouseUp' @([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,$x1,$y1,0))
    Assert ($binding.ClickRegions.Count -eq 1) 'Drawing a region did not create it.'
    (Field $editor 'regionName').Text = '头部'
    $assigned.SelectedIndex = 5
    $wave = Join-Path $runRoot '测试声音.wav'; $ogg = Join-Path $runRoot '测试声音.ogg'
    & ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'anullsrc=r=44100:cl=mono' -t 0.25 -c:a pcm_s16le $wave
    if ($LASTEXITCODE -ne 0) { throw 'WAV fixture creation failed.' }
    & ffmpeg -hide_banner -loglevel error -y -i $wave -c:a libvorbis $ogg
    if ($LASTEXITCODE -ne 0) { throw 'OGG fixture creation failed.' }
    $audioType = $shared.GetType('MalodyVThemeLoader.Shared.Live2dAudio')
    $workspace = $assembly.GetType('MalodyVThemeLoader.ThemeManager.Live2dWorkspace')
    foreach($file in @($wave,$ogg)) {
        $audio = $audioType.GetMethod('Decode').Invoke($null,@([string]$file))
        Assert ($audio.Channels -eq 1 -and $audio.SampleRate -eq 44100 -and $audio.Samples.Length -ge 11000) 'Audio decoding changed channels/rate/duration.'
        $importedAudio = $workspace.GetMethod('ImportAudio').Invoke($null,@([string]$root,[string]$file))
        $motionFile = if ($file.EndsWith('.ogg')) { $binding.ClickRegions[0].Motion } else { $binding.IdleMotion }
        $binding.MotionAudio[$motionFile] = $importedAudio
    }
    Invoke $editor 'RefreshAudio'
    Assert ((Field $editor 'audioRemove').Enabled) 'The bound animation audio is not removable.'
    $binding.Validate()
    $events = $shared.GetType('MalodyVThemeLoader.Shared.Live2dEvents')
    $hit = $events.GetMethod('ClickMotion').Invoke($null,@($binding,[single]0.5,[single]0.15))
    if ($hit -ne $binding.ClickRegions[0].Motion) { $binding.ClickRegions[0] | Format-List; "Hit: $hit"; "Model bounds: $bounds" }
    Assert ($hit -eq $binding.ClickRegions[0].Motion) 'Normalized region click did not select the assigned motion.'
    $miss = $events.GetMethod('ClickMotion').Invoke($null,@($binding,[single]0.05,[single]0.9))
    Assert ($miss -eq $binding.TapMotion) 'An unbound click did not use the fallback animation.'
    $overlap = [Activator]::CreateInstance($shared.GetType('MalodyVThemeLoader.Shared.Live2dClickRegion'))
    $overlap.Name = '重叠区域'; $overlap.X=.4; $overlap.Y=.1; $overlap.Width=.2; $overlap.Height=.1; $overlap.Motion=$binding.IdleMotion
    $binding.ClickRegions.Add($overlap)
    Assert ($events.GetMethod('ClickMotion').Invoke($null,@($binding,[single]0.5,[single]0.15)) -eq $binding.IdleMotion) 'Last-drawn region priority failed.'
    $binding.ClickRegions.RemoveAt(1)
    for($rank=1; $rank -le 5; $rank++) {
        Assert ($events.GetMethod('ResultFromSprite').Invoke($null,@('rank_score_2'+$rank)) -eq ('M'+(6-$rank))) 'Displayed result rank mapping is wrong.'
    }
    foreach($invalid in @('rank_score_bg_2','rank_score_20','rank_score_211','rank_score_91','not-a-result')) {
        Assert ($events.GetMethod('ResultFromSprite').Invoke($null,@($invalid)) -eq '') 'Non-result artwork triggered a result event.'
    }
    $options = $shared.GetType('MalodyVThemeLoader.Shared.JsonDefaults').GetField('Options').GetValue($null)
    $json = [Text.Json.JsonSerializer]::Serialize($binding,$bindingType,$options)
    [IO.File]::WriteAllText((Join-Path $root 'replacement.json'),$json)
    $roundTrip = [Text.Json.JsonSerializer]::Deserialize($json,$bindingType,$options)
    Assert ($roundTrip.ClickRegions[0].Name -eq '头部' -and $roundTrip.ResultMotions.Count -eq 5 -and $roundTrip.MotionAudio.Count -eq 2) 'Region/result/audio binding persistence failed.'
    $document.GetType().GetMethod('ValidateBinding').Invoke($document,@([string]$root,$roundTrip)) | Out-Null
    $before = Get-FileHash (Join-Path $root 'replacement.json')
    $roundTrip.ClickRegions[0].Width = 2
    $rejected=$false; try{$roundTrip.Validate()}catch{$rejected=$true}
    Assert $rejected 'An out-of-bounds click region was accepted.'
    Assert ((Get-FileHash (Join-Path $root 'replacement.json')).Hash -eq $before.Hash) 'Validation changed the saved binding.'
    $native = Field $preview 'model'
    $renderMethod = $native.GetType().GetMethod('Render')
    $motionType = $shared.GetType('MalodyVThemeLoader.Shared.CubismMotion')
    $hashes=@(); $watch=[Diagnostics.Stopwatch]::StartNew()
    foreach($motionReference in $document.Motions | Select-Object -First 2) {
        $motion = $motionType.GetMethod('Read').Invoke($null,@([string](Join-Path $root $motionReference.File)))
        $native.GetType().GetMethod('Update').Invoke($native,@($motion,[single]1)) | Out-Null
        $renderArgs=@([int]420,[int]560,[Drawing.Color]::FromArgb(24,24,24),[Drawing.RectangleF]::Empty)
        $frame = $renderMethod.Invoke($native,$renderArgs)
        try { $framePath=Join-Path $runRoot ($motionReference.Group + '.png'); $frame.Save($framePath); $hashes += (Get-FileHash $framePath).Hash }
        finally {$frame.Dispose()}
    }
    $watch.Stop(); Assert ($hashes[0] -ne $hashes[1]) 'Real moc motion preview frames did not change.'
    $preview.GetType().GetMethod('Play').Invoke($preview,@([string]'',[bool]$false)) | Out-Null
    $rules.SelectedIndex = 7
    Capture $editor 'editor-desktop'
    $rules.SelectedIndex = 4
    Capture $editor 'editor-result'
    $editor.Size=$editor.MinimumSize
    Capture $editor 'editor-minimum'
    $editor.ClientSize=[Drawing.Size]::new(1140,760)
    $editor.Scale([Drawing.SizeF]::new(1.5,1.5)); $clamped=$editor.ClientSize
    $editor.ClientSize=[Drawing.Size]::new($clamped.Width-1,$clamped.Height-1); $editor.ClientSize=$clamped
    Capture $editor 'editor-scale150'
    $gpuActive = $native.IsGpu
    $preview.GetType().GetMethod('Play').Invoke($preview,@([string]$binding.ClickRegions[0].Motion,[bool]$false)) | Out-Null
    Assert ($null -ne (Field $preview 'sound')) 'OGG audio was not prepared for preview playback.'
    $preview.GetType().GetMethod('Play').Invoke($preview,@([string]$binding.TapMotion,[bool]$false)) | Out-Null
    Assert ($null -eq (Field $preview 'sound')) 'Previous preview audio survived the next animation.'
    Invoke $editor 'Complete'
    Assert ($editor.DialogResult -eq [Windows.Forms.DialogResult]::OK) 'Valid bindings could not be completed.'
    Write-Output "PASS: native model/motion preview (GPU=$gpuActive, $($watch.ElapsedMilliseconds)ms for two 420x560 frames), manual drawing, overlap/fallback, five result bindings, OGG/WAV decode/import/playback preparation/stop, persistence and invalid-region rejection."
} finally { $editor.Dispose() }
