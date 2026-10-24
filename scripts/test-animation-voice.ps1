param(
    [string]$SampleDirectory = 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV\ThemeLoader\themes\Murasame\live2d\models',
    [string]$ManagerDll = ''
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build = Join-Path $repo 'artifacts\animation-voice-test'
$run = Join-Path $build ([guid]::NewGuid().ToString('N'))
$models = Join-Path $run 'live2d\models'
New-Item -ItemType Directory -Path $models -Force | Out-Null
foreach ($entry in Get-ChildItem -LiteralPath $SampleDirectory) { Copy-Item -LiteralPath $entry.FullName -Destination $models -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\fixtures\silent-voice.ogg') -Destination (Join-Path $run 'voice.ogg')
if (!$ManagerDll) { $ManagerDll = Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\bin\Release\net8.0-windows\win-x64\MalodyVThemeLoader.ThemeManager.dll' }
$sources = @(
    (Join-Path $repo 'src\MalodyVThemeLoader.Plugin\AnimationAudioPlayer.cs'),
    (Join-Path $PSScriptRoot 'validation\MutedVoiceOutput.cs'),
    (Join-Path $PSScriptRoot 'validation\AnimationVoiceBindingValidation.cs')
) | ForEach-Object { '<Compile Include="' + [Security.SecurityElement]::Escape($_) + '"/>' }
$xml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><PackageReference Include="BepInEx.Core" Version="6.0.0-be.785"/><PackageReference Include="NAudio.WinMM" Version="2.2.1"/>' + ($sources -join '') + '<ProjectReference Include="' + [Security.SecurityElement]::Escape((Join-Path $repo 'src\MalodyVThemeLoader.Shared\MalodyVThemeLoader.Shared.csproj')) + '"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'VoiceTest.csproj'), $xml)
dotnet build (Join-Path $build 'VoiceTest.csproj') -c Release -p:UseSharedCompilation=false -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Voice binding validation build failed.' }
dotnet (Join-Path $build 'bin\Release\net8.0-windows\VoiceTest.dll') $run $ManagerDll
if ($LASTEXITCODE -ne 0) { throw 'Voice binding validation failed.' }
Copy-Item -LiteralPath (Join-Path $run 'result.json') -Destination (Join-Path $build 'result.json') -Force
