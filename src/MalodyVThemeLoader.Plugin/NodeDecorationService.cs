using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MalodyVThemeLoader.Plugin;

internal sealed class NodeDecorationService : IDisposable
{
    private readonly ManualLogSource log;
    private readonly LoadedTheme theme;
    private readonly ThemeResources rules;
    private readonly SceneNodeTargets targetsQuery;
    private readonly Dictionary<string, Decoration> active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> states = new(StringComparer.Ordinal);
    private float nextScan;
    internal NodeDecorationService(ManualLogSource log, LoadedTheme theme, ThemeResources rules)
    { this.log = log; this.theme = theme; this.rules = rules; targetsQuery = new SceneNodeTargets(rules.Nodes); }
    internal string Status(string scene, string address) => states.TryGetValue(scene + "|" + address, out var state) ? state : "";
    internal void Tick()
    {
        foreach (var pair in active.ToArray())
        {
            try { pair.Value.Tick(); }
            catch (Exception e) { pair.Value.Dispose(); active.Remove(pair.Key); states[pair.Key] = "加载失败：" + e.Message; log.LogWarning("Node decoration stopped: " + e); }
        }
        if (rules.Nodes.Count == 0 || Time.realtimeSinceStartup < nextScan) return;
        nextScan = Time.realtimeSinceStartup + .5f;
        var nodes = targetsQuery.Resolve();
        var targets = nodes.ToLookup(n => n.Info.Scene + "|" + n.Info.Address, StringComparer.Ordinal);
        foreach (var key in active.Where(p => p.Value.Target == null || !targets[p.Key].Any(n => n.Transform == p.Value.Target)).Select(p => p.Key).ToArray())
        { active[key].Dispose(); active.Remove(key); states.Remove(key); }
        foreach (var rule in rules.Nodes)
        {
            var key = rule.Scene + "|" + rule.Address;
            if (active.ContainsKey(key)) { states[key] = active[key].Status; continue; }
            var candidates = targets[key].ToList();
            if (candidates.Count != 1) { states[key] = candidates.Count == 0 ? "等待节点出现" : "节点不唯一，未应用"; continue; }
            if (!candidates[0].Info.CanDecorate) { states[key] = "此节点没有 UI 显示区域"; continue; }
            // Failed assets retry only after a theme reload, never every frame.
            if (states.TryGetValue(key, out var failed) && failed.StartsWith("加载失败", StringComparison.Ordinal)) continue;
            try { var decoration = new Decoration(theme.ImageDirectory, candidates[0].Transform, rule); active[key] = decoration; states[key] = decoration.Status; }
            catch (Exception e) { states[key] = "加载失败：" + e.Message; log.LogWarning($"Node decoration {rule.Scene}/{rule.Hierarchy}: {e}"); }
        }
    }
    public void Dispose() { foreach (var value in active.Values) value.Dispose(); active.Clear(); states.Clear(); }

