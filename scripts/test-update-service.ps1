$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$test=Join-Path $repo 'artifacts/update-service-test'
New-Item -ItemType Directory -Path $test -Force|Out-Null
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include="'+$repo+'/src/MalodyVThemeLoader.ThemeManager/UpdateService.cs"/><Compile Include="'+$repo+'/src/MalodyVThemeLoader.ThemeManager/InspectorHeartbeat.cs"/><Compile Include="'+$repo+'/scripts/validation/UpdateValidation.cs"/><ProjectReference Include="'+$repo+'/src/MalodyVThemeLoader.Shared/MalodyVThemeLoader.Shared.csproj"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $test 'validation.csproj'),$xml)
& dotnet run --project (Join-Path $test 'validation.csproj') -c Release -- $test
if($LASTEXITCODE -ne 0){throw 'Update/heartbeat validation failed.'}
