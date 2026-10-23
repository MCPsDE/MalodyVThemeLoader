param(
    [string]$UnityExe = 'E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [string]$SourceRoot = 'E:\UnityProjects\Emiria'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!(Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity Editor not found: $UnityExe" }
$project = Join-Path $repo ('artifacts\entrance-icon-test-' + [Guid]::NewGuid().ToString('N'))
$assets = Join-Path $project 'Assets'
New-Item -ItemType Directory -Path (Join-Path $assets 'Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'), "m_EditorVersion: 2022.3.62f3`n")
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'), '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.ugui":"1.0.0"}}')

$sourceAnimation = Join-Path $SourceRoot 'Assets\Malody\Animation\Widget'
foreach ($name in @('entranceIcon.controller','entranceIcon.controller.meta','entranceIconEnter.anim','entranceIconEnter.anim.meta','entranceIconLeave.anim','entranceIconLeave.anim.meta')) {
    Copy-Item -LiteralPath (Join-Path $sourceAnimation $name) -Destination (Join-Path $assets $name)
}
$slideController = [IO.File]::ReadAllText((Join-Path $repo 'src\MalodyVThemeLoader.Plugin\EntranceIconSlideController.cs'))
$slideController = "#nullable enable`nusing System;`n" + $slideController.Replace('namespace MalodyVThemeLoader.Plugin;', 'namespace MalodyVThemeLoader.Plugin {') + "`n}"
[IO.File]::WriteAllText((Join-Path $assets 'Editor\EntranceIconSlideController.cs'), $slideController)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\EntranceIconMotionValidation.cs') -Destination (Join-Path $assets 'Editor\EntranceIconMotionValidation.cs')
$env:MALODY_ENTRANCE_TEST_RESULT = Join-Path $project 'result.txt'
$compileLog = Join-Path $project 'compile.log'
$log = Join-Path $project 'unity.log'
function Invoke-Unity([string]$LogPath, [switch]$ExecuteValidation) {
    $arguments = @('-batchmode','-quit','-projectPath',('"' + $project + '"'))
    if ($ExecuteValidation) { $arguments += @('-executeMethod','EntranceIconMotionValidation.Run') }
    $arguments += @('-logFile',('"' + $LogPath + '"'))
    $editor = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $editor.WaitForExit()
    $editor.Refresh()
    return $editor.ExitCode
}
$compileExitCode = Invoke-Unity $compileLog
if ($compileExitCode -ne 0) {
    if (Test-Path -LiteralPath $compileLog) { Get-Content -LiteralPath $compileLog -Tail 60 }
    throw 'Unity fixture import or script compilation failed.'
}
$unityExitCode = Invoke-Unity $log -ExecuteValidation
if ($unityExitCode -ne 0 -or !(Test-Path -LiteralPath $env:MALODY_ENTRANCE_TEST_RESULT)) {
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 60 }
    throw 'EntranceIcon motion validation failed.'
}
Get-Content -LiteralPath $env:MALODY_ENTRANCE_TEST_RESULT
Write-Output "Unity fixture and log: $project"
Remove-Item Env:MALODY_ENTRANCE_TEST_RESULT -ErrorAction SilentlyContinue
