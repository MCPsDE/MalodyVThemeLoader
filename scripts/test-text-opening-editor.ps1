param([ValidateSet('dark','light')][string]$Appearance='dark')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fake=Join-Path $repo ('artifacts\text-opening-editor-'+$Appearance)
$plugin=Join-Path $fake 'BepInEx\plugins\MalodyVThemeLoader'; $themeRoot=Join-Path $fake 'ThemeLoader\themes\TextOpening'
New-Item -ItemType Directory -Force -Path $plugin,$themeRoot,(Join-Path $fake 'Malody V_Data')|Out-Null
foreach($name in @('Malody V.exe','UnityPlayer.dll','GameAssembly.dll')){[IO.File]::WriteAllBytes((Join-Path $fake $name),[byte[]](1,2,3))}
Copy-Item -LiteralPath (Join-Path $repo 'artifacts\payload\BepInEx\plugins\MalodyVThemeLoader\catalog.json') -Destination $plugin -Force
[IO.File]::WriteAllText((Join-Path $themeRoot 'theme.json'),'{"formatVersion":1,"id":"TextOpening","name":"文字与开屏验证主题"}')
if(Test-Path (Join-Path $themeRoot 'resources.json')){Remove-Item -LiteralPath (Join-Path $themeRoot 'resources.json')}
$env:MALODY_THEMELOADER_DISABLE_UPDATES='1';$env:MALODY_THEMELOADER_PREFERENCES=Join-Path $fake 'preferences'
$build=Join-Path $repo 'artifacts\text-opening-ui-host';New-Item -ItemType Directory -Force -Path $build|Out-Null
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0-windows</TargetFramework><RuntimeIdentifier>win-x64</RuntimeIdentifier><UseWindowsForms>true</UseWindowsForms><SelfContained>true</SelfContained><EnableDefaultCompileItems>false</EnableDefaultCompileItems><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'validation\TextOpeningEditorValidation.cs'))+'"/><ProjectReference Include="'+[Security.SecurityElement]::Escape((Join-Path $repo 'src\MalodyVThemeLoader.ThemeManager\MalodyVThemeLoader.ThemeManager.csproj'))+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'UiValidation.csproj'),$xml)
dotnet build (Join-Path $build 'UiValidation.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false -v quiet
if($LASTEXITCODE -ne 0){throw 'Native .NET 8 UI host compile failed.'}
$out=Join-Path $repo ('.impeccable\review\text-opening-'+$Appearance);New-Item -ItemType Directory -Force -Path $out|Out-Null
$resultPath=Join-Path $out 'result.json';if(Test-Path $resultPath){Remove-Item -LiteralPath $resultPath}
Add-Type -Path (Join-Path $PSScriptRoot 'validation\HiddenDesktopPlayer.cs')
$arguments='"'+$fake+'" "'+$Appearance+'" "'+$out+'" "'+(Join-Path $repo 'artifacts\inspector-media')+'"'
$exit=[HiddenDesktopPlayer]::RunCommand((Join-Path $build 'output\UiValidation.exe'),$arguments,$build,180)
if(!(Test-Path $resultPath)){throw "UI host did not produce results ($exit)."}
$result=Get-Content $resultPath -Raw|ConvertFrom-Json
if(!$result.passed -or $exit -ne 0){throw $result.error}
Get-Content $resultPath