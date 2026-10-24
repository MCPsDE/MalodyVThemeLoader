using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MalodyVThemeLoader.Plugin;

// Owned overlay only. Never alters game initialization, scene loading or input.
// BepInEx runs after native engine startup; earlier OS/engine frames are outside its lifetime.
internal sealed class OpeningPresentation : IDisposable
{
    private readonly ManualLogSource log;
    private readonly ThemeOpening rule;
    private GameObject? root;
    private CanvasGroup? group;
    private RectTransform? mediaRect;
    private RawImage? media;
    private Texture2D? image;
    private VideoPlayer? player;
    private RenderTexture? target;
    private float createdAt, shownAt;
    private float aspect;
    private int screenWidth, screenHeight;
    private bool started, playing, disposed, polled;
    internal bool Finished => disposed;

    internal OpeningPresentation(ManualLogSource log, string imageRoot, ThemeOpening rule)
    {
        this.log = log; this.rule = rule; createdAt = Time.realtimeSinceStartup;
        try
        {
            rule.Validate(); var path = PathRules.GetSafeCombinedPath(imageRoot, rule.File);
            // Validate all local media before creating or hiding any original presentation.
            if (!File.Exists(path)) throw new FileNotFoundException("Opening media is missing.", path);
            WebmVideo? videoInfo = null;
            if (rule.Kind == "video") videoInfo = WebmVideo.Inspect(path);
            else
            {
                if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("Opening image exceeds 128 MB.");
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) throw new InvalidDataException("Opening image must be PNG.");
                var w = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                var h = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
                if (w <= 0 || h <= 0 || w > 8192 || h > 8192 || (long)w * h > 16 * 1024 * 1024) throw new InvalidDataException("Opening image dimensions exceed the limit.");
                image = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = SceneNodeLocator.OwnPrefix + "OpeningImage", wrapMode = TextureWrapMode.Clamp };
                Il2CppStructArray<byte> native = bytes;
                if (!ImageConversion.LoadImage(image, native, false)) throw new InvalidDataException("Opening image decode failed.");
                aspect = (float)image.width / image.height;
            }
            root = new GameObject(SceneNodeLocator.OwnPrefix + "Opening"); UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            group = root.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var background = Child("Background").gameObject.AddComponent<Image>(); background.color = TextAppearanceService.Parse(rule.Background); background.raycastTarget = false;
            mediaRect = Child("Media"); media = mediaRect.gameObject.AddComponent<RawImage>(); media.raycastTarget = false;
            if (videoInfo != null)
            {
                aspect = (float)videoInfo.Width / videoInfo.Height;
                target = new RenderTexture(videoInfo.Width, videoInfo.Height, 0, RenderTextureFormat.ARGB32) { name = SceneNodeLocator.OwnPrefix + "OpeningVideo" };
                if (!target.Create()) throw new InvalidDataException("Opening video texture creation failed.");
                media.texture = target;
                player = media.gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.source = VideoSource.Url; player.url = new Uri(path).AbsoluteUri;
                player.isLooping = false; player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = target;
                player.audioOutputMode = VideoAudioOutputMode.None; player.waitForFirstFrame = true; player.skipOnDrop = true; player.Prepare();
            }
            else media.texture = image;
            Fit();
        }
        catch { Dispose(); throw; }
    }
    private RectTransform Child(string name)
    {
        var child = new GameObject(SceneNodeLocator.OwnPrefix + "Opening." + name); var rect = child.AddComponent<RectTransform>(); rect.SetParent(root!.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    internal void Tick()
    {
        if (disposed) return;
        try
        {
            if (root == null || group == null || media == null) { Dispose(); return; }
            var now = Time.realtimeSinceStartup;
            // Count preparation from the first update, after synchronous game initialization.
            if (!polled) { createdAt = now; polled = true; }
            if (!started)
            {
                // Preparation never replaces the original with a blank surface.
                if (now - createdAt > 8) { log.LogWarning("Opening preparation timed out; keeping original presentation."); Dispose(); return; }
                if (player != null)
                {
                    if (!player.isPrepared) return;
                    if (!playing) { player.Play(); playing = true; return; }
                    if (player.frame < 0) return;
                }
                started = true; shownAt = now;
                group.alpha = 1;
                if (!SplashScreen.isFinished) SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
                log.LogInfo("Theme opening started.");
            }
            if (screenWidth != Screen.width || screenHeight != Screen.height) Fit();
            var elapsed = now - shownAt;
            var ending = elapsed >= rule.Duration || player != null && elapsed > .2f && !player.isPlaying;
            if (ending) Dispose();
            else group.alpha = Mathf.Clamp01((rule.Duration - elapsed) / .18f);
        }
        catch (Exception e) { log.LogWarning("Opening stopped: " + e.Message); Dispose(); }
    }
    private void Fit()
    {
        if (mediaRect == null || media == null) return;
        screenWidth = Screen.width; screenHeight = Screen.height;
        mediaRect.anchorMin = Vector2.zero; mediaRect.anchorMax = Vector2.one; mediaRect.offsetMin = mediaRect.offsetMax = Vector2.zero; media.uvRect = new Rect(0, 0, 1, 1);
        var screenAspect = (float)Math.Max(1, screenWidth) / Math.Max(1, screenHeight);
        if (rule.Fit == "contain")
        {
            if (aspect > screenAspect) { var fraction = screenAspect / aspect; mediaRect.anchorMin = new Vector2(0, (1 - fraction) / 2); mediaRect.anchorMax = new Vector2(1, (1 + fraction) / 2); }
            else { var fraction = aspect / screenAspect; mediaRect.anchorMin = new Vector2((1 - fraction) / 2, 0); mediaRect.anchorMax = new Vector2((1 + fraction) / 2, 1); }
        }
        else if (rule.Fit == "cover") media.uvRect = aspect > screenAspect
            ? new Rect((1 - screenAspect / aspect) / 2, 0, screenAspect / aspect, 1)
            : new Rect(0, (1 - aspect / screenAspect) / 2, 1, aspect / screenAspect);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Cleanup(() => { if (group != null) group.alpha = 0; });
        Cleanup(() => { if (player != null) { player.Stop(); player.targetTexture = null; } });
        Cleanup(() => { if (media != null) media.texture = null; });
        Cleanup(() => { if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); } });
        Cleanup(() => { if (target != null) UnityEngine.Object.Destroy(target, .1f); });
        Cleanup(() => { if (image != null) UnityEngine.Object.Destroy(image, .1f); });
        root = null; group = null; mediaRect = null; media = null; player = null; target = null; image = null;
    }
    private static void Cleanup(Action release) { try { release(); } catch { } }
}
