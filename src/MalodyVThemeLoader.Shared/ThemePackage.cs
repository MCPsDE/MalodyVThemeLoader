using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MalodyVThemeLoader.Shared;

public static class ThemePackage
{
    public const string Extension = ".mtp";
    private const long MaximumExpandedSize = 1024L * 1024L * 1024L;
    private const int MaximumEntries = 20_000;

    private static readonly Regex SafeId = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ThemePackageInfo Inspect(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ValidateArchive(archive);

        var manifestEntry = archive.Entries.SingleOrDefault(entry =>
            string.Equals(
                NormalizeEntryName(entry.FullName),
                "theme.json",
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("主题包根目录缺少 theme.json。");
        using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream);
        var rawJson = reader.ReadToEnd();
        var manifest = JsonSerializer.Deserialize<ThemeManifest>(
            rawJson, JsonDefaults.Options)
            ?? throw new InvalidDataException("theme.json 为空。");
        ValidateManifest(manifest);

        return new ThemePackageInfo(
            Path.GetFullPath(packagePath),
            manifest,
            rawJson,
            archive.Entries.Count(entry => !IsDirectory(entry)),
            archive.Entries.Sum(entry => entry.Length));
    }

    public static void CreateFromDirectory(
        string sourceDirectory,
        string destinationPath)
    {
        var manifestPath = Path.Combine(sourceDirectory, "theme.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("主题目录缺少 theme.json。");
        }

        var manifest = JsonSerializer.Deserialize<ThemeManifest>(
            File.ReadAllText(manifestPath), JsonDefaults.Options)
            ?? throw new InvalidDataException("theme.json 为空。");
        ValidateManifest(manifest);

        Directory.CreateDirectory(Path.GetDirectoryName(
            Path.GetFullPath(destinationPath))!);
        var temporary = destinationPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            ZipFile.CreateFromDirectory(
                sourceDirectory,
                temporary,
                CompressionLevel.Optimal,
                false);
            Inspect(temporary);
            File.Move(temporary, destinationPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static void ExtractToDirectory(
        string packagePath,
        string destinationDirectory)
    {
        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ValidateArchive(archive);

        Directory.CreateDirectory(destinationDirectory);
        foreach (var entry in archive.Entries)
        {
            if (IsDirectory(entry))
            {
                continue;
            }

            var relative = NormalizeEntryName(entry.FullName);
            var target = PathRules.GetSafeCombinedPath(
                destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open();
            using var destination = new FileStream(
                target, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
    }

    public static string GetPackageFileName(ThemeManifest manifest)
    {
        ValidateManifest(manifest);
        return manifest.Id + Extension;
    }

    public static void ValidateManifest(ThemeManifest manifest)
    {
        if (manifest.FormatVersion != 1)
        {
            throw new InvalidDataException(
                $"不支持的主题格式版本：{manifest.FormatVersion}。");
        }
        if (!SafeId.IsMatch(manifest.Id))
        {
            throw new InvalidDataException(
                "主题 id 只能包含英文字母、数字、点、下划线和连字符，最长 64 个字符。");
        }
        if (string.IsNullOrWhiteSpace(manifest.Name)
            || manifest.Name.Length > 100)
        {
            throw new InvalidDataException("主题 name 不能为空且不得超过 100 个字符。");
        }

        ValidateRoot(manifest.ImageRoot, "imageRoot");
        ValidateRoot(manifest.FontRoot, "fontRoot");
        ValidateRoot(manifest.Live2dRoot, "live2dRoot");
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException("主题包文件数量超过安全限制。");
        }

        long expandedSize = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntryName(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }
            var validationPath = normalized.TrimEnd('/');
            if (Path.IsPathRooted(validationPath)
                || HasUnsafeSegments(validationPath))
            {
                throw new InvalidDataException(
                    $"主题包包含不安全路径：{entry.FullName}");
            }
            if (!names.Add(normalized))
            {
                throw new InvalidDataException(
                    $"主题包包含重复路径：{entry.FullName}");
            }

            expandedSize = checked(expandedSize + entry.Length);
            if (expandedSize > MaximumExpandedSize)
            {
                throw new InvalidDataException("主题包解压后超过 1 GiB 安全限制。");
            }
        }
    }

    private static void ValidateRoot(string value, string property)
    {
        var normalized = PathRules.NormalizeLogicalPath(value);
        if (string.IsNullOrWhiteSpace(normalized)
            || Path.IsPathRooted(normalized)
            || HasUnsafeSegments(normalized.TrimEnd('/')))
        {
            throw new InvalidDataException($"{property} 包含不安全路径。");
        }
    }

    private static bool HasUnsafeSegments(string path)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var part in path.Split('/'))
        {
            if (string.IsNullOrWhiteSpace(part)
                || part is "." or ".."
                || part.EndsWith(".", StringComparison.Ordinal)
                || part.EndsWith(" ", StringComparison.Ordinal)
                || part.IndexOfAny(invalid) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectory(ZipArchiveEntry entry)
    {
        return entry.FullName.EndsWith("/", StringComparison.Ordinal)
               || entry.FullName.EndsWith("\\", StringComparison.Ordinal);
    }

    private static string NormalizeEntryName(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}

public sealed class ThemePackageInfo
{
    public ThemePackageInfo(
        string packagePath,
        ThemeManifest manifest,
        string rawManifestJson,
        int fileCount,
        long expandedSize)
    {
        PackagePath = packagePath;
        Manifest = manifest;
        RawManifestJson = rawManifestJson;
        FileCount = fileCount;
        ExpandedSize = expandedSize;
    }

    public string PackagePath { get; }
    public ThemeManifest Manifest { get; }
    public string RawManifestJson { get; }
    public int FileCount { get; }
    public long ExpandedSize { get; }
}
