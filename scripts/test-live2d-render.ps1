param(
    [string]$UnityExe = 'E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [string]$SdkRoot = 'E:\UnityProjects\Emiria\Assets\Live2D\Cubism',
    [string]$SampleDirectory = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\ThemeLoader\themes\Murasame\live2d\models'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repo 'artifacts\live2d-render-test'
New-Item -ItemType Directory -Path (Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'), "m_EditorVersion: 2022.3.62f3`n")
if (Test-Path (Join-Path $project 'result.json')) { Remove-Item -LiteralPath (Join-Path $project 'result.json') }
if (!(Test-Path (Join-Path $project 'Assets\Cubism'))) { Copy-Item -LiteralPath $SdkRoot -Destination (Join-Path $project 'Assets\Cubism') -Recurse }
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'), '{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.ugui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0"}}')
foreach ($name in @('Cubism67Api','CubismVisualFactory')) {
    $code = [IO.File]::ReadAllText((Join-Path $repo "src\MalodyVThemeLoader.Plugin\$name.cs"))
    $code = '#define THEMELOADER_EDITOR_TEST' + "`n" + $code.Replace('namespace MalodyVThemeLoader.Plugin;', 'namespace MalodyVThemeLoader.Plugin {') + "`n}"
    [IO.File]::WriteAllText((Join-Path $project "Assets\$name.cs"), $code)
}
[IO.File]::WriteAllText((Join-Path $project 'Assets\ValidationVisibility.cs'), '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]')
$env:MALODY_LIVE2D_RENDER_SAMPLE = [IO.Path]::GetFullPath($SampleDirectory)
$env:MALODY_LIVE2D_RENDER_OUTPUT = $project
$shared = [Reflection.Assembly]::LoadFrom((Join-Path $repo 'src\MalodyVThemeLoader.Shared\bin\Release\net6.0\MalodyVThemeLoader.Shared.dll'))
$documentType = $shared.GetType('MalodyVThemeLoader.Shared.Live2dModelDocument')
$motionType = $shared.GetType('MalodyVThemeLoader.Shared.CubismMotion')
$modelFile = Get-ChildItem -LiteralPath $SampleDirectory -Filter '*.model3.json' | Select-Object -First 1
$document = $documentType.GetMethod('Read').Invoke($null, @([string]$SampleDirectory, [string]$modelFile.Name))
$poses = foreach ($reference in $document.Motions | Select-Object -First 2) {
    $motion = $motionType.GetMethod('Read').Invoke($null, @([string](Join-Path $SampleDirectory $reference.File)))
    @{ values = @($motion.Curves | ForEach-Object { @{ target = $_.Target; id = $_.Id; value = $_.Evaluate([single]1) } }) }
}
@{ poses = @($poses) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $project 'poses.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\Live2dRenderValidation.cs') -Destination (Join-Path $project 'Assets\Editor\Live2dRenderValidation.cs') -Force
$taskArguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'Live2dRenderValidation.Run', '-logFile', ('"' + (Join-Path $project 'unity.log') + '"'))
$process = Start-Process -FilePath $UnityExe -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
Write-Output "Unity model validation PID $($process.Id); log: $project\unity.log"
$process.WaitForExit()
if ($process.ExitCode -ne 0) { Get-Content (Join-Path $project 'unity.log') -Tail 45; throw 'Unity Live2D render validation failed.' }
$result = Join-Path $project 'result.json'
if (!(Test-Path $result)) { throw 'Unity produced no validation result.' }
Get-Content $result