    private sealed class Decoration : IDisposable
    {
        internal Transform Target { get; }
        internal string Status { get; private set; } = "已应用";
        private readonly ThemeNodeRule rule;
        private GameObject? root;
        private RectTransform? rect;
        private Image? image;
        private RawImage? video;
        private VideoPlayer? player;
        private RenderTexture? renderTexture;
        private Texture2D? texture;
        private Sprite? sprite;
        private readonly bool sibling;
        private float prepareAt;
        private bool prepareRequested;
        private Rect spriteRect;
        private bool playing;
        private bool disposed;
        internal Decoration(string imageRoot, Transform target, ThemeNodeRule rule)
        {
            Target = target; this.rule = rule;
            try
            {
                var source = target.GetComponent<RectTransform>();
                if (source == null) throw new InvalidDataException("节点没有 UI 区域。");
                var graphic = target.GetComponent<Graphic>();
                sibling = graphic != null && target.GetComponent<Image>() == null && target.GetComponent<RawImage>() == null;
                root = new GameObject(SceneNodeLocator.OwnPrefix + "Decoration");
                root.SetActive(false); root.layer = target.gameObject.layer;
                rect = root.AddComponent<RectTransform>(); rect.SetParent(sibling ? target.parent : target, false);
                root.AddComponent<LayoutElement>().ignoreLayout = true;
                if (sibling) rect.SetSiblingIndex(target.GetSiblingIndex()); else rect.SetAsFirstSibling();
                if (rule.ImageFile.Length > 0)
                {
                    var path = PathRules.GetSafeCombinedPath(imageRoot, rule.ImageFile);
                    if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("底图过大。");
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = SceneNodeLocator.OwnPrefix + "Background", wrapMode = TextureWrapMode.Clamp };
                    Il2CppStructArray<byte> bytes = File.ReadAllBytes(path);
                    if (!ImageConversion.LoadImage(texture, bytes, false)) throw new InvalidDataException("无法读取底图。");
                    var border = rule.Border == null ? Vector4.zero : new Vector4(rule.Border.X, rule.Border.Y, rule.Border.Z, rule.Border.W);
                    if (border.x + border.z > texture.width || border.y + border.w > texture.height) throw new InvalidDataException("切片边距超过底图尺寸。");
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
                    spriteRect = sprite.rect;
                    image = Child("Background").gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
                    image.type = rule.Border != null ? Image.Type.Sliced : Image.Type.Simple;
                }
                if (rule.VideoFile.Length > 0)
                {
                    var path = PathRules.GetSafeCombinedPath(imageRoot, rule.VideoFile); var info = WebmVideo.Inspect(path);
                    renderTexture = new RenderTexture(info.Width, info.Height, 0, RenderTextureFormat.ARGB32) { name = SceneNodeLocator.OwnPrefix + "Video", wrapMode = TextureWrapMode.Clamp };
                    if (!renderTexture.Create()) throw new InvalidDataException("无法创建视频纹理。");
                    video = Child("Video").gameObject.AddComponent<RawImage>(); video.texture = renderTexture; video.color = new Color(1, 1, 1, rule.VideoOpacity); video.raycastTarget = false; video.enabled = false;
                    player = video.gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.source = VideoSource.Url; player.url = new Uri(path).AbsoluteUri;
                    player.isLooping = true; player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = renderTexture; player.audioOutputMode = VideoAudioOutputMode.None;
                    player.waitForFirstFrame = true; player.skipOnDrop = true;
                    // Poll preparation below; no managed delegates cross the IL2CPP lifetime boundary.
                    Status = "视频准备中";
                }
                Tick();
            }
            catch { Dispose(); throw; }
        }
        private RectTransform Child(string name)
        {
            var child = new GameObject(SceneNodeLocator.OwnPrefix + name); child.layer = root!.layer;
            var transform = child.AddComponent<RectTransform>(); transform.SetParent(rect, false);
            transform.anchorMin = Vector2.zero; transform.anchorMax = Vector2.one; transform.offsetMin = transform.offsetMax = Vector2.zero;
            return transform;
        }
        internal void Tick()
        {
            if (disposed || Target == null || root == null || rect == null) return;
            if (Target.gameObject.scene.name != rule.Scene || !SceneThemeApplier.IsAllowedScene(Target.gameObject.scene.name)) { root.SetActive(false); return; }
            var source = Target.GetComponent<RectTransform>(); if (source == null) return;
            var visible = Target.gameObject.activeInHierarchy;
            root.SetActive(visible);
            if (sibling)
            {
                if (rect.parent != Target.parent) rect.SetParent(Target.parent, false);
                // A sibling remains immediately behind its target even if the
                // game reorders panels, without taking part in layout groups.
                var ownIndex = rect.GetSiblingIndex(); var targetIndex = Target.GetSiblingIndex();
                if (ownIndex != targetIndex - 1) rect.SetSiblingIndex(targetIndex - (ownIndex < targetIndex ? 1 : 0));
                rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
                rect.anchoredPosition3D = source.anchoredPosition3D; rect.sizeDelta = source.sizeDelta;
                rect.localRotation = source.localRotation; rect.localScale = source.localScale;
            }
            else { if (rect.GetSiblingIndex() != 0) rect.SetAsFirstSibling(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            if (rule.Width > 0) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rule.Width);
            if (rule.Height > 0) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rule.Height);
            if (image != null && texture != null) Fit(image.rectTransform, (float)texture.width / texture.height);
            if (video != null && renderTexture != null) Fit(video.rectTransform, (float)renderTexture.width / renderTexture.height);
            if (player == null) return;
            if (!visible) { if (playing) player.Pause(); playing = false; prepareRequested = false; return; }
            if (!player.isPrepared && !prepareRequested) { player.Prepare(); prepareAt = Time.realtimeSinceStartup; prepareRequested = true; }
            if (!player.isPrepared && prepareRequested && Time.realtimeSinceStartup - prepareAt > 15)
            {
                Status = "视频加载失败，底图保留"; video!.enabled = false; player.Stop(); UnityEngine.Object.Destroy(player); player = null; return;
            }
            if (!player.isPrepared) return;
            if (visible && !playing) { player.Play(); playing = true; }
            else if (!visible && playing) { player.Pause(); playing = false; }
            if (player.frame >= 0) { video!.enabled = true; Status = "已应用"; }
        }
        private void Fit(RectTransform child, float aspect)
        {
            child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one; child.offsetMin = child.offsetMax = Vector2.zero;
            if (rect!.rect.width <= 0 || rect.rect.height <= 0 || rule.Border != null && image != null && child == image.rectTransform) return;
            var targetAspect = rect.rect.width / rect.rect.height;
            if (rule.Fit == "contain")
            {
                child.anchorMin = child.anchorMax = new Vector2(.5f, .5f); child.anchoredPosition = Vector2.zero;
                child.sizeDelta = aspect > targetAspect ? new Vector2(rect.rect.width, rect.rect.width / aspect) : new Vector2(rect.rect.height * aspect, rect.rect.height);
            }
            if (video != null && child == video.rectTransform)
            {
                video.uvRect = rule.Fit != "cover" ? new Rect(0, 0, 1, 1) : aspect > targetAspect
                    ? new Rect((1 - targetAspect / aspect) / 2, 0, targetAspect / aspect, 1)
                    : new Rect(0, (1 - aspect / targetAspect) / 2, 1, aspect / targetAspect);
            }
            if (image != null && child == image.rectTransform && rule.Fit == "cover")
            {
                // Image uses a cropped sprite, preserving the normal stencil and
                // RectMask path instead of spilling an oversized child outward.
                var crop = aspect > targetAspect ? new Rect((texture!.width - texture.height * targetAspect) / 2, 0, texture.height * targetAspect, texture.height)
                    : new Rect(0, (texture!.height - texture.width / targetAspect) / 2, texture.width, texture.width / targetAspect);
                if (sprite != null && spriteRect != crop)
                {
                    var previous = sprite;
                    sprite = Sprite.Create(texture, crop, new Vector2(.5f, .5f), 100); spriteRect = crop; image.sprite = sprite;
                    UnityEngine.Object.Destroy(previous);
                }
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            // Disconnect consumers before retiring shared native resources. Release()
            // frees the GPU surface immediately, while UI/video destruction is deferred.
            Cleanup(() => { if (player != null) { player.Stop(); player.targetTexture = null; } });
            Cleanup(() => { if (image != null) image.sprite = null; });
            Cleanup(() => { if (video != null) video.texture = null; });
            Cleanup(() => { if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); } });
            Cleanup(() => { if (renderTexture != null) UnityEngine.Object.Destroy(renderTexture, .1f); });
            Cleanup(() => { if (sprite != null) UnityEngine.Object.Destroy(sprite, .1f); });
            Cleanup(() => { if (texture != null) UnityEngine.Object.Destroy(texture, .1f); });
            root = null; rect = null; player = null; image = null; video = null;
            renderTexture = null; sprite = null; texture = null;
        }
        private static void Cleanup(Action release)
        {
            try { release(); }
            catch (Exception e) { Plugin.Log.LogDebug("Node decoration cleanup: " + e.Message); }
        }
    }
}
