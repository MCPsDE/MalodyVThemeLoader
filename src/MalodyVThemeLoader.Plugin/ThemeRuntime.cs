using System.Text.Json;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ThemeRuntime
{
    public static ThemeRuntime? Current { get; set; }

    public ManualLogSource Log { get; }
    public LoadedTheme Theme { get; }
    public ThemeLoaderSettings Settings { get; }
    public ThemeAssetStore Assets { get; }
    public FontReplacementService Fonts { get; }
    public ThemeVideoService Videos { get; }
    public NodeDecorationService Decorations { get; }
    public SceneThemeApplier Applier { get; }
    public Live2dReplacementService Live2d { get; }
    public ThemeAudioService Audio { get; }
    public ThemeRescanScheduler? Scheduler { get; set; }

    private ThemeRuntime(
        ManualLogSource log,
        LoadedTheme theme,
        ThemeLoaderSettings settings,
        AssetCatalog catalog)
    {
        Log = log;
        Theme = theme;
        Settings = settings;
        Assets = new ThemeAssetStore(log, theme, catalog);
        Fonts = new FontReplacementService(log, theme);
        Videos = new ThemeVideoService(log, Assets);
        Decorations = new NodeDecorationService(log, theme, Assets.ResourceRules);
        Applier = new SceneThemeApplier(log, Assets, Fonts, Videos);
        Live2d = new Live2dReplacementService(log, theme);
        Audio = new ThemeAudioService(log, theme, Assets.ResourceRules);
    }

    public static ThemeRuntime Create(ManualLogSource log)
    {
        var loaderRoot = Path.Combine(Paths.GameRootPath, "ThemeLoader");
        var activeThemePath = Path.Combine(loaderRoot, "active-theme.txt");
        var settings = ReadSettings(loaderRoot);
        var activeTheme = File.Exists(activeThemePath)
            ? File.ReadAllText(activeThemePath).Trim()
            : "Default";

        if (string.IsNullOrWhiteSpace(activeTheme))
        {
            activeTheme = "Default";
        }

        var themesRoot = Path.Combine(loaderRoot, "themes");
        var themeDirectoryCandidate = PathRules.GetSafeCombinedPath(themesRoot, activeTheme);
        var packagePath = PathRules.GetSafeCombinedPath(
            themesRoot, activeTheme + ThemePackage.Extension);
        var legacyPackagePath = PathRules.GetSafeCombinedPath(themesRoot, activeTheme + ".mtp");
        string themeDirectory;
        string sourceDisplay;
        string sourcePath;
        ThemeSourceKind sourceKind;
        ThemeManifest manifest;
        if (Directory.Exists(themeDirectoryCandidate))
        {
            themeDirectory = themeDirectoryCandidate;
            sourceDisplay = themeDirectory;
            sourcePath = themeDirectory;
            sourceKind = ThemeSourceKind.Directory;
            manifest = ReadManifest(themeDirectory);
        }
        else if (File.Exists(packagePath) || File.Exists(legacyPackagePath))
        {
            if (!File.Exists(packagePath)) packagePath = legacyPackagePath;
            themeDirectory = ThemeCache.Prepare(packagePath);
            sourceDisplay = packagePath;
            sourcePath = packagePath;
            sourceKind = ThemeSourceKind.Package;
            manifest = ReadManifest(themeDirectory);
        }
        else
        {
            themeDirectory = themeDirectoryCandidate;
            sourceDisplay = themeDirectory;
            if (string.Equals(
                         activeTheme, "Default", StringComparison.OrdinalIgnoreCase))
            {
                themeDirectory = ThemeCache.PrepareVirtualDefault();
                sourceDisplay = "original game assets";
                sourcePath = "";
                sourceKind = ThemeSourceKind.VirtualDefault;
                manifest = new ThemeManifest
                {
                    Id = "default",
                    Name = "Default",
                    Description = "Original game assets; no theme package is stored."
                };
            }
            else
            {
                throw new FileNotFoundException(
                    "Active theme package was not found.", packagePath);
            }
        }

        if (manifest.FormatVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported theme format: {manifest.FormatVersion}");
        }

        var catalogPath = Path.Combine(
            Paths.PluginPath, "MalodyVThemeLoader", "catalog.json");
        var catalog = JsonSerializer.Deserialize<AssetCatalog>(
            File.ReadAllText(catalogPath), JsonDefaults.Options)
            ?? throw new InvalidDataException("Asset catalog is empty.");
        if (catalog.FormatVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported catalog format: {catalog.FormatVersion}");
        }

        return new ThemeRuntime(
            log,
            new LoadedTheme(
                activeTheme,
                themeDirectory,
                sourceDisplay,
                sourcePath,
                sourceKind,
                manifest),
            settings,
            catalog);
    }

    public static ThemeLoaderSettings ReadSettings(string loaderRoot)
    {
        var settingsPath = Path.Combine(loaderRoot, "settings.json");
        if (!File.Exists(settingsPath))
        {
            return new ThemeLoaderSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ThemeLoaderSettings>(
                File.ReadAllText(settingsPath), JsonDefaults.Options);
            return settings?.FormatVersion == 1
                ? settings
                : new ThemeLoaderSettings();
        }
        catch
        {
            return new ThemeLoaderSettings();
        }
    }

    private static ThemeManifest ReadManifest(string themeDirectory)
    {
        var manifestPath = Path.Combine(themeDirectory, "theme.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "Active theme manifest was not found.", manifestPath);
        }

        return JsonSerializer.Deserialize<ThemeManifest>(
                   File.ReadAllText(manifestPath), JsonDefaults.Options)
               ?? throw new InvalidDataException("Theme manifest is empty.");
    }
}

