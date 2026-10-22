param(
    [string]$SampleDirectory = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\ThemeLoader\themes\Murasame\live2d\models',
    [string]$ManagerDll = ''
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $repo 'artifacts\live2d-test'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
if (!$ManagerDll) { $ManagerDll = Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.ThemeManager.dll' }
$assembly = [Reflection.Assembly]::LoadFrom($ManagerDll)
$shared = [Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetDirectoryName($ManagerDll)) 'MalodyVThemeLoader.Shared.dll'))
$workspace = $assembly.GetType('MalodyVThemeLoader.ThemeManager.Live2dWorkspace', $true)
$libraryType = $assembly.GetType('MalodyVThemeLoader.ThemeManager.ThemeLibrary', $true)
$bindingType = $shared.GetType('MalodyVThemeLoader.Shared.Live2dBinding', $true)
$documentType = $shared.GetType('MalodyVThemeLoader.Shared.Live2dModelDocument', $true)
$motionType = $shared.GetType('MalodyVThemeLoader.Shared.CubismMotion', $true)
$curveType = $shared.GetType('MalodyVThemeLoader.Shared.CubismMotionCurve', $true)
function Assert($value, $message) { if (!$value) { throw $message } }
function Call($name, [object[]]$arguments) {
    $taskInvokeArgs = [object[]]::new($arguments.Length)
    for ($i = 0; $i -lt $arguments.Length; $i++) { $taskInvokeArgs[$i] = $arguments[$i].PSObject.BaseObject }
    $workspace.GetMethod($name).Invoke($null, $taskInvokeArgs)
}

# Every run uses a fresh fake game. The supplied sample is read only.
$runRoot = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
$theme = Join-Path $runRoot 'ThemeLoader\themes\Murasame'
$modelRoot = Join-Path $theme 'characters\models'
New-Item -ItemType Directory -Path $modelRoot -Force | Out-Null
foreach ($entry in Get-ChildItem -LiteralPath $SampleDirectory) { Copy-Item -LiteralPath $entry.FullName -Destination $modelRoot -Recurse -Force }
[IO.File]::WriteAllText((Join-Path $theme 'theme.json'), '{"formatVersion":1,"id":"murasame","name":"Murasame","live2dRoot":"characters"}')
$library = $libraryType.GetConstructor([type[]]@([string])).Invoke([object[]]@([string]$runRoot))
$item = @($libraryType.GetMethod('GetThemes').Invoke($library, @())) | Where-Object { $_.Key -eq 'Murasame' } | Select-Object -First 1
$item = $item.PSObject.BaseObject
$models = @(Call 'GetModels' @($item))
Assert ($models -contains 'models/Murasame.model3.json') 'Recursive model discovery failed.'
$doc = $documentType.GetMethod('Read').Invoke($null, @([string](Join-Path $theme 'characters'), 'models/Murasame.model3.json'))
Assert ($doc.Motions.Count -eq 12 -and $doc.Textures.Count -eq 1) 'Sample dependencies/mixed path separators were not resolved.'
foreach ($reference in $doc.Motions) {
    $motion = $motionType.GetMethod('Read').Invoke($null, @([string](Join-Path $theme ('characters/' + $reference.File))))
    foreach ($curve in $motion.Curves) {
        foreach ($time in @(0.0, 0.2, ($motion.Duration / 2), $motion.Duration)) {
            $value = $curve.Evaluate([single]$time)
            Assert ([single]::IsFinite($value)) 'Sample curve produced a non-finite value.'
        }
    }
}
$binding = [Activator]::CreateInstance($bindingType)
$binding.Model = 'models/Murasame.model3.json'; $binding.IdleMotion = $doc.Motions[0].File; $binding.TapMotion = $doc.Motions[1].File
Call 'SaveBinding' @($item, $binding)
$saved = Call 'GetBinding' @($item)
Assert ($saved.IdleMotion -eq $binding.IdleMotion -and $saved.TapMotion -eq $binding.TapMotion) 'Animation assignment was lost.'
$before = Get-FileHash (Join-Path $theme 'characters/replacement.json')
$binding.Model = '../escape.model3.json'
$rejected = $false
try { Call 'SaveBinding' @($item, $binding) } catch { $rejected = $true }
Assert $rejected 'Escaping model path accepted.'
Assert ((Get-FileHash (Join-Path $theme 'characters/replacement.json')).Hash -eq $before.Hash) 'Failed binding replaced the valid configuration.'

# Known answers for all four Cubism segment kinds, including non-uniform
# Bezier time handles and both stepped boundaries.
foreach ($case in @(
    @{ Data = [single[]]@(0,0,0,1,10); Time = .25; Expected = 2.5 },
    @{ Data = [single[]]@(0,2,2,1,8); Time = .5; Expected = 2 },
    @{ Data = [single[]]@(0,2,3,1,8); Time = .5; Expected = 8 },
    @{ Data = [single[]]@(0,0,1,.1,0,.9,1,1,1); Time = .5; Expected = .5 }
)) {
    $curve = [Activator]::CreateInstance($curveType, [object[]]@('Parameter','test',$case.Data))
    Assert ([Math]::Abs($curve.Evaluate([single]$case.Time) - $case.Expected) -lt .0001) 'Curve interpolation regression.'
}

