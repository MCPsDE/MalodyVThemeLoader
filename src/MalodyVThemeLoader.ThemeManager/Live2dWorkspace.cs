using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal static class Live2dWorkspace
{
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
        var cubism = Directory.EnumerateFiles(root, "*.model3.json", SearchOption.AllDirectories)
            .Select(path => PathRules.NormalizeLogicalPath(Path.GetRelativePath(root, path)));
        var legacy = Directory.EnumerateDirectories(root).Where(path =>
            Directory.EnumerateFiles(path, "*.moc.bytes", SearchOption.AllDirectories).Any())
            .Select(Path.GetFileName).OfType<string>();
        return cubism.Concat(legacy).OrderBy(value => value.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToList();
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
        if (model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase)) modelRoot = Path.GetDirectoryName(modelRoot)!;
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
        return ImportModelFiles(copyRoot, destinationRoot);
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
            return ImportModelFiles(source, destinationRoot);
        }
        finally { DeleteDirectory(staging); }
    }

    public static void ImportAnimations(ThemeItem item, string model, IEnumerable<string> paths)
    {
        var themeRoot = EnsureEditableDirectory(item);
        var modelRoot = PathRules.GetSafeCombinedPath(EnsureLive2dRoot(themeRoot), model);
        if (model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            modelRoot = Path.Combine(Path.GetDirectoryName(modelRoot)!, "motions-imported");
        Directory.CreateDirectory(modelRoot);
        foreach (var path in paths)
        {
            if (!IsAnimation(path)) throw new InvalidDataException($"不是支持的 Live2D 动画文件：{path}");
            if (model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase)) CubismMotion.Read(path);
            var destination = Path.Combine(modelRoot, Path.GetFileName(path));
            if (!Path.GetFullPath(path).Equals(destination, StringComparison.OrdinalIgnoreCase)) File.Copy(path, destination, true);
        }
    }

    public static Live2dBinding? GetBinding(ThemeItem item)
    {
        var root = GetLive2dRoot(item, false);
        var file = root is null ? "" : Path.Combine(root, Live2dModelDocument.BindingFile);
        return File.Exists(file) ? JsonSerializer.Deserialize<Live2dBinding>(File.ReadAllText(file), JsonDefaults.Options) : null;
    }

    public static Live2dModelDocument ReadModel(ThemeItem item, string model) =>
        Live2dModelDocument.Read(GetLive2dRoot(item, false) ?? throw new InvalidOperationException("请先选择主题。"), model);

    public static IReadOnlyList<Live2dMotionReference> GetMotionChoices(ThemeItem item, string model)
    {
        var document = ReadModel(item, model);
        var root = GetLive2dRoot(item, false)!;
        var directory = Path.GetDirectoryName(Live2dModelDocument.Resolve(root, model))!;
        var references = document.Motions.ToList();
        foreach (var file in Directory.EnumerateFiles(directory, "*.motion3.json", SearchOption.AllDirectories))
        {
            var relative = PathRules.NormalizeLogicalPath(Path.GetRelativePath(root, file));
            if (!references.Any(value => value.File.Equals(relative, StringComparison.OrdinalIgnoreCase)))
                references.Add(new Live2dMotionReference { Group = "导入动画", File = relative });
        }
        return references;
    }

    public static void SaveBinding(ThemeItem item, Live2dBinding binding)
    {
        var root = EnsureLive2dRoot(EnsureEditableDirectory(item));
        var document = Live2dModelDocument.Read(root, binding.Model);
        document.ValidateBinding(root, binding);
        var file = Path.Combine(root, Live2dModelDocument.BindingFile);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(binding, JsonDefaults.Options));
        File.Move(file + ".tmp", file, true);
    }

    public static void RemoveBinding(ThemeItem item)
    {
        var root = GetLive2dRoot(item, false);
        if (root != null) File.Delete(Path.Combine(root, Live2dModelDocument.BindingFile));
    }

    private static string ImportModelFiles(string source, string root)
    {
        var models = Directory.EnumerateFiles(source, "*.model3.json", SearchOption.AllDirectories).ToArray();
        if (models.Length == 0) throw new InvalidDataException("没有找到 .model3.json，请选择包含模型及纹理的目录。");
        // Validate every dependency before touching an existing model. Keep each
        // model's parent folder so two texture_00.png files never collide.
        foreach (var file in models)
            Live2dModelDocument.Read(source, Path.GetRelativePath(source, file));
        string? firstImported = null;
        foreach (var directory in models.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var name = directory.Equals(source, StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(models.First(file => Path.GetDirectoryName(file) == directory)).Replace(".model3.json", "", StringComparison.OrdinalIgnoreCase)
                : Path.GetRelativePath(source, directory);
            var destination = Path.Combine(root, "models", name);
            firstImported ??= PathRules.NormalizeLogicalPath(Path.GetRelativePath(root,
                Path.Combine(destination, Path.GetFileName(models.First(file => Path.GetDirectoryName(file) == directory)))));
            if (Path.GetFullPath(source).Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)
                || Path.GetFullPath(source).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
                || Path.GetFullPath(root).StartsWith(Path.GetFullPath(source).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("模型已经在当前主题内，请直接选择它并应用。");
            var staging = destination + ".new-" + Guid.NewGuid().ToString("N");
            var backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            try
            {
                CopyDirectoryWithoutLinks(directory, staging);
                if (Directory.Exists(destination)) Directory.Move(destination, backup);
                try { Directory.Move(staging, destination); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
                DeleteDirectory(backup);
            }
            finally { DeleteDirectory(staging); }
        }
        return firstImported!;
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
        var live2d = PathRules.GetSafeCombinedPath(root, item.Info?.Manifest.Live2dRoot ?? "live2d");
        if (create) Directory.CreateDirectory(live2d);
        return live2d;
    }

    private static string? EnsureEditableDirectoryIfPossible(ThemeItem item)
    {
        if (item.IsVirtual) return null;
        return item.IsLegacyDirectory
            ? item.SourcePath
            : null;
    }

    private static string EnsureLive2dRoot(string themeRoot)
    {
        var manifest = JsonSerializer.Deserialize<ThemeManifest>(File.ReadAllText(Path.Combine(themeRoot, "theme.json")), JsonDefaults.Options);
        var root = PathRules.GetSafeCombinedPath(themeRoot, manifest?.Live2dRoot ?? "live2d");
        Directory.CreateDirectory(root);
        return root;
    }

    private static bool IsAnimation(string path)
    {
        return path.EndsWith(".motion3.json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".mtn", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".mtn.bytes", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".fade.asset", StringComparison.OrdinalIgnoreCase);
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
