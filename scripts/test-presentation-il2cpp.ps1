param([string]$UnityExe='E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',[switch]$SkipPlayerBuild,[switch]$Baseline)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo 'artifacts\presentation-il2cpp-test'
$playerRoot=Join-Path $project 'player'
$build=Join-Path $project 'plugin-build'
New-Item -ItemType Directory -Force -Path $build,(Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') | Out-Null
if(!$SkipPlayerBuild){
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\PresentationIl2CppEntry.cs') -Destination (Join-Path $project 'Assets\Editor\PresentationBuild.cs') -Force
    foreach($name in @('entranceIcon.controller','entranceIcon.controller.meta','entranceIconEnter.anim','entranceIconEnter.anim.meta','entranceIconLeave.anim','entranceIconLeave.anim.meta')) {
        Copy-Item -LiteralPath (Join-Path 'E:\UnityProjects\Emiria\Assets\Malody\Animation\Widget' $name) -Destination (Join-Path $project 'Assets') -Force
    }
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f3`n")
    [IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.ugui":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.ui":"1.0.0"}}')
    [IO.File]::WriteAllText((Join-Path $project 'Assets\link.xml'),'<linker><assembly fullname="UnityEngine.UI" preserve="all"/><assembly fullname="UnityEngine.CoreModule" preserve="all"/><assembly fullname="UnityEngine.AudioModule" preserve="all"/><assembly fullname="UnityEngine.AnimationModule" preserve="all"/></linker>')
    $process=Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','PresentationBuild.Run','-logFile',('"'+$project+'\build.log"')) -WindowStyle Hidden -PassThru
    Write-Output "Isolated presentation player build PID $($process.Id)."
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw "Presentation player build failed: $project\build.log"}
}
$player=Join-Path $playerRoot 'PresentationProbe.exe'
if(!(Test-Path -LiteralPath $player)){throw 'Build the isolated presentation player first.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\fixtures\silent-voice.ogg') -Destination (Join-Path $playerRoot 'voice.ogg') -Force
Copy-Item -Path (Join-Path $repo 'vendor\bepinex\*') -Destination $playerRoot -Recurse -Force
$references=Get-ChildItem (Join-Path $repo 'generated\game-libs') -Filter '*.dll' | Where-Object Name -Match '^(UnityEngine|Il2Cpp)' | ForEach-Object {
    '<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'
}
$controllerSource=Join-Path $repo 'src\MalodyVThemeLoader.Plugin\EntranceIconSlideController.cs'
if($Baseline){
    $controllerSource=Join-Path $build 'EntranceIconBaseline.source'
    $oldSource=git -C $repo show v2.6.6:src/MalodyVThemeLoader.Plugin/EntranceIconSlideController.cs
    if($LASTEXITCODE -ne 0){throw 'Cannot read the released baseline controller.'}
    [IO.File]::WriteAllText($controllerSource,($oldSource -join "`n"))
}
$sources='<Compile Include="'+[Security.SecurityElement]::Escape($controllerSource)+'"/>'
$sources+='<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'validation\MutedVoiceOutput.cs'))+'"/>'
foreach($name in @('UnityAudioClipFactory','AnimationAudioPlayer','BuiltInOverlayVisibility')) {
    $sources+='<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $repo "src\MalodyVThemeLoader.Plugin\$name.cs"))+'"/>'
}
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net6.0</TargetFramework><LangVersion>latest</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies></PropertyGroup><ItemGroup><PackageReference Include="BepInEx.Unity.IL2CPP" Version="6.0.0-be.785"/><PackageReference Include="NAudio.WinMM" Version="2.2.1"/>'+($references -join '')+$sources+'<ProjectReference Include="'+[Security.SecurityElement]::Escape((Join-Path $repo 'src\MalodyVThemeLoader.Shared\MalodyVThemeLoader.Shared.csproj'))+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'Probe.csproj'),$xml)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\PresentationIl2CppProbe.cs') -Destination (Join-Path $build 'Probe.cs') -Force
dotnet build (Join-Path $build 'Probe.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false -v quiet
if($LASTEXITCODE -ne 0){throw 'Presentation probe compile failed.'}
$plugin=Join-Path $playerRoot 'BepInEx\plugins\Probe'; New-Item -ItemType Directory -Force -Path $plugin | Out-Null
foreach($name in @('Probe.dll','MalodyVThemeLoader.Shared.dll','NVorbis.dll','NLayer.dll','NAudio.Core.dll','NAudio.WinMM.dll')) {
    Copy-Item -LiteralPath (Join-Path $build ('output\'+$name)) -Destination $plugin -Force
}
$env:MALODY_PRESENTATION_RESULT=Join-Path $project 'result.json'
$env:MALODY_PRESENTATION_BASELINE=if($Baseline){'1'}else{'0'}
if(Test-Path $env:MALODY_PRESENTATION_RESULT){Remove-Item -LiteralPath $env:MALODY_PRESENTATION_RESULT}
Add-Type -Path (Join-Path $PSScriptRoot 'validation\HiddenDesktopPlayer.cs')
$exit=[HiddenDesktopPlayer]::Run($player,(Join-Path $project 'player.log'))
if($exit -ne 0 -or !(Test-Path $env:MALODY_PRESENTATION_RESULT)){Get-Content (Join-Path $playerRoot 'BepInEx\LogOutput.log') -Tail 30;throw 'Presentation runtime probe failed.'}
$result=Get-Content $env:MALODY_PRESENTATION_RESULT -Raw | ConvertFrom-Json
if(!$result.passed){throw $result.error}
Get-Content $env:MALODY_PRESENTATION_RESULT
