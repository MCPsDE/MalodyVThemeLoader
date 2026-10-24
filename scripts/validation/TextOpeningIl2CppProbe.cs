using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using MalodyVThemeLoader.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

// The game-specific state adapter is separately checked against the shipped interop.
// The probe supplies the same presentation boolean without importing business code.
internal static class TextSelectionAdapter
{
    internal static readonly Dictionary<int, bool> States = new();
    internal static bool? Selected(Text? text) => text != null && States.TryGetValue(text.GetInstanceID(), out var selected) ? selected : null;
}
internal static class SceneInspectorController { internal static bool TryMouse(out Vector2 p) { p = Vector2.zero; return false; } }
internal static class SceneThemeApplier
{
    internal static bool IsAllowedScene(string s) => SceneComponentQuery.IsAllowedScene(s);
    internal static string GetHierarchy(Transform t) => t.name;
}
internal static class SceneNodeLocator
{
    internal const string OwnPrefix = "ThemeLoader.";
    internal static bool IsOwnName(string s) => s.StartsWith(OwnPrefix, StringComparison.Ordinal);
    internal static bool IsOwn(Transform t) => IsOwnName(t.name);
    internal static Camera? CameraFor(RectTransform rect) => null;
}
internal sealed record SceneNode(Transform Transform, InspectorNode Info);

