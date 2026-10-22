using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal sealed partial class ThemeAssetStore
{
    internal ThemeResources ResourceRules { get; }
    private readonly Dictionary<int, SpriteOrigin> spriteOrigins = new();
    private readonly Dictionary<int, TextureOrigin> textureOrigins = new();
    private readonly HashSet<string> imageFailures = new(StringComparer.OrdinalIgnoreCase);

    public Sprite? GetOriginalSprite(Sprite? sprite) => sprite != null && spriteOrigins.TryGetValue(sprite.GetInstanceID(), out var origin) ? origin.Sprite : sprite;
    public Texture? GetOriginalTexture(Texture? texture) => texture != null && textureOrigins.TryGetValue(texture.GetInstanceID(), out var origin) ? origin.Texture : texture;

    private ImageCatalogEntry? Identify(string name, Sprite? sprite, Texture? texture, string scene, string hierarchy, string component)
    {
        if (sprite != null && spriteOrigins.TryGetValue(sprite.GetInstanceID(), out var source)) return source.Entry;
        if (texture != null && textureOrigins.TryGetValue(texture.GetInstanceID(), out var textureSource)) return textureSource.Entry;
        if (!imagesByRuntimeName.TryGetValue(NormalizeRuntimeName(name), out var candidates)) return null;
        if (candidates.Count == 1) return candidates[0];
        if (scene.Length > 0 && hierarchy.Length > 0)
        {
            var byUsage = candidates.Where(e => e.Usages.Any(u => u.Scene == scene && u.Component == component && ThemeResources.MatchHierarchy(u.Hierarchy, hierarchy))).ToList();
            if (byUsage.Count == 1) return byUsage[0];
        }
        var width = sprite != null ? sprite.rect.width : texture != null ? texture.width : 0;
        var height = sprite != null ? sprite.rect.height : texture != null ? texture.height : 0;
        var bySize = candidates.Where(e => width > 0 && height > 0 && Math.Abs(e.Width - width) < .5f && Math.Abs(e.Height - height) < .5f).ToList();
        return bySize.Count == 1 ? bySize[0] : null;
    }

    public bool HasImageRule(string name, Sprite? sprite, Texture? texture, string scene, string hierarchy, string component)
    {
        var entry = Identify(name, sprite, texture, scene, hierarchy, component);
        return entry != null && ResourceRules.ResolveImage(entry.LogicalId, scene, hierarchy, component) != null;
    }

    public string GetImageMode(Sprite sprite, string scene, string hierarchy, string component)
    {
        var entry = Identify(sprite.name, sprite, null, scene, hierarchy, component);
        return entry == null ? "" : ResourceRules.ResolveImage(entry.LogicalId, scene, hierarchy, component)?.Mode ?? "";
    }

    public Sprite? ResolveSprite(string requestedName, Sprite? original, string scene = "", string hierarchy = "", string component = "")
    {
        if (scene.Length > 0 && !ThemeResources.IsAllowedScene(scene)) return null;
        var entry = Identify(requestedName, original, null, scene, hierarchy, component);
        if (entry == null) return null;
        var rule = ResourceRules.ResolveImage(entry.LogicalId, scene, hierarchy, component);
        var path = PathRules.GetSafeCombinedPath(theme.ImageDirectory, string.IsNullOrEmpty(rule?.File) ? entry.LogicalId : rule.File);
        if (!File.Exists(path) || rule == null && !IsImageOverride(entry, path)) return null;
        var sourceSprite = GetOriginalSprite(original);
        var texture = LoadTexture(path, entry.TextureName);
        if (texture == null) return null;
        var border = rule?.Border != null ? new Vector4(rule.Border.X, rule.Border.Y, rule.Border.Z, rule.Border.W)
            : sourceSprite != null ? sourceSprite.border : new Vector4(entry.Border.X, entry.Border.Y, entry.Border.Z, entry.Border.W);
        if (rule?.Border != null && (border.x + border.z > texture.width || border.y + border.w > texture.height))
        {
            if (imageFailures.Add(path)) log.LogWarning($"Slice borders exceed image dimensions; keeping original: {entry.LogicalId}");
            return null;
        }
        var cacheKey = entry.LogicalId + "|" + path + "|" + GetSpriteShapeKey(sourceSprite) + "|" + border;
        if (spriteCache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
        var pivot = sourceSprite != null && sourceSprite.rect.width > 0 && sourceSprite.rect.height > 0
            ? new Vector2(sourceSprite.pivot.x / sourceSprite.rect.width, sourceSprite.pivot.y / sourceSprite.rect.height)
            : new Vector2(entry.Pivot.X, entry.Pivot.Y);
        var ppu = sourceSprite != null && sourceSprite.pixelsPerUnit > 0 ? sourceSprite.pixelsPerUnit : entry.PixelsPerUnit;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        sprite.name = sourceSprite != null ? sourceSprite.name : entry.RuntimeName;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        spriteCache[cacheKey] = sprite;
        spriteOrigins[sprite.GetInstanceID()] = new SpriteOrigin(sourceSprite, entry);
        return sprite;
    }

    public Texture2D? ResolveTexture(Texture? original, string scene = "", string hierarchy = "", string component = "RawImage")
    {
        if (original == null || scene.Length > 0 && !ThemeResources.IsAllowedScene(scene)) return null;
        var entry = Identify(original.name, null, original, scene, hierarchy, component);
        if (entry == null) return null;
        var rule = ResourceRules.ResolveImage(entry.LogicalId, scene, hierarchy, component);
        var path = PathRules.GetSafeCombinedPath(theme.ImageDirectory, string.IsNullOrEmpty(rule?.File) ? entry.LogicalId : rule.File);
        if (!File.Exists(path) || rule == null && !IsImageOverride(entry, path)) return null;
        var texture = LoadTexture(path, entry.TextureName);
        if (texture != null && !textureOrigins.ContainsKey(texture.GetInstanceID())) textureOrigins[texture.GetInstanceID()] = new TextureOrigin(GetOriginalTexture(original), entry);
        return texture;
    }

    public void Dispose()
    {
        foreach (var sprite in spriteCache.Values.Distinct()) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        foreach (var texture in textureCache.Values.Distinct()) if (texture != null) UnityEngine.Object.Destroy(texture);
        spriteCache.Clear(); textureCache.Clear(); spriteOrigins.Clear(); textureOrigins.Clear();
    }

    private sealed record SpriteOrigin(Sprite? Sprite, ImageCatalogEntry Entry);
    private sealed record TextureOrigin(Texture? Texture, ImageCatalogEntry Entry);
}
