using System.Text.Json;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Malody.Live2d;
using Malody.Scene.Widget;
using Malody.Scene;
using Malody.Scene.Panel;
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
    private bool updateFailed;
    private readonly Dictionary<int, (SceneResult Owner, string Result)> pendingResults = new();
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
        if (binding == null || updateFailed) return;
        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var replacement = replacements[i];
            if (replacement.Original == null || replacement.Widget == null || replacement.Visual.Model == null)
            {
                replacement.Dispose();
                replacements.RemoveAt(i);
                continue;
            }
            if (!SceneThemeApplier.IsAllowedScene(replacement.Widget.gameObject.scene.name) || !replacement.Widget.gameObject.activeInHierarchy)
            { replacement.StopAudio(); continue; }
            replacement.Animate();
            replacement.KeepOriginalHidden();
            replacement.RefreshClickSurface();
        }
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .4f;
        foreach (var panel in SceneComponentQuery.Find<PanelResultScore>("SceneResult")) ObserveResult(panel);
        if (replacements.Count == 0 && failed.Count == 0) Status("waiting", "等待游戏显示看板角色。");
        foreach (var widget in SceneComponentQuery.Find<WidgetLive2dControl>())
            ApplyNow(widget);
    }

    // Widget.Start has already created the game's visual, and no camera has
    // rendered it yet. Show can also reactivate an existing widget. Neither
    // lifecycle event may wait for the bounded fallback discovery timer.
    public void ApplyNow(WidgetLive2dControl widget)
    {
        if (binding == null || updateFailed || widget == null) return;
        try
        {
            if (!SceneThemeApplier.IsSceneComponent(widget) || !widget.gameObject.activeInHierarchy
                || !SceneThemeApplier.IsAllowedScene(widget.gameObject.scene.name) || widget.container == null) return;
            for (var index = replacements.Count - 1; index >= 0; index--)
            {
                var previous = replacements[index];
                if (previous.Original != null && previous.Widget != null && previous.Visual.Model != null) continue;
                previous.Dispose();
                replacements.RemoveAt(index);
            }
            // Read the already-instantiated visual only. Never select/instantiate
            // a game character or invoke MarloManage, purchases or notifications.
            var original = FindInstantiatedVisual(widget);
            if (original == null || failed.Contains(original.GetInstanceID())) return;
            var existing = replacements.FirstOrDefault(replacement => replacement.Original == original);
            if (existing != null)
            {
                existing.KeepOriginalHidden();
                ApplyPendingResult(existing);
                return;
            }
            try
            {
                var replacement = Create(widget, original);
                replacements.Add(replacement);
                ApplyPendingResult(replacement);
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
        catch (Exception exception) { ReportUpdateFailure(exception); }
    }

    public void PlayTap(WidgetLive2dControl widget, UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (binding == null) return;
        try
        {
            foreach (var replacement in replacements.Where(r => r.Widget == widget))
            {
                var file = binding.TapMotion;
                if (Live2dClickSurface.TryModelPoint(replacement.Visual.Model.transform, replacement.Visual.LocalBounds,
                    eventData.pressEventCamera, eventData.position, out var point))
                    file = Live2dEvents.ClickMotion(binding, point.x, point.y);
                replacement.Play(file);
            }
        }
        catch (Exception e) { ReportUpdateFailure(e); }
    }

    // Observe only the completed visual panel. No Score, PlayInfo, submissions,
    // judgement or result calculation is patched, called or modified here.
    public void ObserveResult(PanelResultScore panel)
    {
        if (binding == null || updateFailed || panel == null || !SceneThemeApplier.IsSceneComponent(panel, "SceneResult")
            || !panel.gameObject.activeInHierarchy || panel.rankLayers == null || panel.rankLayers.Count < 3) return;
        try
        {
            var rankImage = panel.rankLayers[2];
            if (rankImage == null || !rankImage.gameObject.activeSelf || rankImage.sprite == null) return;
            var result = Live2dEvents.ResultFromSprite(rankImage.sprite.name);
            if (result.Length == 0)
            {
                var original = ThemeRuntime.Current?.Assets.GetOriginalSprite(rankImage.sprite);
                if (original != null) result = Live2dEvents.ResultFromSprite(original.name);
            }
            if (result.Length == 0) return;
            foreach (var scene in SceneComponentQuery.Find<SceneResult>("SceneResult"))
            {
                if (!SceneThemeApplier.IsSceneComponent(scene, panel.gameObject.scene.name)
                    || scene.gameObject.scene.handle != panel.gameObject.scene.handle || scene.marloControl == null) continue;
                pendingResults[scene.gameObject.scene.handle] = (scene, result);
                ApplyNow(scene.marloControl);
            }
        }
        catch (Exception e) { ReportUpdateFailure(e); }
    }

    private void ApplyPendingResult(Replacement replacement)
    {
        if (binding == null || !pendingResults.TryGetValue(replacement.Widget.gameObject.scene.handle, out var pending)
            || pending.Owner == null) return;
        if (replacement.ResultAnimation.Apply(pending.Result, binding.ResultMotions, replacement.Play))
            log.LogInfo($"Live2D result animation: {pending.Result} -> {binding.ResultMotions[pending.Result]}");
    }

    private GameObject? FindInstantiatedVisual(WidgetLive2dControl widget)
    {
        GameObject? found = null;
        var container = widget.container;
        for (var index = 0; index < container.childCount; index++)
        {
            var candidate = container.GetChild(index).gameObject;
            if (!candidate.activeSelf || replacements.Any(replacement => replacement.Visual.Model != null
                && replacement.Visual.Model.gameObject == candidate)) continue;
            if (candidate.GetComponentInChildren<LAppModelProxy>(true) == null
                && candidate.GetComponentInChildren<CubismModel>(true) == null) continue;
            if (found != null) throw new InvalidDataException("看板区域存在多个模型，无法确定替换目标。");
            found = candidate;
        }
        return found;
    }

    public void ReportUpdateFailure(Exception exception)
    {
        if (updateFailed) return;
        updateFailed = true;
        try { Dispose(); }
        catch (Exception restoreError) { log.LogError($"Live2D restore after failure: {restoreError}"); }
        Status("error", exception is MissingMethodException or TypeLoadException
            ? "加载器与当前游戏接口不兼容，请更新加载器。"
            : exception.Message);
        log.LogError($"Live2D updates stopped after a runtime failure. Reapply the theme to retry: {exception}");
    }

    private Replacement Create(WidgetLive2dControl widget, GameObject original)
    {
        var document = Live2dModelDocument.Read(theme.Live2dDirectory, binding!.Model);
        // Finish managed decoding before constructing the Unity model. Voice
        // playback itself is lazy and cannot fail its creation transaction.
        var audioData = binding.MotionAudio.Where(pair => pair.Value.Length > 0).ToDictionary(pair => pair.Key,
            pair => Live2dAudio.Decode(Live2dModelDocument.Resolve(theme.Live2dDirectory, pair.Value)), StringComparer.OrdinalIgnoreCase);
        var textures = new List<Texture2D>();
        CubismVisual? visual = null;
        Replacement? prepared = null;
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
                LoadMotion(binding.IdleMotion), new[] { binding.TapMotion }.Concat(binding.ClickRegions.Select(region => region.Motion))
                    .Concat(binding.ResultMotions.Values).Where(file => file.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(file => file, file => LoadMotion(file)!, StringComparer.OrdinalIgnoreCase), binding.IdleMotion,
                audioData, log);
            prepared = replacement;
            replacement.Animate();
            // Commit only after the replacement has valid geometry, materials,
            // textures and animation. Game fields still refer to its own model.
            CubismVisualFactory.Show(visual);
            replacement.HideOriginal();
            replacement.RefreshClickSurface();
            replacement.StartIdleAudio();
            return replacement;
        }
        catch
        {
            if (prepared != null) prepared.Dispose();
            else
            {
                visual?.Dispose();
                foreach (var texture in textures) UnityEngine.Object.DestroyImmediate(texture);
            }
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
        pendingResults.Clear();
    }

    private sealed class Replacement : IDisposable
    {
        public readonly WidgetLive2dControl Widget;
        public readonly GameObject Original;
        public readonly CubismVisual Visual;
        private readonly List<Texture2D> textures;
        private readonly CubismMotion? idle;
        private readonly Dictionary<string, CubismMotion> actions;
        private readonly AnimationAudioPlayer audio;
        private readonly Live2dClickSurface? clickSurface;
        private readonly string idleFile;
        private CubismMotion? action;
        public readonly Live2dResultAnimation ResultAnimation = new();
        private readonly Dictionary<string, CubismParameter> parameters;
        private readonly Dictionary<string, float> defaults;
        private readonly Dictionary<string, CubismPart> parts;
        private readonly Dictionary<string, float> partDefaults;
        private readonly CubismRenderController renderController;
        private readonly List<(Renderer Renderer, bool ForceRenderingOff)> originalRenderers = new();
        private readonly LAppModelProxy? legacy;
        private readonly bool legacyVisible;
        private float idleAt = Time.unscaledTime;
        private float actionAt = float.NegativeInfinity;

        public Replacement(WidgetLive2dControl widget, GameObject original, CubismVisual visual,
            List<Texture2D> textures, CubismMotion? idle, Dictionary<string, CubismMotion> actions, string idleFile,
            Dictionary<string, Live2dAudio> audioData, ManualLogSource log)
        {
            Widget = widget; Original = original; Visual = visual; this.textures = textures; this.idle = idle; this.actions = actions;
            parameters = visual.Model.GetComponentsInChildren<CubismParameter>(true).ToDictionary(p => p.name, StringComparer.Ordinal);
            defaults = parameters.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
            parts = visual.Model.GetComponentsInChildren<CubismPart>(true).ToDictionary(p => p.name, StringComparer.Ordinal);
            partDefaults = parts.ToDictionary(p => p.Key, p => p.Value.Opacity, StringComparer.Ordinal);
            renderController = visual.Model.GetComponent<CubismRenderController>();
            legacy = original.GetComponent<LAppModelProxy>();
            legacyVisible = legacy != null && legacy.GetVisible();
            this.idleFile = idleFile;
            audio = new AnimationAudioPlayer(log, audioData);
            var graphic = widget.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null && graphic.raycastTarget)
                clickSurface = new Live2dClickSurface(graphic, visual.Model.transform, visual.LocalBounds);
        }

        public void RefreshClickSurface() => clickSurface?.Refresh();

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

        public bool Play(string file)
        {
            if (string.IsNullOrEmpty(file) || !actions.TryGetValue(file, out var motion)) return false;
            action = motion; actionAt = Time.unscaledTime; audio.Play(file);
            return true;
        }

        public void StartIdleAudio() { idleAt = Time.unscaledTime; audio.Stop(); if (idle != null) audio.Play(idleFile); }
        public void StopAudio() => audio.Stop();

        public void Animate()
        {
            var actionTime = Time.unscaledTime - actionAt;
            if (action != null && actionTime >= action.Duration) { action = null; StartIdleAudio(); }
            foreach (var parameter in parameters) parameter.Value.Value = defaults[parameter.Key];
            foreach (var part in parts) part.Value.Opacity = partDefaults[part.Key];
            renderController.Opacity = 1;
            if (idle != null) ApplyMotion(idle, (Time.unscaledTime - idleAt) % idle.Duration, 1);
            if (action != null && actionTime < action.Duration)
                ApplyMotion(action, actionTime, Math.Min(1, Math.Min(actionTime / .2f, (action.Duration - actionTime) / .2f)));
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
            clickSurface?.Dispose();
            audio.Dispose();
            foreach (var pair in originalRenderers) if (pair.Renderer != null) pair.Renderer.forceRenderingOff = pair.ForceRenderingOff;
            if (legacy != null) legacy.SetVisible(legacyVisible);
            Visual.Dispose();
            foreach (var texture in textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
