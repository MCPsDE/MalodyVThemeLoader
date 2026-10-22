param([string]$UnityExe='E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo 'artifacts\resource-render-test'
$build=Join-Path $project 'validation-build'
$plugins=Join-Path $project 'Assets\Plugins'
New-Item -ItemType Directory -Path $build,$plugins,(Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') -Force | Out-Null
$managed=Join-Path (Split-Path $UnityExe) 'Data\Managed\UnityEngine'
$projectXml=@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><AssemblyName>ThemeLoaderResourceValidation</AssemblyName><LangVersion>latest</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems><CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies><NoWarn>CS8602</NoWarn></PropertyGroup>
  <ItemGroup>
    <Compile Include="Store.cs"/><Compile Include="Images.cs"/><Compile Include="Stubs.cs"/>
    <Compile Include="__REPO__/src/MalodyVThemeLoader.Shared/AssetCatalog.cs"/>
    <Compile Include="Resources.cs"/>
    <Compile Include="__REPO__/src/MalodyVThemeLoader.Shared/ThemeManifest.cs"/>
    <Compile Include="__REPO__/src/MalodyVThemeLoader.Shared/PathRules.cs"/>
    <Compile Include="__REPO__/src/MalodyVThemeLoader.Shared/JsonDefaults.cs"/>
    <PackageReference Include="System.Text.Json" Version="6.0.10"/>
    <Reference Include="UnityEngine.CoreModule"><HintPath>__UNITY__/UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.ImageConversionModule"><HintPath>__UNITY__/UnityEngine.ImageConversionModule.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $build 'validation.csproj'),$projectXml.Replace('__REPO__',$repo.Replace('\','/')).Replace('__UNITY__',$managed.Replace('\','/')))
$store=[IO.File]::ReadAllText((Join-Path $repo 'src\MalodyVThemeLoader.Plugin\ThemeAssetStore.cs'))
$store=$store.Replace('using Il2CppInterop.Runtime.InteropTypes.Arrays;','').Replace('Il2CppStructArray<byte> il2CppBytes = bytes;','var il2CppBytes = bytes;').Replace('Convert.ToHexString(sha256.ComputeHash(stream))','BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "")')
[IO.File]::WriteAllText((Join-Path $build 'Store.cs'),$store)
Copy-Item -LiteralPath (Join-Path $repo 'src\MalodyVThemeLoader.Plugin\ThemeResourceImages.cs') -Destination (Join-Path $build 'Images.cs') -Force
# The validation calls Read/Resolve only. The .NET 6 atomic writer is covered by
# test-resource-editor; adapt its uncalled overload for Unity's Mono compiler.
$resourceSource=[IO.File]::ReadAllText((Join-Path $repo 'src\MalodyVThemeLoader.Shared\ThemeResources.cs')).Replace('File.Move(temporary, target, true);','File.Copy(temporary, target, true); File.Delete(temporary);')
[IO.File]::WriteAllText((Join-Path $build 'Resources.cs'),$resourceSource)
$stubs=@'
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
namespace System.Runtime.CompilerServices { public sealed class IsExternalInit {} }
namespace BepInEx.Logging { public sealed class ManualLogSource {
  public void LogInfo(object value) { UnityEngine.Debug.Log(value); }
  public void LogWarning(object value) { UnityEngine.Debug.LogWarning(value); }
} }
namespace MalodyVThemeLoader.Plugin { internal sealed record LoadedTheme(string ThemeDirectory, MalodyVThemeLoader.Shared.ThemeManifest Manifest) {
  public string ImageDirectory => MalodyVThemeLoader.Shared.PathRules.GetSafeCombinedPath(ThemeDirectory,Manifest.ImageRoot);
  public string Live2dDirectory => MalodyVThemeLoader.Shared.PathRules.GetSafeCombinedPath(ThemeDirectory,Manifest.Live2dRoot);
} }
'@
[IO.File]::WriteAllText((Join-Path $build 'Stubs.cs'),$stubs)
& dotnet build (Join-Path $build 'validation.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Unity resource validation assembly failed to compile.' }
Get-ChildItem (Join-Path $build 'output') -Filter '*.dll' | ForEach-Object {
    # Unity's netstandard 2.1 runtime already supplies these facade types.
    if ($_.Name -notin @('System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Threading.Tasks.Extensions.dll')) { Copy-Item -LiteralPath $_.FullName -Destination $plugins -Force }
}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f3`n")
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.modules.imageconversion":"1.0.0"}}')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\ResourceRenderValidation.cs') -Destination (Join-Path $project 'Assets\Editor\ResourceRenderValidation.cs') -Force
$result=Join-Path $project 'result.json'
if (Test-Path $result) { Remove-Item -LiteralPath $result }
$env:MALODY_RESOURCE_FIXTURE=Join-Path $repo 'artifacts\resource-editor-dark'
$env:MALODY_RESOURCE_RENDER_OUTPUT=$project
$arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','ResourceRenderValidation.Run','-logFile',('"'+(Join-Path $project 'unity.log')+'"'))
$process=Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity sprite validation PID $($process.Id); log: $project\unity.log"
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or !(Test-Path $result)) { Get-Content (Join-Path $project 'unity.log') -Tail 45; throw 'Unity sprite validation failed.' }
Get-Content $result
