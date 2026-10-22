param([string]$UnityExe='E:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',[switch]$Standalone)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $repo $(if($Standalone){'artifacts\inspector-overlay-player-test'}else{'artifacts\inspector-runtime-test'})
$build=Join-Path $project 'validation-build'
$plugins=Join-Path $project 'Assets\Plugins'
New-Item -ItemType Directory -Path $build,$plugins,(Join-Path $project 'Assets\Editor'),(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings') -Force|Out-Null
$managed=Join-Path (Split-Path $UnityExe) 'Data\Managed\UnityEngine'
$sources=@('ThemeAssetStore','ThemeResourceImages','SceneNodeLocator','NodeDecorationService','SceneInspectorController','ControlModeController','InspectorInputFence')
foreach($name in $sources){
    $source=[IO.File]::ReadAllText((Join-Path $repo "src\MalodyVThemeLoader.Plugin\$name.cs"))
    $source=$source.Replace('using Il2CppInterop.Runtime.InteropTypes.Arrays;','').Replace('using BepInEx.Unity.IL2CPP.Utils.Collections;','').Replace('.WrapToIl2Cpp()','')
    $source=$source.Replace('Il2CppStructArray<byte> il2CppBytes = bytes;','var il2CppBytes = bytes;').Replace('Il2CppStructArray<byte> bytes =','byte[] bytes =')
    $source=$source.Replace('private static readonly Il2CppStructArray<Vector3> cornerBuffer = new(4);','private static readonly Vector3[] cornerBuffer = new Vector3[4];')
    $source=$source.Replace('new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4)','new Vector3[4]')
    $source=$source.Replace('Convert.ToHexString(sha256.ComputeHash(stream))','BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "")')
    $source=$source.Replace('new Action<VideoPlayer, string>','new VideoPlayer.ErrorEventHandler')
    $source=$source.Replace('public SceneInspectorController(IntPtr pointer) : base(pointer)','public SceneInspectorController()').Replace('public ControlModeController(IntPtr pointer) : base(pointer)','public ControlModeController()')
    $source=$source.Replace('public InspectorInputFence(IntPtr pointer) : base(pointer)','public InspectorInputFence()')
    [IO.File]::WriteAllText((Join-Path $build ($name+'.cs')),$source)
}
foreach($name in @('ThemeResources','SceneInspector')){
    $source=[IO.File]::ReadAllText((Join-Path $repo "src\MalodyVThemeLoader.Shared\$name.cs")).Replace('File.Move(temporary, target, true);','File.Copy(temporary, target, true); File.Delete(temporary);')
    [IO.File]::WriteAllText((Join-Path $build ($name+'.cs')),$source)
}
$references=@(Get-ChildItem -LiteralPath $managed -Filter '*.dll'|ForEach-Object{'<Reference Include="'+$_.BaseName+'"><HintPath>'+ $_.FullName+'</HintPath><Private>false</Private></Reference>'})
$ui='E:\UnityProjects\Emiria\Library\ScriptAssemblies\UnityEngine.UI.dll'
$references+='<Reference Include="UnityEngine.UI"><HintPath>'+$ui+'</HintPath><Private>false</Private></Reference>'
$includes=@('AssetCatalog','ThemeManifest','PathRules','JsonDefaults','WebmVideo','ThemeLoaderSettings')|ForEach-Object{'<Compile Include="'+(Join-Path $repo "src\MalodyVThemeLoader.Shared\$_.cs")+'"/>'}
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><AssemblyName>ThemeLoaderInspectorValidation</AssemblyName><LangVersion>latest</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies></PropertyGroup><ItemGroup>'+($references -join '')+($includes -join '')+'<Compile Include="'+(Join-Path $PSScriptRoot 'validation\InspectorRuntimeValidation.cs')+'"/><PackageReference Include="System.Text.Json" Version="6.0.10"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $build 'validation.csproj'),$xml)
$stubs=@'
#nullable disable
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
namespace System.Runtime.CompilerServices { public sealed class IsExternalInit {} }
namespace BepInEx { public static class Paths { public static string GameRootPath => System.Environment.GetEnvironmentVariable("MALODY_INSPECTOR_FIXTURE"); } }
namespace BepInEx.Logging { public sealed class ManualLogSource { public void LogInfo(object v) => UnityEngine.Debug.Log(v); public void LogWarning(object v) => UnityEngine.Debug.LogWarning(v); public void LogDebug(object v) => UnityEngine.Debug.Log(v); } }
namespace MalodyVThemeLoader.Plugin {
 internal static class Plugin { public static readonly BepInEx.Logging.ManualLogSource Log = new(); }
 internal static class MyPluginInfo { public const string PLUGIN_VERSION = "__PLUGIN_VERSION__"; }
 internal sealed record LoadedTheme(string ThemeDirectory, MalodyVThemeLoader.Shared.ThemeManifest Manifest) {
   public string ActiveKey => "Inspector";
   public string ImageDirectory => MalodyVThemeLoader.Shared.PathRules.GetSafeCombinedPath(ThemeDirectory,Manifest.ImageRoot);
   public string Live2dDirectory => MalodyVThemeLoader.Shared.PathRules.GetSafeCombinedPath(ThemeDirectory,Manifest.Live2dRoot);
 }
 internal sealed class ThemeRuntime {
   public static ThemeRuntime Current; public LoadedTheme Theme; public ThemeAssetStore Assets; public NodeDecorationService Decorations;
   public static MalodyVThemeLoader.Shared.ThemeLoaderSettings ReadSettings(string root) => new();
 }
 internal static class SceneThemeApplier {
   public static bool IsAllowedScene(string s) => MalodyVThemeLoader.Shared.ThemeResources.IsAllowedScene(s);
   public static string GetHierarchy(UnityEngine.Transform t) { var parts=new System.Collections.Generic.List<string>(); while(t != null){parts.Add(t.name); t=t.parent;} parts.Reverse(); return string.Join("/",parts); }
 }
}
'@
$versionProps=[xml](Get-Content (Join-Path $repo 'Directory.Build.props') -Raw)
$stubs=$stubs.Replace('__PLUGIN_VERSION__',[string]$versionProps.Project.PropertyGroup.Version)
[IO.File]::WriteAllText((Join-Path $build 'Stubs.cs'),$stubs)
& dotnet build (Join-Path $build 'validation.csproj') -c Release -o (Join-Path $build 'output') -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Runtime validation assembly failed to compile.'}
Get-ChildItem (Join-Path $build 'output') -Filter '*.dll' | ForEach-Object {if($_.Name -notin @('System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Threading.Tasks.Extensions.dll')){Copy-Item -LiteralPath $_.FullName -Destination $plugins -Force}}
if($Standalone){
    # The source project's UI DLL includes UNITY_EDITOR references. Compile
    # UGUI from its package for a genuine Windows player instead.
    Remove-Item -LiteralPath (Join-Path $plugins 'UnityEngine.UI.dll'),(Join-Path $plugins 'UnityEngine.UI.dll.meta') -Force -ErrorAction SilentlyContinue
}else{Copy-Item -LiteralPath $ui -Destination $plugins -Force}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f3`n")
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.modules.imageconversion":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.video":"1.0.0","com.unity.modules.screencapture":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.physics":"1.0.0"}}')
if($Standalone){
    $manifestPath=Join-Path $project 'Packages\manifest.json'
    $manifest=Get-Content $manifestPath -Raw|ConvertFrom-Json -AsHashtable
    $manifest.dependencies['com.unity.ugui']='1.0.0'
    $manifest|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $manifestPath -Encoding utf8
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'validation\InspectorValidationEntry.cs') -Destination (Join-Path $project 'Assets\Editor\InspectorValidationEntry.cs') -Force
$env:MALODY_INSPECTOR_OUTPUT=$project
$env:MALODY_INSPECTOR_FIXTURE=Join-Path $project 'fake-game'
$env:MALODY_INSPECTOR_MEDIA=Join-Path $repo 'artifacts\inspector-media'
$result=Join-Path $project 'result.json'
if(Test-Path $result){Remove-Item -LiteralPath $result}
$entry=if($Standalone){'InspectorValidationEntry.BuildPlayer'}else{'InspectorValidationEntry.Run'}
$arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod',$entry,'-logFile',('"'+(Join-Path $project 'unity.log')+'"'))
$process=Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Inspector Unity validation PID $($process.Id)."
$process.WaitForExit()
if($Standalone){
    if($process.ExitCode -ne 0){Get-Content (Join-Path $project 'unity.log') -Tail 55;throw 'Overlay validation player build failed.'}
    $player=Join-Path $project 'player\InspectorOverlayValidation.exe'
    Add-Type -Path (Join-Path $PSScriptRoot 'validation\HiddenDesktopPlayer.cs')
    $playerExit=[HiddenDesktopPlayer]::Run($player,(Join-Path $project 'player.log'))
    if($playerExit -ne 0){Get-Content (Join-Path $project 'result.json') -ErrorAction SilentlyContinue;throw 'Standalone overlay validation failed.'}
}
if($process.ExitCode -ne 0 -or !(Test-Path $result)){Get-Content (Join-Path $project 'unity.log') -Tail 55;throw 'Isolated runtime validation failed.'}
Get-Content $result
