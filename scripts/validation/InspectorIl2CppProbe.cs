using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using MalodyVThemeLoader.Shared;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

// Only the runtime collaborators outside this test's scope are stubbed. The
// inspector, native raycast filter, locator and decoration sources are unchanged.
internal static class ControlModeController
{ public static bool TryMapInspectorPoint(Vector2 point, out Vector2 mapped) { mapped = point; return true; } }
internal static class MyPluginInfo { public const string PLUGIN_VERSION = "probe"; }
internal sealed record LoadedTheme(string ImageDirectory, ThemeManifest Manifest)
{ public string ThemeDirectory => ImageDirectory; public string Live2dDirectory => ImageDirectory; public string ActiveKey => "Probe"; }
internal sealed class ThemeRuntime
{
    public static ThemeRuntime? Current { get; set; }
    public ThemeAssetStore Assets = null!; public NodeDecorationService Decorations = null!; public LoadedTheme Theme = null!;
}
internal static class SceneThemeApplier
{
    public static bool IsAllowedScene(string scene) => SceneComponentQuery.IsAllowedScene(scene);
    public static string GetHierarchy(Transform target)
    { var names = new List<string>(); while (target != null) { names.Add(target.name); target = target.parent; } names.Reverse(); return string.Join("/", names); }
}
internal static class Plugin { public static ManualLogSource Log = null!; }

[BepInPlugin("test.inspector.lifetime", "Inspector lifetime probe", "1.0.0")]
public sealed class InspectorLifetimeProbe : BasePlugin
{
    public override void Load()
    {
        Plugin.Log = Log;
        ClassInjector.RegisterTypeInIl2Cpp<InspectorInputFence>(new() { Interfaces = new[] { typeof(ICanvasRaycastFilter) } });
        AddComponent<InspectorLifetimeDriver>();
    }
}

