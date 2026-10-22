using BepInEx.Logging;
using Live2D.Cubism.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal sealed class SceneThemeApplier
{
    private readonly ManualLogSource log;
    private readonly ThemeAssetStore assets;
    private readonly FontReplacementService fonts;
    private readonly ThemeVideoService videos;
    private string lastLoggedScene = "";

    public SceneThemeApplier(
        ManualLogSource log,
        ThemeAssetStore assets,
        FontReplacementService fonts,
        ThemeVideoService videos)
    {
        this.log = log;
        this.assets = assets;
        this.fonts = fonts;
        this.videos = videos;
    }

    public void ApplyScene(string sceneName)
    {
        if (!IsAllowedScene(sceneName))
        {
            return;
        }

        var changed = 0;
        var imageCount = 0;
        var rawImageCount = 0;
        var spriteRendererCount = 0;
        var tmpTextCount = 0;
        var legacyTextCount = 0;
        var cubismRendererCount = 0;

        foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (!IsSceneComponent(image, sceneName)
                || (image.sprite == null && image.overrideSprite == null))
            {
                continue;
            }

            imageCount++;
            var videoState = videos.ApplyImage(image, true);
            if (videoState == ThemeVideoApplyState.Changed)
            {
                changed++;
                continue;
            }

            if (videoState == ThemeVideoApplyState.Active)
            {
                continue;
            }

            if (ApplyImage(image, true))
            {
                changed++;
            }
        }

        foreach (var rawImage in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (!IsSceneComponent(rawImage, sceneName) || rawImage.texture == null)
            {
                continue;
            }

            rawImageCount++;
            var videoState = videos.ApplyRawImage(rawImage, true);
            if (videoState == ThemeVideoApplyState.Changed)
            {
                changed++;
                continue;
            }

            if (videoState == ThemeVideoApplyState.Active)
            {
                continue;
            }

            var replacement = assets.ResolveTexture(rawImage.texture, rawImage.gameObject.scene.name, GetHierarchy(rawImage.transform));
            if (replacement != null && rawImage.texture != replacement)
            {
                ThemeReplacementRegistry.TrackRawImage(rawImage);
                var originalName = rawImage.texture.name;
                rawImage.texture = replacement;
                log.LogInfo(
                    $"Theme raw image applied in '{rawImage.gameObject.scene.name}': "
                    + $"{GetHierarchy(rawImage.transform)} [{originalName}].");
                changed++;
            }
        }

        foreach (var renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (!IsSceneComponent(renderer, sceneName) || renderer.sprite == null)
            {
                continue;
            }

            spriteRendererCount++;
            var videoState = videos.ApplySpriteRenderer(renderer, true);
            if (videoState == ThemeVideoApplyState.Changed)
            {
                changed++;
                continue;
            }

            if (videoState == ThemeVideoApplyState.Active)
            {
                continue;
            }

            var hierarchy = GetHierarchy(renderer.transform);
            var replacement = assets.ResolveSprite(renderer.sprite.name, renderer.sprite, renderer.gameObject.scene.name, hierarchy, "SpriteRenderer");
            var imageMode = replacement == null ? "" : assets.GetImageMode(replacement, renderer.gameObject.scene.name, hierarchy, "SpriteRenderer");
            var desiredMode = imageMode.Length == 0 ? renderer.drawMode : imageMode == "sliced" ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            if (replacement != null && (renderer.sprite != replacement || renderer.drawMode != desiredMode))
            {
                ThemeReplacementRegistry.TrackSpriteRenderer(renderer);
                var originalName = renderer.sprite.name;
                renderer.sprite = replacement;
                renderer.drawMode = desiredMode;
                log.LogInfo(
                    $"Theme sprite renderer applied in '{renderer.gameObject.scene.name}': "
                    + $"{GetHierarchy(renderer.transform)} [{originalName}].");
                changed++;
            }
        }

        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!IsSceneComponent(text, sceneName))
            {
                continue;
            }

            tmpTextCount++;
            var replacement = fonts.ResolveTmpFont(text.font);
            if (replacement != null && text.font != replacement)
            {
                ThemeReplacementRegistry.TrackTmpText(text);
                text.font = replacement;
                changed++;
            }
        }

        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (!IsSceneComponent(text, sceneName))
            {
                continue;
            }

            legacyTextCount++;
            var replacement = fonts.ResolveLegacyFont(text.font);
            if (replacement != null && text.font != replacement)
            {
                ThemeReplacementRegistry.TrackLegacyText(text);
                text.font = replacement;
                changed++;
            }
        }

        foreach (var renderer in Resources.FindObjectsOfTypeAll<CubismRenderer>())
        {
            if (!IsSceneComponent(renderer, sceneName) || renderer._mainTexture == null)
            {
                continue;
            }

            cubismRendererCount++;
            var replacement = assets.ResolveCubismTexture(
                renderer._mainTexture, GetHierarchy(renderer.transform));
            if (replacement != null && renderer._mainTexture != replacement)
            {
                ThemeReplacementRegistry.TrackCubismRenderer(renderer);
                renderer._mainTexture = replacement;
                changed++;
            }
        }

        if (changed > 0 || !string.Equals(lastLoggedScene, sceneName, StringComparison.Ordinal))
        {
            lastLoggedScene = sceneName;
            log.LogInfo(
                $"Theme scan '{sceneName}': {changed} replacements; "
                + $"{imageCount} Images, {rawImageCount} RawImages, "
                + $"{spriteRendererCount} SpriteRenderers, {tmpTextCount} TMP texts, "
                + $"{legacyTextCount} legacy texts, {cubismRendererCount} Cubism renderers.");
        }
    }

    public bool ApplyImage(Image image, bool logChange = false)
    {
        if (!IsSceneComponent(image)
            || !IsAllowedScene(image.gameObject.scene.name))
        {
            return false;
        }

        var videoState = videos.ApplyImage(image, logChange);
        if (videoState == ThemeVideoApplyState.Changed)
        {
            return true;
        }

        if (videoState == ThemeVideoApplyState.Active)
        {
            return false;
        }

        var changed = false;
        if (ApplyImageSprite(
                image,
                image.sprite,
                replacement => image.sprite = replacement,
                "image",
                logChange))
        {
            changed = true;
        }

        if (ApplyImageSprite(
                image,
                image.m_OverrideSprite,
                replacement => image.overrideSprite = replacement,
                "image override",
                logChange))
        {
            changed = true;
        }

        return changed;
    }

    private bool ApplyImageSprite(
        Image image,
        Sprite? original,
        Action<Sprite> setSprite,
        string label,
        bool logChange)
    {
        if (original == null)
        {
            return false;
        }

        var hierarchy = GetHierarchy(image.transform);
        var replacement = assets.ResolveSprite(original.name, original, image.gameObject.scene.name, hierarchy, "Image");
        if (replacement == null)
        {
            return false;
        }
        var mode = assets.GetImageMode(replacement, image.gameObject.scene.name, hierarchy, "Image");
        var desiredType = mode.Length == 0 ? image.type : mode == "sliced" ? Image.Type.Sliced : Image.Type.Simple;
        if (original == replacement && desiredType == image.type) return false;

        ThemeReplacementRegistry.TrackImage(image);
        setSprite(replacement);
        image.type = desiredType;
        if (logChange)
        {
            log.LogInfo(
                $"Theme {label} applied in '{image.gameObject.scene.name}': "
                + $"{GetHierarchy(image.transform)} [{original.name}].");
        }

        return true;
    }

    internal static bool IsAllowedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return !sceneName.Equals("ScenePlay", StringComparison.OrdinalIgnoreCase)
               && !sceneName.Equals("SceneReplay", StringComparison.OrdinalIgnoreCase)
               && !sceneName.Equals("SceneRoomWatch", StringComparison.OrdinalIgnoreCase)
               && !sceneName.Equals("SceneWatchSub", StringComparison.OrdinalIgnoreCase)
               && !sceneName.StartsWith("PlayMode", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSceneComponent(Component component, string? sceneName = null)
    {
        if (component == null || component.gameObject == null)
        {
            return false;
        }

        var scene = component.gameObject.scene;
        if (!scene.IsValid())
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(sceneName)
               || string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetHierarchy(Transform? transform)
    {
        var parts = new List<string>();
        var current = transform;
        while (current != null && parts.Count < 96)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
