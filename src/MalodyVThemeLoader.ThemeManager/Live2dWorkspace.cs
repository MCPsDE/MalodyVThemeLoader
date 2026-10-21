using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal static class Live2dWorkspace
{
    private static readonly HashSet<string> ModelMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        ".model3.json", ".moc3", ".moc.bytes", ".json", ".mtn.bytes", ".motion3.json", ".anim"
    };

    public static string EnsureEditableDirectory(ThemeItem item)
    {
        if (item.IsVirtual)
            throw new InvalidOperationException("Default 原始外观没有可编辑目录，请先导入或导出一个主题副本。");
        if (item.IsLegacyDirectory)
            return item.SourcePath;

        var target = Path.Combine(
            Path.GetDirectoryName(item.SourcePath)!,
            Path.GetFileNameWithoutExtension(item.SourcePath));
        if (!Directory.Exists(target))
        {
            var staging = target + ".live2d-new-" + Guid.NewGuid().ToString("N");
            try
            {
                ThemePackage.ExtractToDirectory(item.SourcePath, staging);
                Directory.Move(staging, target);
            }
            finally { DeleteDirectory(staging); }
        }

        // The directory is now canonical; the old ZIP remains readable only if
        // the extraction failed. A successful explicit edit removes ambiguity.
        if (File.Exists(item.SourcePath)) File.Delete(item.SourcePath);
        return target;
    }

    public static IReadOnlyList<string> GetModels(ThemeItem item)
    {
        var root = GetLive2dRoot(item, false);
        if (root is null) return Array.Empty<string>();
        return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path => ContainsModelFiles(path))
            .Select(path => Path.GetFileName(path))
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> GetTexturePaths(ThemeItem item)
    {
        var root = GetLive2dRoot(item, false);
        if (root is null) return Array.Empty<string>();
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            .Select(path => PathRules.NormalizeLogicalPath(Path.GetRelativePath(root, path)))
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> GetAnimationPaths(ThemeItem item, string model)
    {
        var root = GetLive2dRoot(item, false);
        if (root is null) return Array.Empty<string>();
        var modelRoot = PathRules.GetSafeCombinedPath(root, model);
        if (!Directory.Exists(modelRoot)) return Array.Empty<string>();
        return Directory.EnumerateFiles(modelRoot, "*", SearchOption.AllDirectories)
            .Where(IsAnimation)
            .Select(path => PathRules.NormalizeLogicalPath(Path.GetRelativePath(modelRoot, path)))
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string ImportDirectory(ThemeItem item, string sourceDirectory)
    {
        var themeRoot = EnsureEditableDirectory(item);
        var destinationRoot = EnsureLive2dRoot(themeRoot);
        var source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("模型目录不存在。");
        var copyRoot = Directory.Exists(Path.Combine(source, "live2d"))
            ? Path.Combine(source, "live2d")
            : source;
        CopyDirectoryWithoutLinks(copyRoot, destinationRoot);
        return destinationRoot;
    }

    public static string ImportArchive(ThemeItem item, string archivePath)
    {
        var themeRoot = EnsureEditableDirectory(item);
        var staging = Path.Combine(Path.GetTempPath(), "MalodyVLive2d-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtractArchive(archivePath, staging);
            var source = Directory.Exists(Path.Combine(staging, "live2d"))
                ? Path.Combine(staging, "live2d")
                : staging;
            var destinationRoot = EnsureLive2dRoot(themeRoot);
            CopyDirectoryWithoutLinks(source, destinationRoot);
            return destinationRoot;
        }
        finally { DeleteDirectory(staging); }
    }

    public static void ImportAnimations(ThemeItem item, string model, IEnumerable<string> paths)
    {
        var themeRoot = EnsureEditableDirectory(item);
        var modelRoot = PathRules.GetSafeCombinedPath(
            EnsureLive2dRoot(themeRoot), model);
        Directory.CreateDirectory(modelRoot);
        foreach (var path in paths)
        {
            if (!IsAnimation(path)) throw new InvalidDataException($"不是支持的 Live2D 动画文件：{path}");
            File.Copy(path, Path.Combine(modelRoot, Path.GetFileName(path)), true);
        }
    }

    public static void ImportImageOrGif(ThemeItem item, string sourcePath, string targetRelativePath)
    {
        var themeRoot = EnsureEditableDirectory(item);
        var target = PathRules.GetSafeCombinedPath(
            EnsureLive2dRoot(themeRoot), targetRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var extension = Path.GetExtension(sourcePath);
        if (!extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, target, true);
            return;
        }

        // GIF has no Live2D rig or animation binding. Use its first frame as a
        // valid texture replacement rather than creating an unloadable fake model.
        using var image = Image.FromFile(sourcePath);
        using var bitmap = new Bitmap(image);
        bitmap.Save(target, ImageFormat.Png);
    }

    private static string? GetLive2dRoot(ThemeItem item, bool create)
    {
        var root = EnsureEditableDirectoryIfPossible(item);
        if (root is null) return null;
        var live2d = PathRules.GetSafeCombinedPath(root, "live2d");
        if (create) Directory.CreateDirectory(live2d);
        return live2d;
    }

    private static string? EnsureEditableDirectoryIfPossible(ThemeItem item)
    {
        if (item.IsVirtual) return null;
        return item.IsLegacyDirectory
            ? item.SourcePath
            : EnsureEditableDirectory(item);
    }

    private static string EnsureLive2dRoot(string themeRoot)
    {
        var root = PathRules.GetSafeCombinedPath(themeRoot, "live2d");
        Directory.CreateDirectory(root);
        return root;
    }

    private static bool ContainsModelFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Any(path => ModelMarkers.Any(marker =>
                path.EndsWith(marker, StringComparison.OrdinalIgnoreCase)));

    private static bool IsAnimation(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".motion3.json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mtn", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".anim", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".fade.asset", StringComparison.OrdinalIgnoreCase);
    }

    private static void ExtractArchive(string archivePath, string destination)
    {
        using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Directory.CreateDirectory(destination);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/').Normalize();
            if (string.IsNullOrWhiteSpace(relative) || relative.EndsWith('/')) continue;
            var target = PathRules.GetSafeCombinedPath(destination, relative);
            entries[relative] = entry;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        }
        foreach (var pair in entries)
        {
            var target = PathRules.GetSafeCombinedPath(destination, pair.Key);
            using var source = pair.Value.Open();
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(output);
        }
    }

    private static void CopyDirectoryWithoutLinks(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Live2D 目录包含重解析点，拒绝导入：{entry}");
            var target = Path.Combine(destination, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0)
                CopyDirectoryWithoutLinks(entry, target);
            else
                File.Copy(entry, target, true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
