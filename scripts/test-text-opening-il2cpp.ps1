param([string]$UnityExe='E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',[switch]$SkipPlayerBuild)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo 'artifacts\text-opening-il2cpp-probe'
$playerRoot=Join-Path $project 'player'; $build=Join-Path $project 'plugin-build'
New-Item -ItemType Directory -Force -Path $build,(Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') | Out-Null
if(!$SkipPlayerBuild){
    # Read-only copy of TMP's existing essentials into this disposable test project.
    Copy-Item -LiteralPath 'E:\UnityProjects\Emiria\Assets\TextMesh Pro' -Destination (Join-Path $project 'Assets') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\TextOpeningBuild.cs') -Destination (Join-Path $project 'Assets\Editor\ProbeBuild.cs') -Force
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f3`n")
    [IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.ugui":"1.0.0","com.unity.textmeshpro":"3.0.9","com.unity.modules.video":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.screencapture":"1.0.0"}}')
    [IO.File]::WriteAllText((Join-Path $project 'Assets\link.xml'),'<linker><assembly fullname="UnityEngine.UI" preserve="all"/><assembly fullname="Unity.TextMeshPro" preserve="all"/><assembly fullname="UnityEngine.CoreModule" preserve="all"/><assembly fullname="UnityEngine.VideoModule" preserve="all"/></linker>')
    $process=Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','TextOpeningBuild.Run','-logFile',('"'+$project+'\build.log"')) -WindowStyle Hidden -PassThru
    Write-Output "Isolated text/opening player build PID $($process.Id)."
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw "Text/opening player build failed: $project\build.log"}
}
$player=Join-Path $playerRoot 'TextOpeningProbe.exe'
if(!(Test-Path $player)){throw 'Build isolated text/opening player first.'}
Copy-Item -Path (Join-Path $repo 'vendor\bepinex\*') -Destination $playerRoot -Recurse -Force
$references=Get-ChildItem (Join-Path $repo 'generated\game-libs') -Filter '*.dll' | Where-Object Name -Match '^(UnityEngine|Unity.TextMeshPro|Il2Cpp)' | ForEach-Object {
    '<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'
}
$sources=@('SceneNodeTargets','SceneComponentQuery','TextAppearanceService','OpeningPresentation') | ForEach-Object {'<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $repo "src\MalodyVThemeLoader.Plugin\$_.cs"))+'"/>'}
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net6.0</TargetFramework><LangVersion>latest</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="BepInEx.Unity.IL2CPP" Version="6.0.0-be.785"/>'+($references -join '')+($sources -join '')+'<ProjectReference Include="'+[Security.SecurityElement]::Escape((Join-Path $repo 'src\MalodyVThemeLoader.Shared\MalodyVThemeLoader.Shared.csproj'))+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'Probe.csproj'),$xml)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\TextOpeningIl2CppProbe.cs') -Destination (Join-Path $build 'Probe.cs') -Force
dotnet build (Join-Path $build 'Probe.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false -v quiet
if($LASTEXITCODE -ne 0){throw 'Text/opening probe compile failed.'}
$plugin=Join-Path $playerRoot 'BepInEx\plugins\Probe'; New-Item -ItemType Directory -Force -Path $plugin | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'output\Probe.dll'),(Join-Path $build 'output\MalodyVThemeLoader.Shared.dll') -Destination $plugin -Force
$env:MALODY_TEXT_RESULT=Join-Path $project 'result.json'; $env:MALODY_TEXT_MEDIA=Join-Path $repo 'artifacts\inspector-media'
if(Test-Path $env:MALODY_TEXT_RESULT){Remove-Item -LiteralPath $env:MALODY_TEXT_RESULT}
Add-Type -Path (Join-Path $PSScriptRoot 'validation\HiddenDesktopPlayer.cs')
$exit=[HiddenDesktopPlayer]::Run($player,(Join-Path $project 'player.log'))
if(!(Test-Path $env:MALODY_TEXT_RESULT)){Get-Content (Join-Path $playerRoot 'BepInEx\LogOutput.log') -Tail 35;throw "Text/opening runtime probe did not complete ($exit)."}
$result=Get-Content $env:MALODY_TEXT_RESULT -Raw | ConvertFrom-Json
if(!$result.passed -or $exit -ne 0){throw $result.error}
Get-Content $env:MALODY_TEXT_RESULT