$newMotion = Join-Path $runRoot '新动作.motion3.json'
Copy-Item -LiteralPath (Join-Path $modelRoot 'motion/motion02.motion3.json') -Destination $newMotion
Call 'ImportAnimations' @($item, 'models/Murasame.model3.json', [string[]]@($newMotion))
$choices = @(Call 'GetMotionChoices' @($item, 'models/Murasame.model3.json'))
Assert ($choices.Count -eq 13) 'Imported motion is absent from assignment choices.'
Call 'RemoveBinding' @($item)
Assert (!(Test-Path (Join-Path $theme 'characters/replacement.json'))) 'Restore did not remove binding.'

$importSource = Join-Path $runRoot '外部模型'
Copy-Item -LiteralPath $modelRoot -Destination $importSource -Recurse
Call 'ImportDirectory' @($item, $importSource) | Out-Null
Call 'ImportDirectory' @($item, $importSource) | Out-Null
$imported = Join-Path $theme 'characters/models/Murasame/Murasame.model3.json'
Assert (Test-Path $imported) 'Model import lost its stable folder.'
$hash = (Get-FileHash $imported).Hash
Remove-Item -LiteralPath (Join-Path $importSource 'Murasame.moc3')
$rejected = $false
try { Call 'ImportDirectory' @($item, $importSource) } catch { $rejected = $true }
Assert $rejected 'Incomplete model was accepted.'
Assert ((Get-FileHash $imported).Hash -eq $hash) 'Failed import damaged the old model.'

# Exercise the real form's Apply/Restore commands against the disposable game.
$env:MALODY_THEMELOADER_DISABLE_UPDATES = '1'
$env:MALODY_THEMELOADER_PREFERENCES = Join-Path $runRoot 'preferences'
Add-Type -AssemblyName System.Windows.Forms
$formType = $assembly.GetType('MalodyVThemeLoader.ThemeManager.ThemeManagerForm', $true)
$form = $formType.GetConstructor([type[]]@([string])).Invoke(@([string]$runRoot))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
try {
    $formType.GetMethod('RefreshThemes', $flags).Invoke($form, @('Murasame')) | Out-Null
    $formType.GetMethod('ApplyLive2dModel', $flags).Invoke($form, @()) | Out-Null
    Assert ((Get-Content (Join-Path $runRoot 'ThemeLoader/active-theme.txt') -Raw).Trim() -eq 'Murasame') 'Apply did not activate the theme.'
    $settings = Get-Content (Join-Path $runRoot 'ThemeLoader/settings.json') -Raw | ConvertFrom-Json
    Assert $settings.realtimeMode 'Apply did not enable realtime mode.'
    $saved = Call 'GetBinding' @($item)
    Assert ($saved.IdleMotion.EndsWith('motion01.motion3.json')) 'The default idle motion was not recognized.'
    Assert ($saved.TapMotion.EndsWith('motion02.motion3.json')) 'The default tap motion was not assigned.'
    $statusLabel = $formType.GetField('live2dState', $flags).GetValue($form)
    Assert ($statusLabel.Text.Contains('2.3.0')) 'Old plugin installation did not show an update instruction.'
    $fakePlugin = Join-Path $runRoot 'BepInEx/plugins/MalodyVThemeLoader/MalodyVThemeLoader.Plugin.dll'
    New-Item -ItemType Directory -Path (Split-Path $fakePlugin) -Force | Out-Null
    # Only version metadata is needed for this fixture, not executable plugin behavior.
    Copy-Item -LiteralPath $shared.Location -Destination $fakePlugin
    $statusPath = Join-Path $runRoot 'ThemeLoader/live2d-status.json'
    @{ pluginVersion = '2.3.0'; theme = 'murasame'; model = $saved.Model; state = 'applied'; message = '模型已在游戏中显示。'; updatedAt = [DateTimeOffset]::UtcNow } |
        ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding utf8
    $formType.GetMethod('RefreshLive2dStatus', $flags).Invoke($form, @()) | Out-Null
    Assert ($statusLabel.Text -eq '模型已在游戏中显示。') 'Runtime success was not displayed.'
    $formType.GetMethod('RestoreLive2dModel', $flags).Invoke($form, @()) | Out-Null
    Assert (!(Test-Path (Join-Path $theme 'characters/replacement.json'))) 'Form restore left a model binding behind.'
} finally { $form.Dispose() }
Write-Output 'PASS: Murasame dependencies, all 12 motions, interpolation, binding rollback, custom live2dRoot, Unicode animation import, repeat model overwrite and restore.'
Write-Output 'PASS: Form Apply activates theme/realtime, assigns idle/tap, distinguishes plugin upgrade from runtime success, and Restore removes the binding.'