[BepInPlugin("test.text-opening", "Text and opening native probe", "1.0.0")]
public sealed class TextOpeningProbe : BasePlugin
{
    public override void Load() { ProbeDriver.Log = Log; AddComponent<ProbeDriver>(); }
}
public sealed class ProbeDriver : MonoBehaviour
{
    internal static ManualLogSource Log = null!;
    private readonly string result = Environment.GetEnvironmentVariable("MALODY_TEXT_RESULT")!;
    private readonly string mediaRoot = Environment.GetEnvironmentVariable("MALODY_TEXT_MEDIA")!;
    private TextAppearanceService? service;
    private OpeningPresentation? opening;
    private Text legacy = null!;
    private TMP_Text tmp = null!;
    private Material material = null!;
    private RectTransform canvas = null!;
    private readonly List<Text> rows = new();
    private readonly ThemeResources resources = new();
    private int tick, checks, phase;
    private float nextAt, deadline;
    private Color originalTmp;
    private VertexGradient originalGradient;
    private TMP_ColorGradient preset = null!;
    private bool originalOverride;
    private string latestText = "";
    private Color latestColor;
    public ProbeDriver(IntPtr pointer) : base(pointer) { }
    private void Start()
    {
        try
        {
            deadline = Time.realtimeSinceStartup + 40;
            SceneManager.SetActiveScene(SceneManager.CreateScene("SceneSong"));
            canvas = Rect("Canvas", null); canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            legacy = Label("Label", canvas); legacy.color = new Color(.2f, .3f, .4f, .7f); legacy.text = "<color=#FF0000>GLOBAL</color>"; legacy.supportRichText = true;
            legacy.rectTransform.anchoredPosition = new Vector2(0, 100);
            var tmpRect = Rect("Tmp", canvas); tmp = tmpRect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Resources.Load<TMP_FontAsset>("ProbeFont"); tmp.text = "<color=#FF0000>GRADE</color>"; tmp.fontSize = 32; tmp.color = originalTmp = new Color(.8f, .7f, .6f, .5f);
            originalGradient = tmp.colorGradient; originalOverride = tmp.overrideColorTags; material = tmp.fontSharedMaterial;
            preset = ScriptableObject.CreateInstance<TMP_ColorGradient>(); preset.topLeft = Color.black; preset.topRight = Color.white; preset.bottomLeft = Color.black; preset.bottomRight = Color.white;
            tmp.colorGradientPreset = preset;
            Require(material != null, "TMP source material absent");
            for (var i = 0; i < 2; i++) { var row = Rect("Row", canvas); row.anchoredPosition = new Vector2(0, -70 - i * 60); rows.Add(Label("Name", row)); }
            var huge = Rect("UnrelatedEditorBranch", null);
            for (var i = 0; i < 6000; i++) Rect("Unused" + i, huge);
            resources.Texts.Add(new ThemeTextRule { Scene = "SceneSong", Address = "Canvas#0:1/Label#0:1", Hierarchy = "Canvas/Label", Color = "#12AB34", SelectedColor = "#445566", HoverColor = "#FEDCBA", RichText = "uniform",
                Mappings = new() { new TextColorMapping { Source = "#F90000", Target = "#0000EE" } } });
            resources.Texts.Add(new ThemeTextRule { Scene = "SceneSong", Address = "Canvas#0:1/Row#0:2/Name#0:1", RepeatDepth = 1, Color = "#AABBCC" });
            resources.Texts.Add(new ThemeTextRule { Scene = "SceneSong", Address = "Canvas#0:1/Tmp#0:1", Component = "TMP_Text", Color = "#FFFFFF", Opacity = .65f, RichText = "uniform", Gradient = "custom",
                GradientColors = new[] { "#FF0000", "#00FF00", "#0000FF", "#FFFFFF" }, FaceColor = "#998877", OutlineColor = "#123456" });
            resources.Validate(); service = new TextAppearanceService(Log, resources);
            var query = new SceneNodeTargets(resources.Texts); var resolved = query.Resolve();
            Require(resolved.Count == 4 && query.LastSiblingReads < 32, "Target lookup traversed unrelated branches");
            File.WriteAllText(result + ".lookup", JsonSerializer.Serialize(new { roots = resolved.Count, siblingReads = query.LastSiblingReads, unrelatedNodes = 6000 }));
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Update()
    {
        try
        {
            if (tick < 0) return;
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Native text/opening probe timed out.");
            tick++;
            if (phase == 0 && tick > 2)
            {
                service!.Tick();
                Require(TextAppearanceService.Hex(legacy.color) == "#12AB34" && Near(legacy.color.a, .7f), "Legacy color/alpha");
                Require(legacy.text == "GLOBAL", "Legacy rich text color tags were not unified");
                Require(rows.All(r => TextAppearanceService.Hex(r.color) == "#AABBCC"), "Template missed duplicate rows");
                Require(tmp.overrideColorTags && tmp.enableVertexGradient && Near(tmp.color.a, .65f), "TMP rich text/gradient/alpha");
                Require(tmp.fontSharedMaterial != material && TextAppearanceService.Hex(tmp.fontSharedMaterial.GetColor("_FaceColor")) == "#998877", "TMP face material was not isolated");
                Require(TextAppearanceService.Hex(material.GetColor("_FaceColor")) != "#998877", "Shared material changed");
                Require(TextAppearanceService.Hex(tmp.fontSharedMaterial.GetColor("_OutlineColor")) == "#123456", "TMP outline missing");
                Require(TextAppearanceService.Hex(tmp.colorGradient.bottomLeft) == "#0000FF", "TMP gradient corner order");
                Require(tmp.colorGradientPreset == null, "Original TMP gradient preset still overrides custom corners");
                var snapshot = TextAppearanceService.Describe(tmp.transform);
                Require(snapshot.Count == 1 && snapshot[0].Component == "TMP_Text" && snapshot[0].Content.Contains("GRADE"), "Live text snapshot");
                ScreenCapture.CaptureScreenshot(result + ".text.png"); phase = 1;
            }
            else if (phase == 1)
            {
                TextSelectionAdapter.States[legacy.GetInstanceID()] = true; legacy.color = new Color(249 / 255f, 0, 0, .37f); service!.Tick();
                Require(TextAppearanceService.Hex(legacy.color) == "#445566" && Near(legacy.color.a, .37f), "Selected state lost game fade");
                TextSelectionAdapter.States[legacy.GetInstanceID()] = false; legacy.color = new Color(249 / 255f, 0, 0, .42f); service.Tick();
                Require(TextAppearanceService.Hex(legacy.color) == "#0000EE", "Script source color mapping");
                Require(resources.Texts[0].ColorFor("#F90000", true, true) == "#FEDCBA", "Hover precedence");
                for (var i = 0; i < 240; i++)
                {
                    latestColor = new Color(.1f, .2f, .3f, (i % 100) / 100f); legacy.color = latestColor;
                    latestText = "<b><color=#00FF00>GLOBAL " + i + "</color></b>"; legacy.text = latestText; service.Tick();
                    Require(Near(legacy.color.a, latestColor.a) && legacy.text == "<b>GLOBAL " + i + "</b>", "Animated alpha/localized rich text update");
                }
                // Later game font/material and gradient writes remain the restoration source.
                var shared = material; material = new Material(shared); tmp.fontSharedMaterial = material;
                originalGradient = new VertexGradient { topLeft = new Color(.9f, .8f, .7f), topRight = Color.white, bottomLeft = Color.black, bottomRight = Color.white };
                tmp.colorGradient = originalGradient; tmp.enableVertexGradient = false; service.Tick();
                Require(tmp.fontSharedMaterial != material && TextAppearanceService.Hex(material.GetColor("_FaceColor")) != "#998877", "Later material write changed shared source");
                service.Dispose(); service = null;
                Require(legacy.color == latestColor && legacy.text == latestText, "Restoration froze first-frame source text/color");
                Require(tmp.color == originalTmp && tmp.overrideColorTags == originalOverride && tmp.fontSharedMaterial == material && !tmp.enableVertexGradient,
                    "TMP properties did not restore");
                Require(tmp.colorGradient.topLeft == originalGradient.topLeft, "TMP gradient did not restore");
                Require(tmp.colorGradientPreset == preset, "TMP original gradient preset was not restored");
                rows.Add(Label("Name", Rect("Row", canvas)));
                service = new TextAppearanceService(Log, resources); service.Tick();
                Require(rows.All(r => TextAppearanceService.Hex(r.color) == "#AABBCC"), "Template failed after row count change");
                UnityEngine.Object.DestroyImmediate(rows[0].gameObject); service.Tick();
                Require(rows.Skip(1).All(r => TextAppearanceService.Hex(r.color) == "#AABBCC"), "Destroyed native label interrupted remaining bindings");
                var query = new SceneNodeTargets(new[] { new ThemeNodeRule { Scene = "SceneSong", Address = "Canvas#0:1/Row#0:2/Name#0:1" } });
                Require(query.Resolve().Count == 0, "Exact address accepted changed duplicate count");
                service.Dispose(); service = null;
                opening = new OpeningPresentation(Log, mediaRoot, new ThemeOpening { File = "background.png", Duration = 1 });
                Require(Alpha(opening) == 0, "Opening hid original before readiness"); opening.Tick(); Require(Alpha(opening) == 1, "Image opening never appeared");
                ScreenCapture.CaptureScreenshot(result + ".opening.png"); nextAt = Time.realtimeSinceStartup + 1.2f; phase = 2;
            }
            else if (phase == 2 && Time.realtimeSinceStartup >= nextAt)
            {
                opening!.Tick(); Require(opening.Finished, "Opening did not stop at duration"); opening = null;
                var failed = false;
                try { opening = new OpeningPresentation(Log, mediaRoot, new ThemeOpening { File = "invalid.webm", Kind = "video" }); }
                catch (InvalidDataException) { failed = true; }
                Require(failed, "Corrupt video was accepted");
                opening = new OpeningPresentation(Log, mediaRoot, new ThemeOpening { File = "vp8.webm", Kind = "video", Duration = 2 });
                nextAt = Time.realtimeSinceStartup + 8; phase = 3;
            }
            else if (phase == 3)
            {
                opening!.Tick();
                Require(Time.realtimeSinceStartup < nextAt, "Video opening preparation failed");
                if (Alpha(opening) > 0) { Require(!opening.Finished, "Video finished before showing a frame"); phase = 4; nextAt = Time.realtimeSinceStartup + 3; }
            }
            else if (phase == 4)
            {
                opening!.Tick();
                Require(Time.realtimeSinceStartup < nextAt, "Video opening leaked after playback");
                if (opening.Finished) { opening = null; phase = 5; nextAt = Time.realtimeSinceStartup + .3f; }
            }
            else if (phase == 5 && Time.realtimeSinceStartup > nextAt)
            {
                Require(!Resources.FindObjectsOfTypeAll<GameObject>().Any(g => g != null && g.name.StartsWith("ThemeLoader.Opening", StringComparison.Ordinal)), "Opening objects leaked");
                Require(!Resources.FindObjectsOfTypeAll<Material>().Any(m => m != null && m.name == "ThemeLoader.TextMaterial"), "TMP owned material leaked");
                Finish(true, "");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static float Alpha(OpeningPresentation o) => ((CanvasGroup?)typeof(OpeningPresentation).GetField("group", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o))?.alpha ?? 0;
    private static RectTransform Rect(string name, Transform? parent)
    {
        var go = new GameObject(name); var rect = go.AddComponent<RectTransform>(); if (parent != null) rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(260, 100); return rect;
    }
    private static Text Label(string name, Transform parent)
    {
        var text = Rect(name, parent).gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 28; text.text = "TEXT"; return text;
    }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .002f;
    private void Require(bool valid, string reason) { checks++; if (!valid) throw new InvalidOperationException(reason); }
    private void Finish(bool passed, string error)
    {
        if (tick < 0) return; tick = -1;
        try { service?.Dispose(); opening?.Dispose(); File.WriteAllText(result, JsonSerializer.Serialize(new { passed, checks, error, unity = Application.unityVersion, backend = "IL2CPP", frames = Time.frameCount })); }
        finally { Application.Quit(passed ? 0 : 1); }
    }
}