internal sealed record LoadedTheme(
    string ActiveKey,
    string ThemeDirectory,
    string SourceDisplay,
    string SourcePath,
    ThemeSourceKind SourceKind,
    ThemeManifest Manifest)
{
    public string ImageDirectory =>
        PathRules.GetSafeCombinedPath(ThemeDirectory, Manifest.ImageRoot);

    public string FontDirectory =>
        PathRules.GetSafeCombinedPath(ThemeDirectory, Manifest.FontRoot);

    public string Live2dDirectory =>
        PathRules.GetSafeCombinedPath(ThemeDirectory, Manifest.Live2dRoot);
}

internal enum ThemeSourceKind
{
    Package,
    Directory,
    VirtualDefault
}

internal static class ThemeCache
{
    public static string PrepareVirtualDefault()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MalodyVThemeLoader",
            "theme-cache",
            "Default-original-assets");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string Prepare(string packagePath)
    {
        ThemePackage.Inspect(packagePath);
        var hash = ComputeSha256(packagePath)[..20];
        var cacheRoot = Path.Combine(
            Path.GetTempPath(), "MalodyVThemeLoader", "theme-cache");
        var destination = Path.Combine(
            cacheRoot,
            Path.GetFileNameWithoutExtension(packagePath) + "-" + hash);
        if (File.Exists(Path.Combine(destination, "theme.json")))
        {
            return destination;
        }

        Directory.CreateDirectory(cacheRoot);
        var staging = destination + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            ThemePackage.ExtractToDirectory(packagePath, staging);
            if (Directory.Exists(destination))
            {
                DeleteCacheDirectory(destination, cacheRoot);
            }
            Directory.Move(staging, destination);
            CleanStaleCaches(cacheRoot, destination);
            return destination;
        }
        catch
        {
            DeleteCacheDirectory(staging, cacheRoot);
            throw;
        }
    }

    private static void CleanStaleCaches(string cacheRoot, string current)
    {
        foreach (var directory in Directory.EnumerateDirectories(cacheRoot))
        {
            if (string.Equals(
                    directory, current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (Directory.GetLastWriteTimeUtc(directory)
                    < DateTime.UtcNow.AddDays(-7))
                {
                    DeleteCacheDirectory(directory, cacheRoot);
                }
            }
            catch
            {
            }
        }
    }

    private static void DeleteCacheDirectory(string path, string cacheRoot)
    {
        var fullRoot = Path.GetFullPath(cacheRoot)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }
}
