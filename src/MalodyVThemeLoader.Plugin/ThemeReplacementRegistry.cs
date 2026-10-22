using BepInEx.Logging;
using Live2D.Cubism.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal static class ThemeReplacementRegistry
{
    private static readonly Dictionary<int, ImageOriginal> Images = new();
    private static readonly Dictionary<int, RawImageOriginal> RawImages = new();
    private static readonly Dictionary<int, SpriteRendererOriginal> SpriteRenderers = new();
    private static readonly Dictionary<int, CubismRendererOriginal> CubismRenderers = new();
    private static readonly Dictionary<int, TmpTextOriginal> TmpTexts = new();
    private static readonly Dictionary<int, LegacyTextOriginal> LegacyTexts = new();

    public static void TrackImage(Image image)
    {
        var key = image.GetInstanceID();
        var assets = ThemeRuntime.Current?.Assets;
        var type = Images.TryGetValue(key, out var previous) ? previous.Type : image.type;
        Images[key] = new ImageOriginal(image, assets == null ? image.sprite : assets.GetOriginalSprite(image.sprite),
            assets == null ? image.m_OverrideSprite : assets.GetOriginalSprite(image.m_OverrideSprite), type);
    }

    public static void TrackRawImage(RawImage image)
    {
        var key = image.GetInstanceID();
        var assets = ThemeRuntime.Current?.Assets;
        RawImages[key] = new RawImageOriginal(image, assets == null ? image.texture : assets.GetOriginalTexture(image.texture));
    }

    public static void TrackSpriteRenderer(SpriteRenderer renderer)
    {
        var key = renderer.GetInstanceID();
        var assets = ThemeRuntime.Current?.Assets;
        var mode = SpriteRenderers.TryGetValue(key, out var previous) ? previous.DrawMode : renderer.drawMode;
        SpriteRenderers[key] = new SpriteRendererOriginal(renderer, assets == null ? renderer.sprite : assets.GetOriginalSprite(renderer.sprite), mode);
    }

    public static void TrackCubismRenderer(CubismRenderer renderer)
    {
        var key = renderer.GetInstanceID();
        if (!CubismRenderers.ContainsKey(key))
        {
            CubismRenderers[key] = new CubismRendererOriginal(
                renderer, renderer._mainTexture);
        }
    }

    public static void TrackTmpText(TMP_Text text)
    {
        var key = text.GetInstanceID();
        if (!TmpTexts.ContainsKey(key))
        {
            TmpTexts[key] = new TmpTextOriginal(text, text.font);
        }
    }

    public static void TrackLegacyText(Text text)
    {
        var key = text.GetInstanceID();
        if (!LegacyTexts.ContainsKey(key))
        {
            LegacyTexts[key] = new LegacyTextOriginal(text, text.font);
        }
    }

    public static void RestoreAll(ManualLogSource log)
    {
        RestoreImages(log);
        RestoreRawImages(log);
        RestoreSpriteRenderers(log);
        RestoreCubismRenderers(log);
        RestoreTmpTexts(log);
        RestoreLegacyTexts(log);
    }

    private static void RestoreImages(ManualLogSource log)
    {
        foreach (var original in Images.Values)
        {
            try
            {
                if (original.Image != null)
                {
                    original.Image.sprite = original.Sprite;
                    original.Image.overrideSprite = original.OverrideSprite;
                    original.Image.type = original.Type;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning($"Failed to restore themed Image: {exception.Message}");
            }
        }

        Images.Clear();
    }

    private static void RestoreRawImages(ManualLogSource log)
    {
        foreach (var original in RawImages.Values)
        {
            try
            {
                if (original.Image != null)
                {
                    original.Image.texture = original.Texture;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning($"Failed to restore themed RawImage: {exception.Message}");
            }
        }

        RawImages.Clear();
    }

    private static void RestoreSpriteRenderers(ManualLogSource log)
    {
        foreach (var original in SpriteRenderers.Values)
        {
            try
            {
                if (original.Renderer != null)
                {
                    original.Renderer.sprite = original.Sprite;
                    original.Renderer.drawMode = original.DrawMode;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning(
                    $"Failed to restore themed SpriteRenderer: {exception.Message}");
            }
        }

        SpriteRenderers.Clear();
    }

    private static void RestoreCubismRenderers(ManualLogSource log)
    {
        foreach (var original in CubismRenderers.Values)
        {
            try
            {
                if (original.Renderer != null)
                {
                    original.Renderer._mainTexture = original.Texture;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning(
                    $"Failed to restore themed CubismRenderer: {exception.Message}");
            }
        }

        CubismRenderers.Clear();
    }

    private static void RestoreTmpTexts(ManualLogSource log)
    {
        foreach (var original in TmpTexts.Values)
        {
            try
            {
                if (original.Text != null)
                {
                    original.Text.font = original.Font;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning($"Failed to restore themed TMP font: {exception.Message}");
            }
        }

        TmpTexts.Clear();
    }

    private static void RestoreLegacyTexts(ManualLogSource log)
    {
        foreach (var original in LegacyTexts.Values)
        {
            try
            {
                if (original.Text != null)
                {
                    original.Text.font = original.Font;
                }
            }
            catch (Exception exception)
            {
                log.LogWarning(
                    $"Failed to restore themed legacy font: {exception.Message}");
            }
        }

        LegacyTexts.Clear();
    }

    private sealed record ImageOriginal(
        Image Image,
        Sprite? Sprite,
        Sprite? OverrideSprite,
        Image.Type Type);

    private sealed record RawImageOriginal(RawImage Image, Texture? Texture);

    private sealed record SpriteRendererOriginal(
        SpriteRenderer Renderer,
        Sprite? Sprite,
        SpriteDrawMode DrawMode);

    private sealed record CubismRendererOriginal(
        CubismRenderer Renderer,
        Texture2D? Texture);

    private sealed record TmpTextOriginal(TMP_Text Text, TMP_FontAsset? Font);

    private sealed record LegacyTextOriginal(Text Text, Font? Font);
}