public sealed class InspectorLifetimeDriver : MonoBehaviour
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly string result = Environment.GetEnvironmentVariable("MALODY_PROBE_RESULT")!;
    private readonly InspectorControl control = new() { Client = "probe", Enabled = true };
    private readonly ThemeResources rules = new();
    private SceneInspectorController? inspector;
    private NodeDecorationService? decorations;
    private LoadedTheme theme = null!;
    private RectTransform target = null!;
    private int tick;
    private int overlayChecks;
    public InspectorLifetimeDriver(IntPtr pointer) : base(pointer) { }

    private void Start()
    {
        try
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("SceneSong"));
            overlayChecks = OverlayVisibilityValidation.Run(Paths.GameRootPath, false);
            var canvas = new GameObject("Canvas"); canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            target = new GameObject("Target").AddComponent<RectTransform>(); target.SetParent(canvas.transform, false); target.sizeDelta = new Vector2(241, 179);
            var caption = new GameObject("Caption").AddComponent<Text>(); caption.transform.SetParent(canvas.transform, false);
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); caption.text = "Runtime";
            var directory = Path.Combine(Paths.GameRootPath, "fixture"); Directory.CreateDirectory(directory);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            File.WriteAllBytes(Path.Combine(directory, "bg.png"), ImageConversion.EncodeToPNG(texture)); UnityEngine.Object.Destroy(texture);
            theme = new LoadedTheme(directory, new ThemeManifest());
            rules.Nodes.Add(new ThemeNodeRule { Scene = "SceneSong", Address = SceneNodeLocator.AddressOf(target), ImageFile = "bg.png", Fit = "cover" });
            decorations = new NodeDecorationService(Plugin.Log, theme, rules); decorations.Tick();
            var store = new ThemeAssetStore(Plugin.Log, theme, new AssetCatalog());
            ThemeRuntime.Current = new ThemeRuntime { Theme = theme, Assets = store, Decorations = decorations };
            ClassInjector.RegisterTypeInIl2Cpp<SceneInspectorController>();
            inspector = gameObject.AddComponent<SceneInspectorController>(); inspector.Initialize(); Renew();
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Update()
    {
        try
        {
            if (inspector == null) return;
            if (tick % 10 == 0) Renew();
            if (tick % 10 == 0 && !SceneComponentQuery.Find<Text>("SceneSong").Any(text => text.name == "Caption"))
                throw new Exception("Scene-local native component discovery lost the game UI.");
            typeof(SceneInspectorController).GetField("nextPoll", Flags)!.SetValue(inspector, 0f);
            typeof(SceneInspectorController).GetField("nextSnapshot", Flags)!.SetValue(inspector, 0f);
            Call("Update"); Call("Pick", new Vector2(Screen.width / 2, Screen.height / 2)); Call("LateUpdate");
            var snapshot = InspectorFiles.Read<InspectorSnapshot>(Path.Combine(Paths.GameRootPath, "ThemeLoader"), InspectorFiles.SnapshotName)!;
            if (control.PointerSearch && (!snapshot.PointerSearch || !snapshot.HasPointerSample || !snapshot.PointerHits.Contains(target.GetInstanceID())))
                throw new Exception("Through search lost the active node in the IL2CPP domain.");
            if (!control.PointerSearch && snapshot.PointerHits.Count > 0)
                throw new Exception("Disabled through search retained IL2CPP candidates.");
            var overlay = (GameObject)typeof(SceneInspectorController).GetField("overlay", Flags)!.GetValue(inspector)!;
            var mesh = (Mesh)typeof(SceneInspectorController).GetField("mesh", Flags)!.GetValue(inspector)!;
            if (mesh.vertexCount != 16 || overlay.GetComponentsInChildren<Text>().Length != 1)
                throw new Exception("Expected exactly one selected frame and label in IL2CPP.");
            var filter = overlay.GetComponentInChildren<InspectorInputFence>();
            if (filter.GetComponent<Image>() == null || filter.TryCast<ICanvasRaycastFilter>() == null)
                throw new Exception("Native Image / ICanvasRaycastFilter interface not installed.");
            filter.CaptureForFrame(true, Time.frameCount);
            if (!filter.Cast<ICanvasRaycastFilter>().IsRaycastLocationValid(Vector2.zero, null))
                throw new Exception("Native filter interface did not dispatch to the managed inspection fence.");
            if (tick % 6 == 3)
            {
                decorations = new NodeDecorationService(Plugin.Log, theme, rules); decorations.Tick();
                ThemeRuntime.Current!.Decorations = decorations;
            }
            decorations!.Tick();
            if (target.GetComponentsInChildren<Image>().Length != 1) throw new Exception("Reload duplicated or lost the background layer.");
            if (tick % 6 == 2) { decorations.Dispose(); decorations.Dispose(); }
            if (tick % 6 == 4) { GC.Collect(); Il2CppInterop.Runtime.IL2CPP.il2cpp_gc_collect(0); GC.WaitForPendingFinalizers(); }
            if (++tick >= 600) Finish(true, "");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    [HideFromIl2Cpp] private void Call(string method, params object[] arguments) => typeof(SceneInspectorController).GetMethod(method, Flags)!.Invoke(inspector, arguments);
    [HideFromIl2Cpp] private void Renew()
    { control.PointerSearch = tick % 200 >= 100; control.LeaseUtc = DateTime.UtcNow; InspectorFiles.Write(Path.Combine(Paths.GameRootPath, "ThemeLoader"), InspectorFiles.ControlName, control); }
    [HideFromIl2Cpp] private void Finish(bool passed, string error)
    {
        File.WriteAllText(result, JsonSerializer.Serialize(new { passed, picks = tick, reloads = tick / 6, overlayChecks, error,
            scope = "isolated Unity 2022.3.62f3 IL2CPP + pinned BepInEx 785; production sources; not the real game or other plugins" }, JsonDefaults.Options));
        enabled = false; Application.Quit(passed ? 0 : 1);
    }
}
