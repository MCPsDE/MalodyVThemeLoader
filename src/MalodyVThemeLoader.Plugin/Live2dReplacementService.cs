using System.Text.Json;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Malody.Live2d;
using Malody.Scene.Widget;
using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal sealed class Live2dReplacementService : IDisposable
{
    private readonly ManualLogSource log;
    private readonly LoadedTheme theme;
    private readonly List<Replacement> replacements = new();
    private readonly HashSet<int> failed = new();
    private Live2dBinding? binding;
    private string lastStatus = "";
    private float nextScan;
    private static readonly Lazy<bool> CompatibleGame = new(() =>
    {
        // Match the native build that generated this version's obfuscated API.
        // A theme's declared gameVersion is not evidence of API compatibility.
        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)) ==
            "F554AF9B371F7CD3629EE1E257B76FBA41B3899FA6F2C33F2C6E121B17B8021F";
    });

    public Live2dReplacementService(ManualLogSource log, LoadedTheme theme)
    {
        this.log = log;
        this.theme = theme;
        var file = Path.Combine(theme.Live2dDirectory, Live2dModelDocument.BindingFile);
        if (!File.Exists(file)) return;
        try
        {
            binding = JsonSerializer.Deserialize<Live2dBinding>(File.ReadAllText(file), JsonDefaults.Options)
                ?? throw new InvalidDataException("Live2D 配置为空。");
            binding.Validate();
            if (!CompatibleGame.Value)
                throw new InvalidDataException($"当前游戏版本 {Application.version} 尚未验证模型替换，请更新加载器。");
            var document = Live2dModelDocument.Read(theme.Live2dDirectory, binding.Model);
            document.ValidateBinding(theme.Live2dDirectory, binding);
            Status("waiting", "等待游戏显示看板角色。");
        }
        catch (Exception e) { Status("error", e.Message); log.LogWarning($"Live2D binding rejected: {e}"); binding = null; }
    }

    // Called on Unity's main thread, before the game's Cubism LateUpdate.
    public void Tick()
    {
        if (binding == null) return;
        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var replacement = replacements[i];
            if (replacement.Original == null || replacement.Widget == null || replacement.Visual.Model == null)
            {
                replacement.Dispose();
                replacements.RemoveAt(i);
                continue;
            }
            if (!SceneThemeApplier.IsAllowedScene(replacement.Widget.gameObject.scene.name)) continue;
            replacement.Animate();
            replacement.KeepOriginalHidden();
        }
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .4f;
        if (replacements.Count == 0 && failed.Count == 0) Status("waiting", "等待游戏显示看板角色。");
        foreach (var widget in Resources.FindObjectsOfTypeAll<WidgetLive2dControl>())
        {
            if (!SceneThemeApplier.IsSceneComponent(widget) || !widget.gameObject.activeInHierarchy
                || !SceneThemeApplier.IsAllowedScene(widget.gameObject.scene.name) || widget.container == null) continue;
            // Read the already-instantiated visual only. Never select/instantiate
            // a game character or invoke MarloManage, purchases or notifications.
            GameObject? original = widget.bkmv != null ? widget.bkmv.gameObject
                : widget.bkmw != null ? widget.bkmw.gameObject : null;
            if (original == null || replacements.Any(r => r.Original == original) || failed.Contains(original.GetInstanceID())) continue;
            try
            {
                replacements.Add(Create(widget, original));
                Status("applied", "模型已在游戏中显示。");
                log.LogInfo($"Live2D model applied: {binding.Model} -> {SceneThemeApplier.GetHierarchy(original.transform)}");
            }
            catch (Exception e)
            {
                failed.Add(original.GetInstanceID());
                Status("error", e.Message);
                log.LogWarning($"Live2D replacement failed; original preserved: {e}");
            }
        }
    }

    public void PlayTap(WidgetLive2dControl widget)
    {
        foreach (var replacement in replacements.Where(r => r.Widget == widget)) replacement.PlayTap();
    }

    private Replacement Create(WidgetLive2dControl widget, GameObject original)
    {
        var document = Live2dModelDocument.Read(theme.Live2dDirectory, binding!.Model);
        var textures = new List<Texture2D>();
        CubismVisual? visual = null;
        try
        {
            foreach (var relative in document.Textures)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                textures.Add(texture);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Live2dModelDocument.Resolve(theme.Live2dDirectory, relative)), false))
                    throw new InvalidDataException($"无法读取纹理：{relative}");
                texture.name = "ThemeLoader." + relative;
            }
            visual = CubismVisualFactory.Create(File.ReadAllBytes(Live2dModelDocument.Resolve(theme.Live2dDirectory, document.Moc)), textures.ToArray());
            var bounds = DestinationBounds(widget, original);
            CubismVisualFactory.Fit(visual, original.transform.parent, bounds, original.layer,
                binding.Scale, binding.OffsetX, binding.OffsetY);
            var replacement = new Replacement(widget, original, visual, textures,
                LoadMotion(binding.IdleMotion), LoadMotion(binding.TapMotion));
            replacement.Animate();
            // Commit only after the replacement has valid geometry, materials,
            // textures and animation. Game fields still refer to its own model.
            CubismVisualFactory.Show(visual);
            replacement.HideOriginal();
            return replacement;
        }
        catch
        {
            visual?.Dispose();
            foreach (var texture in textures) UnityEngine.Object.DestroyImmediate(texture);
            throw;
        }
    }

    private CubismMotion? LoadMotion(string file) => string.IsNullOrEmpty(file) ? null
        : CubismMotion.Read(Live2dModelDocument.Resolve(theme.Live2dDirectory, file));

    private static Bounds DestinationBounds(WidgetLive2dControl widget, GameObject original)
    {
        var parent = original.transform.parent;
        var renderers = original.GetComponentsInChildren<MeshRenderer>(true);
        var first = true;
        var bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            if (renderer.bounds.size.y < .001f) continue;
            var world = renderer.bounds;
            foreach (var point in new[] { world.min, world.max })
            {
                var local = parent.InverseTransformPoint(point);
                if (first) { bounds = new Bounds(local, Vector3.zero); first = false; } else bounds.Encapsulate(local);
            }
        }
        if (!first && bounds.size.y > .001f) return bounds;
        // Legacy Live2D draws directly and has no MeshRenderer bounds. Its
        // existing click region supplies the intended on-screen character box.
        var rect = widget.GetComponent<RectTransform>();
        if (rect == null) throw new InvalidDataException("找不到角色显示区域。");
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        bounds = new Bounds(parent.InverseTransformPoint(corners[0]), Vector3.zero);
        for (var i = 1; i < 4; i++) bounds.Encapsulate(parent.InverseTransformPoint(corners[i]));
        return bounds;
    }

    private void Status(string state, string message)
    {
        if (state + message == lastStatus) return;
        lastStatus = state + message;
        try
        {
            var path = Path.Combine(Paths.GameRootPath, "ThemeLoader", "live2d-status.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new Live2dLoadStatus
            {
                PluginVersion = MyPluginInfo.PLUGIN_VERSION, Theme = theme.Manifest.Id,
                Model = binding?.Model ?? "", State = state, Message = message
            }, JsonDefaults.Options));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception e) { log.LogDebug($"Live2D status could not be written: {e.Message}"); }
    }

    public void Dispose()
    {
        foreach (var replacement in replacements) replacement.Dispose();
        replacements.Clear();
    }

    private sealed class Replacement : IDisposable
    {
        public readonly WidgetLive2dControl Widget;
        public readonly GameObject Original;
        public readonly CubismVisual Visual;
        private readonly List<Texture2D> textures;
        private readonly CubismMotion? idle;
        private readonly CubismMotion? tap;
        private readonly Dictionary<string, CubismParameter> parameters;
        private readonly Dictionary<string, float> defaults;
        private readonly Dictionary<string, CubismPart> parts;
        private readonly Dictionary<string, float> partDefaults;
        private readonly CubismRenderController renderController;
        private readonly List<(Renderer Renderer, bool ForceRenderingOff)> originalRenderers = new();
        private readonly LAppModelProxy? legacy;
        private readonly bool legacyVisible;
        private readonly float started = Time.unscaledTime;
        private float tappedAt = float.NegativeInfinity;

        public Replacement(WidgetLive2dControl widget, GameObject original, CubismVisual visual,
            List<Texture2D> textures, CubismMotion? idle, CubismMotion? tap)
        {
            Widget = widget; Original = original; Visual = visual; this.textures = textures; this.idle = idle; this.tap = tap;
            parameters = visual.Model.GetComponentsInChildren<CubismParameter>(true).ToDictionary(p => p.name, StringComparer.Ordinal);
            defaults = parameters.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
            parts = visual.Model.GetComponentsInChildren<CubismPart>(true).ToDictionary(p => p.name, StringComparer.Ordinal);
            partDefaults = parts.ToDictionary(p => p.Key, p => p.Value.Opacity, StringComparer.Ordinal);
            renderController = visual.Model.GetComponent<CubismRenderController>();
            legacy = original.GetComponent<LAppModelProxy>();
            legacyVisible = legacy != null && legacy.GetVisible();
        }

        public void HideOriginal()
        {
            foreach (var renderer in Original.GetComponentsInChildren<Renderer>(true))
            {
                originalRenderers.Add((renderer, renderer.forceRenderingOff)); renderer.forceRenderingOff = true;
            }
            if (legacy != null) legacy.SetVisible(false);
        }

        public void KeepOriginalHidden()
        {
            foreach (var pair in originalRenderers) if (pair.Renderer != null) pair.Renderer.forceRenderingOff = true;
            if (legacy != null) legacy.SetVisible(false);
        }

        public void PlayTap() { if (tap != null) tappedAt = Time.unscaledTime; }

        public void Animate()
        {
            var tapTime = Time.unscaledTime - tappedAt;
            foreach (var parameter in parameters) parameter.Value.Value = defaults[parameter.Key];
            foreach (var part in parts) part.Value.Opacity = partDefaults[part.Key];
            renderController.Opacity = 1;
            if (idle != null) ApplyMotion(idle, (Time.unscaledTime - started) % idle.Duration, 1);
            if (tap != null && tapTime < tap.Duration)
                ApplyMotion(tap, tapTime, Math.Min(1, Math.Min(tapTime / .2f, (tap.Duration - tapTime) / .2f)));
        }

        private void ApplyMotion(CubismMotion motion, float time, float weight)
        {
            foreach (var curve in motion.Curves)
            {
                var value = curve.Evaluate(time);
                if (curve.Target == "Parameter" && parameters.TryGetValue(curve.Id, out var parameter))
                    parameter.Value = parameter.GetParameterClampValue(parameter.Value + (value - parameter.Value) * weight);
                else if (curve.Target == "PartOpacity" && parts.TryGetValue(curve.Id, out var part))
                    part.Opacity = Mathf.Clamp01(part.Opacity + (value - part.Opacity) * weight);
                else if (curve.Target == "Model" && curve.Id == "Opacity")
                    renderController.Opacity = Mathf.Clamp01(renderController.Opacity + (value - renderController.Opacity) * weight);
            }
        }

        public void Dispose()
        {
            foreach (var pair in originalRenderers) if (pair.Renderer != null) pair.Renderer.forceRenderingOff = pair.ForceRenderingOff;
            if (legacy != null) legacy.SetVisible(legacyVisible);
            Visual.Dispose();
            foreach (var texture in textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
