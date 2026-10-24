param([string]$UnityExe='E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',[switch]$SkipPlayerBuild)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo 'artifacts\inspector-il2cpp-probe'
$playerRoot=Join-Path $project 'player'
$player=Join-Path $playerRoot 'InspectorProbe.exe'
$build=Join-Path $project 'plugin-build'
New-Item -ItemType Directory -Force -Path $build,(Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') | Out-Null
if(!$SkipPlayerBuild){
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\InspectorIl2CppEntry.cs') -Destination (Join-Path $project 'Assets\Editor\ProbeBuild.cs') -Force
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f3`n")
    [IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.ugui":"1.0.0","com.unity.modules.video":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.ui":"1.0.0"}}')
    [IO.File]::WriteAllText((Join-Path $project 'Assets\link.xml'),'<linker><assembly fullname="UnityEngine.UI" preserve="all"/><assembly fullname="UnityEngine.CoreModule" preserve="all"/><assembly fullname="UnityEngine.VideoModule" preserve="all"/></linker>')
    $process=Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','ProbeBuild.Run','-logFile',('"'+$project+'\build.log"')) -WindowStyle Hidden -PassThru
    Write-Output "Isolated IL2CPP player build PID $($process.Id)."
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw "IL2CPP test player build failed; see $project\build.log"}
}
if(!(Test-Path -LiteralPath $player)){throw 'Build the isolated IL2CPP player first.'}
Copy-Item -Path (Join-Path $repo 'vendor\bepinex\*') -Destination $playerRoot -Recurse -Force
$references=Get-ChildItem (Join-Path $repo 'generated\game-libs') -Filter '*.dll' | Where-Object Name -Match '^(UnityEngine|Il2Cpp)' | ForEach-Object {
    '<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'
}
$sources=@('InspectorInputFence','SceneNodeLocator','SceneAddressIndex','NodeDecorationService','SceneInspectorController','ThemeAssetStore','ThemeResourceImages') | ForEach-Object {
    '<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $repo "src\MalodyVThemeLoader.Plugin\$_.cs"))+'"/>'
}
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net6.0</TargetFramework><LangVersion>latest</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="BepInEx.Unity.IL2CPP" Version="6.0.0-be.785"/>'+($references -join '')+($sources -join '')+'<ProjectReference Include="'+[Security.SecurityElement]::Escape((Join-Path $repo 'src\MalodyVThemeLoader.Shared\MalodyVThemeLoader.Shared.csproj'))+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'Probe.csproj'),$xml)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\InspectorIl2CppProbe.cs') -Destination (Join-Path $build 'Probe.cs') -Force
dotnet build (Join-Path $build 'Probe.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false -v quiet
if($LASTEXITCODE -ne 0){throw 'IL2CPP runtime probe compile failed.'}
$plugin=Join-Path $playerRoot 'BepInEx\plugins\Probe'; New-Item -ItemType Directory -Force -Path $plugin | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'output\Probe.dll'),(Join-Path $build 'output\MalodyVThemeLoader.Shared.dll') -Destination $plugin -Force
$env:MALODY_PROBE_RESULT=Join-Path $project 'result.json'
if(Test-Path $env:MALODY_PROBE_RESULT){Remove-Item -LiteralPath $env:MALODY_PROBE_RESULT}
Add-Type -Path (Join-Path $PSScriptRoot 'validation\HiddenDesktopPlayer.cs')
$exit=[HiddenDesktopPlayer]::Run($player,(Join-Path $project 'player.log'))
if($exit -ne 0 -or !(Test-Path $env:MALODY_PROBE_RESULT)){Get-Content (Join-Path $playerRoot 'BepInEx\LogOutput.log') -Tail 20;throw 'IL2CPP runtime probe failed.'}
$result=Get-Content $env:MALODY_PROBE_RESULT -Raw | ConvertFrom-Json
if(!$result.passed){throw $result.error}
Get-Content $env:MALODY_PROBE_RESULT
