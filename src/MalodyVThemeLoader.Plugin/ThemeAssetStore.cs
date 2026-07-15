using System.Security.Cryptography;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ThemeAssetStore
{
    private readonly ManualLogSource log;
    private readonly LoadedTheme theme;
    private readonly Dictionary<string, List<ImageCatalogEntry>> imagesByRuntimeName;
    private readonly Dictionary<string, Texture2D> textureCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> spriteCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> imageOverrideCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> live2dHashesByLogicalId;
    private readonly Dictionary<string, bool> live2dOverrideCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> live2dTexturesByName =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".ogv" };

    public ThemeAssetStore(
        ManualLogSource log,
        LoadedTheme theme,
        AssetCatalog catalog)
    {
        this.log = log;
        this.theme = theme;
        imagesByRuntimeName = catalog.Images
            .GroupBy(entry => NormalizeRuntimeName(entry.RuntimeName),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);
        live2dHashesByLogicalId = catalog.Live2dFiles
            .GroupBy(entry => PathRules.NormalizeLogicalPath(entry.LogicalId),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Sha256,
                StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(theme.Live2dDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(
                         theme.Live2dDirectory, "*.png", SearchOption.AllDirectories))
            {
                var logicalId = PathRules.NormalizeLogicalPath(
                    Path.GetRelativePath(theme.Live2dDirectory, file));
                if (!IsLive2dOverride(logicalId, file))
                {
                    continue;
                }

                var key = NormalizeRuntimeName(Path.GetFileNameWithoutExtension(file));
                if (!live2dTexturesByName.TryGetValue(key, out var paths))
                {
                    paths = new List<string>();
                    live2dTexturesByName[key] = paths;
                }

                paths.Add(file);
            }
        }

        LogImageOverrideSummary(catalog.Images);
        LogVideoOverrideSummary(catalog.Images);
        LogLive2dOverrideSummary(catalog.Live2dFiles);
    }

    public Sprite? ResolveSprite(string requestedName, Sprite? original)
    {
        var key = NormalizeRuntimeName(requestedName);
        if (!TryResolveImageEntry(key, original, out var entry, out var filePath))
        {
            return null;
        }

        var cacheKey = entry.LogicalId + "|" + GetSpriteShapeKey(original);
        if (spriteCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached != null)
            {
                return cached;
            }

            spriteCache.Remove(cacheKey);
        }

        var texture = LoadTexture(filePath, entry.TextureName);
        if (texture == null)
        {
            return null;
        }

        var width = texture.width;
        var height = texture.height;
        var pivot = original != null && original.rect.width > 0 && original.rect.height > 0
            ? new Vector2(
                original.pivot.x / original.rect.width,
                original.pivot.y / original.rect.height)
            : new Vector2(entry.Pivot.X, entry.Pivot.Y);
        var border = original != null
            ? original.border
            : new Vector4(entry.Border.X, entry.Border.Y, entry.Border.Z, entry.Border.W);
        var pixelsPerUnit = original != null && original.pixelsPerUnit > 0
            ? original.pixelsPerUnit
            : entry.PixelsPerUnit;

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            pivot,
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = original != null ? original.name : entry.RuntimeName;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        spriteCache[cacheKey] = sprite;
        return sprite;
    }

    public Texture2D? ResolveTexture(Texture? original)
    {
        if (original == null)
        {
            return null;
        }

        var key = NormalizeRuntimeName(original.name);
        if (!TryResolveImageEntry(key, null, out var entry, out var filePath))
        {
            return null;
        }

        return LoadTexture(filePath, entry.TextureName);
    }

    public ThemeVideoReplacement? ResolveVideo(string requestedName, Sprite? original)
    {
        var key = NormalizeRuntimeName(requestedName);
        var width = original != null ? (int)Math.Round(original.rect.width) : 0;
        var height = original != null ? (int)Math.Round(original.rect.height) : 0;
        if (!TryResolveVideoEntry(key, width, height, out var entry, out var filePath))
        {
            return null;
        }

        return CreateVideoReplacement(entry, filePath, width, height);
    }

    public ThemeVideoReplacement? ResolveVideo(Texture? original)
    {
        if (original == null)
        {
            return null;
        }

        var key = NormalizeRuntimeName(original.name);
        if (!TryResolveVideoEntry(
                key,
                original.width,
                original.height,
                out var entry,
                out var filePath))
        {
            return null;
        }

        return CreateVideoReplacement(entry, filePath, original.width, original.height);
    }

    public Texture2D? ResolveCubismTexture(Texture? original, string hierarchy)
    {
        if (original == null)
        {
            return null;
        }

        var key = NormalizeRuntimeName(original.name);
        if (!live2dTexturesByName.TryGetValue(key, out var candidates))
        {
            return null;
        }

        var selected = SelectLive2dCandidate(candidates, hierarchy);
        return selected == null ? null : LoadTexture(selected, original.name);
    }

    public string? ResolveLegacyLive2dFile(string resourcePath, params string[] suffixes)
    {
        var normalized = resourcePath.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("Live2d/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Live2d/".Length..];
        }

        foreach (var suffix in suffixes)
        {
            var logicalId = PathRules.NormalizeLogicalPath(normalized + suffix);
            var candidate = PathRules.GetSafeCombinedPath(theme.Live2dDirectory, logicalId);
            if (File.Exists(candidate) && IsLive2dOverride(logicalId, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool TryResolveImageEntry(
        string runtimeName,
        Sprite? original,
        out ImageCatalogEntry entry,
        out string filePath)
    {
        entry = null!;
        filePath = "";

        if (!imagesByRuntimeName.TryGetValue(runtimeName, out var candidates))
        {
            return false;
        }

        var existing = candidates
            .Select(candidate => new
            {
                Entry = candidate,
                Path = PathRules.GetSafeCombinedPath(theme.ImageDirectory, candidate.LogicalId)
            })
            .Where(candidate => IsImageOverride(candidate.Entry, candidate.Path))
            .ToList();
        if (existing.Count == 0)
        {
            return false;
        }

        if (candidates.Count == 1)
        {
            entry = existing[0].Entry;
            filePath = existing[0].Path;
            return true;
        }

        if (original != null)
        {
            var sizeMatches = candidates.Where(candidate =>
                    candidate.Width > 0
                    && candidate.Height > 0
                    && Math.Abs(original.rect.width - candidate.Width) < 0.5f
                    && Math.Abs(original.rect.height - candidate.Height) < 0.5f)
                .ToList();
            if (sizeMatches.Count == 1)
            {
                var selected = existing.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Entry.LogicalId,
                        sizeMatches[0].LogicalId,
                        StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                {
                    entry = selected.Entry;
                    filePath = selected.Path;
                    return true;
                }
            }
        }

        log.LogWarning($"Ambiguous theme image '{runtimeName}', keeping original.");
        return false;
    }

    private bool TryResolveVideoEntry(
        string runtimeName,
        int width,
        int height,
        out ImageCatalogEntry entry,
        out string filePath)
    {
        entry = null!;
        filePath = "";

        if (!imagesByRuntimeName.TryGetValue(runtimeName, out var candidates))
        {
            return false;
        }

        var existing = candidates
            .Select(candidate => new
            {
                Entry = candidate,
                Path = TryGetVideoPath(candidate, out var path) ? path : null
            })
            .Where(candidate => candidate.Path != null)
            .ToList();
        if (existing.Count == 0)
        {
            return false;
        }

        if (existing.Count == 1)
        {
            entry = existing[0].Entry;
            filePath = existing[0].Path!;
            return true;
        }

        if (width > 0 && height > 0)
        {
            var sizeMatches = existing.Where(candidate =>
                    candidate.Entry.Width > 0
                    && candidate.Entry.Height > 0
                    && Math.Abs(width - candidate.Entry.Width) < 1
                    && Math.Abs(height - candidate.Entry.Height) < 1)
                .ToList();
            if (sizeMatches.Count == 1)
            {
                entry = sizeMatches[0].Entry;
                filePath = sizeMatches[0].Path!;
                return true;
            }
        }

        log.LogWarning($"Ambiguous theme video '{runtimeName}', keeping original.");
        return false;
    }

    private bool TryGetVideoPath(ImageCatalogEntry entry, out string filePath)
    {
        foreach (var extension in VideoExtensions)
        {
            var logicalId = PathRules.NormalizeLogicalPath(
                Path.ChangeExtension(entry.LogicalId, extension));
            var candidate = PathRules.GetSafeCombinedPath(theme.ImageDirectory, logicalId);
            if (File.Exists(candidate))
            {
                filePath = candidate;
                return true;
            }
        }

        filePath = "";
        return false;
    }

    private static ThemeVideoReplacement CreateVideoReplacement(
        ImageCatalogEntry entry,
        string filePath,
        int preferredWidth,
        int preferredHeight)
    {
        var width = preferredWidth > 0 ? preferredWidth : entry.Width;
        var height = preferredHeight > 0 ? preferredHeight : entry.Height;
        if (width <= 0 || height <= 0)
        {
            width = 1920;
            height = 1080;
        }

        return new ThemeVideoReplacement(
            filePath,
            entry.LogicalId,
            entry.TextureName,
            width,
            height);
    }

    private bool IsImageOverride(ImageCatalogEntry entry, string path)
    {
        if (imageOverrideCache.TryGetValue(entry.LogicalId, out var cached))
        {
            return cached;
        }

        var isOverride = false;
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                using var sha256 = SHA256.Create();
                var hash = Convert.ToHexString(sha256.ComputeHash(stream));
                isOverride = string.IsNullOrWhiteSpace(entry.Sha256)
                             || !hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception)
        {
            log.LogWarning(
                $"Failed to inspect theme image '{path}': {exception.Message}");
        }

        imageOverrideCache[entry.LogicalId] = isOverride;
        return isOverride;
    }

    private bool IsLive2dOverride(string logicalId, string path)
    {
        logicalId = PathRules.NormalizeLogicalPath(logicalId);
        if (live2dOverrideCache.TryGetValue(logicalId, out var cached))
        {
            return cached;
        }

        var isOverride = false;
        try
        {
            if (File.Exists(path))
            {
                if (!live2dHashesByLogicalId.TryGetValue(logicalId, out var originalHash)
                    || string.IsNullOrWhiteSpace(originalHash))
                {
                    isOverride = true;
                }
                else
                {
                    using var stream = File.OpenRead(path);
                    using var sha256 = SHA256.Create();
                    var hash = Convert.ToHexString(sha256.ComputeHash(stream));
                    isOverride = !hash.Equals(originalHash, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception exception)
        {
            log.LogWarning(
                $"Failed to inspect Live2D theme file '{path}': {exception.Message}");
        }

        live2dOverrideCache[logicalId] = isOverride;
        return isOverride;
    }

    private Texture2D? LoadTexture(string filePath, string textureName)
    {
        if (textureCache.TryGetValue(filePath, out var cached))
        {
            if (cached != null)
            {
                return cached;
            }

            textureCache.Remove(filePath);
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            Il2CppStructArray<byte> il2CppBytes = bytes;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, il2CppBytes, false))
            {
                log.LogWarning($"Unity rejected image: {filePath}");
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = textureName;
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            texture.wrapMode = TextureWrapMode.Clamp;
            textureCache[filePath] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            log.LogWarning($"Failed to load theme image '{filePath}': {exception.Message}");
            return null;
        }
    }

    private void LogImageOverrideSummary(IEnumerable<ImageCatalogEntry> entries)
    {
        var count = 0;
        var examples = new List<string>();

        foreach (var entry in entries)
        {
            var path = PathRules.GetSafeCombinedPath(theme.ImageDirectory, entry.LogicalId);
            if (!TryIsImageOverrideForSummary(entry, path))
            {
                continue;
            }

            count++;
            if (examples.Count < 8)
            {
                examples.Add(entry.LogicalId);
            }
        }

        if (count == 0)
        {
            log.LogInfo("Theme image overrides detected: 0.");
            return;
        }

        var suffix = count > examples.Count ? ", ..." : "";
        log.LogInfo(
            $"Theme image overrides detected: {count}; "
            + string.Join(", ", examples)
            + suffix);
    }

    private void LogVideoOverrideSummary(IEnumerable<ImageCatalogEntry> entries)
    {
        var count = 0;
        var examples = new List<string>();

        foreach (var entry in entries)
        {
            if (!TryGetVideoPath(entry, out _))
            {
                continue;
            }

            count++;
            if (examples.Count < 8)
            {
                examples.Add(Path.ChangeExtension(entry.LogicalId, ".mp4"));
            }
        }

        if (count == 0)
        {
            log.LogInfo("Theme video overrides detected: 0.");
            return;
        }

        var suffix = count > examples.Count ? ", ..." : "";
        log.LogInfo(
            $"Theme video overrides detected: {count}; "
            + string.Join(", ", examples)
            + suffix);
    }

    private void LogLive2dOverrideSummary(IEnumerable<FileCatalogEntry> entries)
    {
        var count = 0;
        var examples = new List<string>();

        foreach (var entry in entries)
        {
            var logicalId = PathRules.NormalizeLogicalPath(entry.LogicalId);
            var path = PathRules.GetSafeCombinedPath(theme.Live2dDirectory, logicalId);
            if (!IsLive2dOverride(logicalId, path))
            {
                continue;
            }

            count++;
            if (examples.Count < 8)
            {
                examples.Add(logicalId);
            }
        }

        if (count == 0)
        {
            log.LogInfo("Theme Live2D overrides detected: 0.");
            return;
        }

        var suffix = count > examples.Count ? ", ..." : "";
        log.LogInfo(
            $"Theme Live2D overrides detected: {count}; "
            + string.Join(", ", examples)
            + suffix);
    }

    private bool TryIsImageOverrideForSummary(ImageCatalogEntry entry, string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            var hash = Convert.ToHexString(sha256.ComputeHash(stream));
            return string.IsNullOrWhiteSpace(entry.Sha256)
                   || !hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            log.LogWarning(
                $"Failed to inspect theme image '{path}': {exception.Message}");
            return false;
        }
    }

    private static string? SelectLive2dCandidate(
        IReadOnlyCollection<string> candidates,
        string hierarchy)
    {
        if (candidates.Count == 1)
        {
            return candidates.First();
        }

        if (hierarchy.Contains("christmas", StringComparison.OrdinalIgnoreCase)
            || hierarchy.Contains("chris", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(path =>
                path.Contains("marlo_chris", StringComparison.OrdinalIgnoreCase));
        }

        if (hierarchy.Contains("marlo", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(path =>
                path.Contains("marlo 2", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static string NormalizeRuntimeName(string name)
    {
        const string cloneSuffix = "(Clone)";
        var normalized = name?.Trim() ?? "";
        if (normalized.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            normalized = normalized[..^cloneSuffix.Length];
        }

        return normalized;
    }

    private static string GetSpriteShapeKey(Sprite? original)
    {
        if (original == null)
        {
            return "catalog";
        }

        return string.Join(",",
            original.pixelsPerUnit,
            original.pivot.x,
            original.pivot.y,
            original.border.x,
            original.border.y,
            original.border.z,
            original.border.w);
    }
}

internal sealed record ThemeVideoReplacement(
    string FilePath,
    string LogicalId,
    string TextureName,
    int Width,
    int Height);
