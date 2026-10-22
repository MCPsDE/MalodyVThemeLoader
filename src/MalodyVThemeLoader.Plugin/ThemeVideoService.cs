using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ThemeVideoService
{
    private readonly ManualLogSource log;
    private readonly ThemeAssetStore assets;
    private readonly Dictionary<int, ImageVideoBinding> imageBindings = new();
    private readonly Dictionary<int, RawImageVideoBinding> rawImageBindings = new();
    private readonly Dictionary<int, SpriteRendererVideoBinding> spriteRendererBindings = new();

    public ThemeVideoService(ManualLogSource log, ThemeAssetStore assets)
    {
        this.log = log;
        this.assets = assets;
    }

    public ThemeVideoApplyState ApplyImage(Image image, bool logChange)
    {
        var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
        if (sprite == null)
        {
            return ThemeVideoApplyState.None;
        }

        if (assets.HasImageRule(sprite.name, sprite, null, image.gameObject.scene.name, SceneThemeApplier.GetHierarchy(image.transform), "Image", SceneNodeLocator.AddressOf(image.transform)))
            return ThemeVideoApplyState.None;
        var replacement = assets.ResolveVideo(sprite.name, sprite);
        if (replacement == null)
        {
            return ThemeVideoApplyState.None;
        }

        var key = image.GetInstanceID();
        if (imageBindings.TryGetValue(key, out var current)
            && string.Equals(
                current.FilePath,
                replacement.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            current.EnsurePlaying();
            return ThemeVideoApplyState.Active;
        }

        ClearImageBinding(key);
        ThemeReplacementRegistry.TrackImage(image);

        var targetTexture = CreateTargetTexture(replacement);
        var layer = new GameObject(SceneNodeLocator.OwnPrefix + "LegacyVideo"); layer.layer = image.gameObject.layer;
        var rect = layer.AddComponent<RectTransform>(); rect.SetParent(image.transform, false); rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        layer.AddComponent<LayoutElement>().ignoreLayout = true;
        var player = CreatePlayer(layer, replacement, targetTexture);
        var overlay = layer.AddComponent<RawImage>();
        overlay.texture = targetTexture;
        overlay.color = image.color;
        overlay.raycastTarget = false;
        overlay.maskable = image.maskable;

        var originalEnabled = image.enabled;

        imageBindings[key] = new ImageVideoBinding(
            replacement.FilePath,
            image,
            originalEnabled,
            overlay,
            player,
            targetTexture);
        player.Play();

        if (logChange)
        {
            log.LogInfo(
                $"Theme video applied in '{image.gameObject.scene.name}': "
                + $"{SceneThemeApplier.GetHierarchy(image.transform)} "
                + $"[{replacement.LogicalId}].");
        }

        return ThemeVideoApplyState.Changed;
    }

    public ThemeVideoApplyState ApplyRawImage(RawImage image, bool logChange)
    {
        if (image.texture != null && assets.HasImageRule(image.texture.name, null, image.texture, image.gameObject.scene.name, SceneThemeApplier.GetHierarchy(image.transform), "RawImage", SceneNodeLocator.AddressOf(image.transform)))
            return ThemeVideoApplyState.None;
        var replacement = assets.ResolveVideo(image.texture);
        if (replacement == null)
        {
            return ThemeVideoApplyState.None;
        }

        var key = image.GetInstanceID();
        if (rawImageBindings.TryGetValue(key, out var current)
            && string.Equals(
                current.FilePath,
                replacement.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            current.EnsurePlaying();
            return ThemeVideoApplyState.Active;
        }

        ClearRawImageBinding(key);
        ThemeReplacementRegistry.TrackRawImage(image);

        var originalTexture = image.texture;
        var targetTexture = CreateTargetTexture(replacement);
        var player = CreatePlayer(image.gameObject, replacement, targetTexture);
        image.texture = targetTexture;

        rawImageBindings[key] = new RawImageVideoBinding(
            replacement.FilePath,
            image,
            originalTexture,
            player,
            targetTexture);
        player.Play();

        if (logChange)
        {
            log.LogInfo(
                $"Theme raw video applied in '{image.gameObject.scene.name}': "
                + $"{SceneThemeApplier.GetHierarchy(image.transform)} "
                + $"[{replacement.LogicalId}].");
        }

        return ThemeVideoApplyState.Changed;
    }

    public ThemeVideoApplyState ApplySpriteRenderer(
        SpriteRenderer renderer,
        bool logChange)
    {
        if (renderer.sprite == null || renderer.material == null)
        {
            return ThemeVideoApplyState.None;
        }

        if (assets.HasImageRule(renderer.sprite.name, renderer.sprite, null, renderer.gameObject.scene.name, SceneThemeApplier.GetHierarchy(renderer.transform), "SpriteRenderer", SceneNodeLocator.AddressOf(renderer.transform)))
            return ThemeVideoApplyState.None;
        var replacement = assets.ResolveVideo(renderer.sprite.name, renderer.sprite);
        if (replacement == null)
        {
            return ThemeVideoApplyState.None;
        }

        var key = renderer.GetInstanceID();
        if (spriteRendererBindings.TryGetValue(key, out var current)
            && string.Equals(
                current.FilePath,
                replacement.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            current.EnsurePlaying();
            return ThemeVideoApplyState.Active;
        }

        ClearSpriteRendererBinding(key);
        ThemeReplacementRegistry.TrackSpriteRenderer(renderer);

        var material = renderer.material;
        var originalTexture = material.mainTexture;
        var targetTexture = CreateTargetTexture(replacement);
        var player = CreatePlayer(renderer.gameObject, replacement, targetTexture);
        material.mainTexture = targetTexture;

        spriteRendererBindings[key] = new SpriteRendererVideoBinding(
            replacement.FilePath,
            renderer,
            material,
            originalTexture,
            player,
            targetTexture);
        player.Play();

        if (logChange)
        {
            log.LogInfo(
                $"Theme sprite video applied in '{renderer.gameObject.scene.name}': "
                + $"{SceneThemeApplier.GetHierarchy(renderer.transform)} "
                + $"[{replacement.LogicalId}].");
        }

        return ThemeVideoApplyState.Changed;
    }

    public void StopAll()
    {
        foreach (var key in imageBindings.Keys.ToArray())
        {
            ClearImageBinding(key);
        }

        foreach (var key in rawImageBindings.Keys.ToArray())
        {
            ClearRawImageBinding(key);
        }

        foreach (var key in spriteRendererBindings.Keys.ToArray())
        {
            ClearSpriteRendererBinding(key);
        }
    }

    private static RenderTexture CreateTargetTexture(ThemeVideoReplacement replacement)
    {
        var width = Math.Clamp(replacement.Width, 16, 4096);
        var height = Math.Clamp(replacement.Height, 16, 4096);
        var texture = new RenderTexture(width, height, 0)
        {
            name = "ThemeVideo:" + replacement.TextureName,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();
        return texture;
    }

    private static VideoPlayer CreatePlayer(
        GameObject target,
        ThemeVideoReplacement replacement,
        RenderTexture targetTexture)
    {
        var player = target.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.source = VideoSource.Url;
        player.url = new Uri(replacement.FilePath).AbsoluteUri;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = targetTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.skipOnDrop = true;
        player.waitForFirstFrame = false;
        return player;
    }

    private void ClearImageBinding(int key)
    {
        if (!imageBindings.Remove(key, out var binding))
        {
            return;
        }

        binding.Restore(log);
    }

    private void ClearRawImageBinding(int key)
    {
        if (!rawImageBindings.Remove(key, out var binding))
        {
            return;
        }

        binding.Restore(log);
    }

    private void ClearSpriteRendererBinding(int key)
    {
        if (!spriteRendererBindings.Remove(key, out var binding))
        {
            return;
        }

        binding.Restore(log);
    }

    private sealed record ImageVideoBinding(
        string FilePath,
        Image Image,
        bool OriginalEnabled,
        RawImage Overlay,
        VideoPlayer Player,
        RenderTexture TargetTexture) : VideoBinding(FilePath, Player, TargetTexture)
    {
        public override void Restore(ManualLogSource log)
        {
            try
            {
                if (Image != null)
                {
                    Image.enabled = OriginalEnabled;
                }

                if (Overlay != null)
                {
                    Overlay.gameObject.SetActive(false); UnityEngine.Object.Destroy(Overlay.gameObject);
                }
            }
            catch (Exception exception)
            {
                log.LogWarning($"Failed to restore video Image binding: {exception.Message}");
            }
            finally
            {
                ReleaseVideoObjects();
            }
        }
    }

    private sealed record RawImageVideoBinding(
        string FilePath,
        RawImage Image,
        Texture? OriginalTexture,
        VideoPlayer Player,
        RenderTexture TargetTexture) : VideoBinding(FilePath, Player, TargetTexture)
    {
        public override void Restore(ManualLogSource log)
        {
            try
            {
                if (Image != null)
                {
                    Image.texture = OriginalTexture;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning(
                    $"Failed to restore video RawImage binding: {exception.Message}");
            }
            finally
            {
                ReleaseVideoObjects();
            }
        }
    }

    private sealed record SpriteRendererVideoBinding(
        string FilePath,
        SpriteRenderer Renderer,
        Material Material,
        Texture? OriginalTexture,
        VideoPlayer Player,
        RenderTexture TargetTexture) : VideoBinding(FilePath, Player, TargetTexture)
    {
        public override void Restore(ManualLogSource log)
        {
            try
            {
                if (Material != null)
                {
                    Material.mainTexture = OriginalTexture;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning(
                    "Failed to restore video SpriteRenderer binding: "
                    + exception.Message);
            }
            finally
            {
                ReleaseVideoObjects();
            }
        }
    }

    private abstract record VideoBinding(
        string FilePath,
        VideoPlayer Player,
        RenderTexture TargetTexture)
    {
        public void EnsurePlaying()
        {
            if (Player != null && !Player.isPlaying)
            {
                Player.Play();
            }
        }

        public abstract void Restore(ManualLogSource log);

        protected void ReleaseVideoObjects()
        {
            if (Player != null)
            {
                Player.Stop();
                Player.targetTexture = null;
                UnityEngine.Object.Destroy(Player);
            }

            if (TargetTexture != null)
            {
                TargetTexture.Release();
                UnityEngine.Object.Destroy(TargetTexture);
            }
        }
    }
}

internal enum ThemeVideoApplyState
{
    None,
    Active,
    Changed
}
