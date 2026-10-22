using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeBuilder;

internal static class Program
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".webp" };

    public static int Main(string[] args)
    {
        try
        {
            var options = BuilderOptions.Parse(args);
            Build(options);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Build(BuilderOptions options)
    {
        var assetsRoot = Path.Combine(options.SourceRoot, "Assets", "Malody");
        var projectVersion = Path.Combine(options.SourceRoot, "ProjectSettings", "ProjectVersion.txt");
        if (!Directory.Exists(assetsRoot) || !File.Exists(projectVersion))
        {
            throw new DirectoryNotFoundException(
                $"Not a Malody Unity source tree: {options.SourceRoot}");
        }

        var catalog = new AssetCatalog
        {
            UnityVersion = "2022.3.62f2"
        };

        string? themeRoot = null;
        string? imageRoot = null;
        string? live2dRoot = null;
        if (options.IncludeDefaultTheme)
        {
            themeRoot = Path.Combine(
                options.OutputRoot, "ThemeLoader", ".default-theme-build");
            imageRoot = Path.Combine(themeRoot, "images");
            live2dRoot = Path.Combine(themeRoot, "live2d");
            RecreateDirectory(themeRoot);
        }

        CopyImages(assetsRoot, imageRoot, catalog);
        CopyLive2d(assetsRoot, live2dRoot, catalog);
        SourceUsageCatalog.Build(assetsRoot, catalog,
            themeRoot == null ? null : Path.Combine(themeRoot, "audio"));

        string? defaultDirectory = null;
        if (options.IncludeDefaultTheme && themeRoot != null)
        {
            CopyFonts(assetsRoot, Path.Combine(themeRoot, "fonts"));
            var manifest = new ThemeManifest
            {
                Id = "default",
                Name = "Default",
                Version = options.ProductVersion,
                Description = "Original visual assets.",
                GameVersion = "6.7",
                ImageRoot = "images",
                FontRoot = "fonts",
                Live2dRoot = "live2d"
            };

            WriteJson(Path.Combine(themeRoot, "theme.json"), manifest);
            var themesRoot = Path.Combine(
                options.OutputRoot, "ThemeLoader", "themes");
            Directory.CreateDirectory(themesRoot);
            defaultDirectory = Path.Combine(themesRoot, "Default");
            if (Directory.Exists(defaultDirectory))
            {
                Directory.Delete(defaultDirectory, true);
            }
            Directory.Move(themeRoot, defaultDirectory);
        }

        var pluginDirectory = Path.Combine(
            options.OutputRoot, "BepInEx", "plugins", "MalodyVThemeLoader");
        Directory.CreateDirectory(pluginDirectory);
        WriteJson(Path.Combine(pluginDirectory, "catalog.json"), catalog);

        var activeThemePath = Path.Combine(options.OutputRoot, "ThemeLoader", "active-theme.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(activeThemePath)!);
        File.WriteAllText(activeThemePath, "Default" + Environment.NewLine);
        WriteJson(
            Path.Combine(options.OutputRoot, ThemeLoaderSettings.RelativePath),
            new ThemeLoaderSettings());

        Console.WriteLine($"Catalog images: {catalog.Images.Count}");
        Console.WriteLine($"Catalog UI audio: {catalog.Audio.Count}; scene image uses: {catalog.Images.Sum(e => e.Usages.Count)}");
        Console.WriteLine(defaultDirectory == null
            ? "Default theme: not packaged"
            : $"Default theme directory: {defaultDirectory}");
    }

    private static void CopyImages(
        string assetsRoot,
        string? destinationRoot,
        AssetCatalog catalog)
    {
        var searchRoots = new[]
        {
            Path.Combine(assetsRoot, "Textures"),
            Path.Combine(assetsRoot, "Resources")
        };

        foreach (var sourcePath in searchRoots.SelectMany(root =>
                     Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)))
        {
            if (!ImageExtensions.Contains(Path.GetExtension(sourcePath)))
            {
                continue;
            }
            if (sourcePath.StartsWith(
                    Path.Combine(assetsRoot, "Resources", "Live2d")
                    + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var logicalId = PathRules.NormalizeLogicalPath(
                Path.GetRelativePath(assetsRoot, sourcePath));
            if (destinationRoot != null)
            {
                var destination = PathRules.GetSafeCombinedPath(
                    destinationRoot, logicalId);
                CopyFile(sourcePath, destination);
            }

            var imageInfo = ImageInfoReader.Read(sourcePath);
            var importer = TextureImporterMetadata.Read(sourcePath + ".meta");
            catalog.Images.Add(new ImageCatalogEntry
            {
                LogicalId = logicalId,
                RuntimeName = Path.GetFileNameWithoutExtension(sourcePath),
                TextureName = Path.GetFileNameWithoutExtension(sourcePath),
                Sha256 = ComputeSha256(sourcePath),
                Width = imageInfo.Width,
                Height = imageInfo.Height,
                Pivot = importer.Pivot,
                Border = importer.Border,
                PixelsPerUnit = importer.PixelsPerUnit,
                MeshType = importer.MeshType
            });
        }

        catalog.Images.Sort((left, right) =>
            string.Compare(left.LogicalId, right.LogicalId, StringComparison.Ordinal));
    }

    private static void CopyFonts(string assetsRoot, string destinationRoot)
    {
        var sourceRoot = Path.Combine(assetsRoot, "Fonts");
        CopyTree(sourceRoot, destinationRoot, path =>
            !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
    }

    private static void CopyLive2d(
        string assetsRoot,
        string? destinationRoot,
        AssetCatalog catalog)
    {
        var sourceRoot = Path.Combine(assetsRoot, "Resources", "Live2d");
        foreach (var sourcePath in Directory.EnumerateFiles(
                     sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (sourcePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var logicalId = PathRules.NormalizeLogicalPath(
                Path.GetRelativePath(sourceRoot, sourcePath));
            if (destinationRoot != null)
            {
                var destination = PathRules.GetSafeCombinedPath(
                    destinationRoot, logicalId);
                CopyFile(sourcePath, destination);
            }
            catalog.Live2dFiles.Add(new FileCatalogEntry
            {
                LogicalId = logicalId,
                Sha256 = ComputeSha256(sourcePath)
            });
        }

        catalog.Live2dFiles.Sort((left, right) =>
            string.Compare(left.LogicalId, right.LogicalId, StringComparison.Ordinal));
    }

    private static void CopyTree(
        string sourceRoot,
        string destinationRoot,
        Func<string, bool> include)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(
                     sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (!include(sourcePath))
            {
                continue;
            }

            var relative = Path.GetRelativePath(sourceRoot, sourcePath);
            var destination = PathRules.GetSafeCombinedPath(destinationRoot, relative);
            CopyFile(sourcePath, destination);
        }
    }

    private static void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, true);
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonDefaults.Options));
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal sealed class BuilderOptions
{
    public required string SourceRoot { get; init; }
    public required string OutputRoot { get; init; }
    public string ProductVersion { get; init; } = DefaultProductVersion;
    public bool IncludeDefaultTheme { get; init; } = true;

    public static BuilderOptions Parse(string[] args)
    {
        string? source = null;
        string? output = null;
        var version = DefaultProductVersion;
        var includeDefaultTheme = true;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--source":
                    source = RequireValue(args, ref index);
                    break;
                case "--output":
                    output = RequireValue(args, ref index);
                    break;
                case "--version":
                    version = RequireValue(args, ref index);
                    break;
                case "--catalog-only":
                    includeDefaultTheme = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException(
                "Usage: --source <Unity project> --output <payload root> [--version <version>]");
        }

        return new BuilderOptions
        {
            SourceRoot = Path.GetFullPath(source),
            OutputRoot = Path.GetFullPath(output),
            ProductVersion = version,
            IncludeDefaultTheme = includeDefaultTheme
        };
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException("Missing argument value.");
        }

        return args[index];
    }

    private static string DefaultProductVersion =>
        typeof(BuilderOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "1.3.0";
}

internal sealed class TextureImporterMetadata
{
    private static readonly Regex Vector2Regex = new(
        @"\{x:\s*(?<x>-?[\d.]+),\s*y:\s*(?<y>-?[\d.]+)\}",
        RegexOptions.Compiled);

    private static readonly Regex Vector4Regex = new(
        @"\{x:\s*(?<x>-?[\d.]+),\s*y:\s*(?<y>-?[\d.]+),\s*z:\s*(?<z>-?[\d.]+),\s*w:\s*(?<w>-?[\d.]+)\}",
        RegexOptions.Compiled);

    public Float2 Pivot { get; private set; } = new(0.5f, 0.5f);
    public Float4 Border { get; private set; } = new(0, 0, 0, 0);
    public float PixelsPerUnit { get; private set; } = 100;
    public int MeshType { get; private set; }

    public static TextureImporterMetadata Read(string path)
    {
        if (!File.Exists(path))
        {
            return new TextureImporterMetadata();
        }

        var result = new TextureImporterMetadata();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("spritePivot:", StringComparison.Ordinal))
            {
                var match = Vector2Regex.Match(trimmed);
                if (match.Success)
                {
                    result.Pivot = new Float2(Parse(match, "x"), Parse(match, "y"));
                }
            }
            else if (trimmed.StartsWith("spriteBorder:", StringComparison.Ordinal))
            {
                var match = Vector4Regex.Match(trimmed);
                if (match.Success)
                {
                    result.Border = new Float4(
                        Parse(match, "x"),
                        Parse(match, "y"),
                        Parse(match, "z"),
                        Parse(match, "w"));
                }
            }
            else if (trimmed.StartsWith("spritePixelsToUnits:", StringComparison.Ordinal)
                     && float.TryParse(
                         trimmed.Split(':', 2)[1],
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out var pixelsPerUnit))
            {
                result.PixelsPerUnit = pixelsPerUnit;
            }
            else if (trimmed.StartsWith("spriteMeshType:", StringComparison.Ordinal)
                     && int.TryParse(trimmed.Split(':', 2)[1], out var meshType))
            {
                result.MeshType = meshType;
            }
        }

        return result;
    }

    private static float Parse(Match match, string group)
    {
        return float.Parse(
            match.Groups[group].Value,
            System.Globalization.CultureInfo.InvariantCulture);
    }
}

internal readonly record struct ImageInfo(int Width, int Height);

internal static class ImageInfoReader
{
    public static ImageInfo Read(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[32];
        var read = stream.Read(header);

        if (read >= 24
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47)
        {
            return new ImageInfo(ReadBigEndianInt32(header[16..20]),
                ReadBigEndianInt32(header[20..24]));
        }

        return new ImageInfo(0, 0);
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes)
    {
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}
