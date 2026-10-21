using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVBepInExInstaller;

internal sealed class InstallService
{
    public OperationResult InstallOrRepair(
        string gameDirectory,
        IProgress<string>? progress = null,
        bool installConfigurationManager = true)
    {
        if (!GameLocator.IsGameDirectory(gameDirectory, out var validationError))
        {
            return OperationResult.Fail(validationError);
        }

        gameDirectory = Path.GetFullPath(gameDirectory);
        var manifestPath = PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.ManifestRelativePath);
        var previous = ReadManifest(manifestPath);
        var previousFiles = previous?.Files
            .ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, BepInExInstalledFile>(
                StringComparer.OrdinalIgnoreCase);
        var staging = Path.Combine(
            Path.GetTempPath(), "MalodyVBepInEx-" + Guid.NewGuid().ToString("N"));
        var rollback = new List<RollbackEntry>();
        RuntimeReplacement? runtimeReplacement = null;

        try
        {
            Directory.CreateDirectory(staging);
            ExtractPayload(staging, installConfigurationManager);
            var payloadFiles = Directory.EnumerateFiles(
                    staging, "*", SearchOption.AllDirectories)
                .Select(path => new PayloadFile(
                    path,
                    PathRules.NormalizeLogicalPath(
                        Path.GetRelativePath(staging, path))))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToList();
            runtimeReplacement = new RuntimeReplacement(Path.Combine(
                gameDirectory, ".malody-bepinex-old-" + Guid.NewGuid().ToString("N")));
            PrepareCleanRuntime(gameDirectory, staging, runtimeReplacement, progress);
            ValidateOwnership(gameDirectory, payloadFiles, previousFiles);

            var installed = new List<BepInExInstalledFile>(payloadFiles.Count);
            foreach (var payloadFile in payloadFiles)
            {
                progress?.Report($"安装 {payloadFile.RelativePath}");
                var target = PathRules.GetSafeCombinedPath(
                    gameDirectory, payloadFile.RelativePath);
                previousFiles.TryGetValue(
                    payloadFile.RelativePath, out var previousFile);

                if (IsMutableFile(payloadFile.RelativePath)
                    && File.Exists(target))
                {
                    installed.Add(new BepInExInstalledFile
                    {
                        RelativePath = payloadFile.RelativePath,
                        Sha256 = ComputeSha256(target),
                        Preserved = previousFile?.Preserved ?? true
                    });
                    continue;
                }

                string? backupRelativePath = null;
                var needsBackup = File.Exists(target)
                                  && (previousFile == null
                                      || !string.Equals(
                                          ComputeSha256(target),
                                          previousFile.Sha256,
                                          StringComparison.OrdinalIgnoreCase));
                if (needsBackup)
                {
                    var backupId = Guid.NewGuid().ToString("N");
                    backupRelativePath = PathRules.NormalizeLogicalPath(
                        Path.Combine(
                            ProductInfo.BackupRootRelativePath,
                            backupId,
                            payloadFile.RelativePath));
                    var backup = PathRules.GetSafeCombinedPath(
                        gameDirectory, backupRelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, true);
                    rollback.Add(new RollbackEntry(target, null, backup));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary = target + ".mbi-new-" + Guid.NewGuid().ToString("N");
                File.Copy(payloadFile.SourcePath, temporary, true);
                rollback.Add(new RollbackEntry(target, temporary, null));
                File.Move(temporary, target, true);

                installed.Add(new BepInExInstalledFile
                {
                    RelativePath = payloadFile.RelativePath,
                    Sha256 = ComputeSha256(target),
                    BackupRelativePath = backupRelativePath,
                    Preserved = false
                });
            }

            RestorePlugins(
                runtimeReplacement.PreservedPluginsPath,
                Path.Combine(gameDirectory, "BepInEx", "plugins"));
            MarkPluginFilesPreserved(gameDirectory, installed);

            RemoveObsoleteOwnedFiles(
                gameDirectory,
                staging,
                previousFiles,
                payloadFiles.Select(file => file.RelativePath),
                rollback,
                progress);

            var manifest = new BepInExInstallManifest
            {
                ProductVersion = ProductInfo.InstallerVersion,
                BepInExVersion = ProductInfo.BepInExVersion,
                UnityVersion = ProductInfo.UnityVersion,
                GameDirectory = gameDirectory,
                Files = installed
            };
            WriteManifestAtomic(manifestPath, manifest);
            DeleteDirectoryTreeWithoutFollowingLinks(runtimeReplacement.BackupDirectory);
            progress?.Report(
                installConfigurationManager
                    ? "BepInEx 与官方 ConfigurationManager 安装完成。"
                    : "BepInEx 安装完成。");
            progress?.Report(
                "首次启动 Malody V 时会生成 IL2CPP interop 文件。");
            return OperationResult.Ok(
                installConfigurationManager
                    ? $"已安装 BepInEx {ProductInfo.BepInExVersion} 和官方 ConfigurationManager 到 {gameDirectory}。"
                    : $"已安装 BepInEx {ProductInfo.BepInExVersion} 到 {gameDirectory}。");
        }
        catch (Exception exception)
        {
            RollBack(rollback);
            if (runtimeReplacement is not null)
            {
                RestorePreviousRuntime(gameDirectory, runtimeReplacement);
            }
            return OperationResult.Fail(exception.Message);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    public OperationResult Verify(string gameDirectory)
    {
        if (!GameLocator.IsGameDirectory(gameDirectory, out var validationError))
        {
            return OperationResult.Fail(validationError);
        }

        var manifestPath = PathRules.GetSafeCombinedPath(
            Path.GetFullPath(gameDirectory), ProductInfo.ManifestRelativePath);
        var manifest = ReadManifest(manifestPath);
        if (manifest == null)
        {
            return OperationResult.Fail("没有找到 BepInEx 安装清单。");
        }

        var invalid = manifest.Files.Count(file =>
            !file.Preserved
            && (!File.Exists(PathRules.GetSafeCombinedPath(
                    gameDirectory, file.RelativePath))
                || !string.Equals(
                    ComputeSha256(PathRules.GetSafeCombinedPath(
                        gameDirectory, file.RelativePath)),
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase)));
        return invalid == 0
            ? OperationResult.Ok("BepInEx 文件清单校验通过。")
            : OperationResult.Fail($"{invalid} 个 BepInEx 文件缺失或已修改。");
    }

    public OperationResult Uninstall(
        string gameDirectory,
        IProgress<string>? progress = null)
    {
        if (!Directory.Exists(gameDirectory))
        {
            return OperationResult.Fail("游戏目录不存在。");
        }

        gameDirectory = Path.GetFullPath(gameDirectory);
        var manifestPath = PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.ManifestRelativePath);
        var manifest = ReadManifest(manifestPath);
        if (manifest == null)
        {
            return OperationResult.Fail("没有找到 BepInEx 安装清单。");
        }

        var preserved = 0;
        foreach (var file in manifest.Files
                     .Where(file => !file.Preserved)
                     .OrderByDescending(file => file.RelativePath.Length))
        {
            if (IsPluginPath(file.RelativePath))
            {
                continue;
            }

            var target = PathRules.GetSafeCombinedPath(
                gameDirectory, file.RelativePath);
            if (!File.Exists(target))
            {
                RestoreBackup(gameDirectory, file);
                continue;
            }

            if (!string.Equals(
                    ComputeSha256(target),
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                preserved++;
                continue;
            }

            progress?.Report($"删除 {file.RelativePath}");
            File.Delete(target);
            RestoreBackup(gameDirectory, file);
        }

        File.Delete(manifestPath);
        RemoveEmptyTree(PathRules.GetSafeCombinedPath(
            gameDirectory, ProductInfo.BackupRootRelativePath));
        RemoveEmptyTreeExceptPlugins(PathRules.GetSafeCombinedPath(
            gameDirectory, "BepInEx"));
        return OperationResult.Ok(
            preserved == 0
                ? "BepInEx 已卸载。"
                : $"BepInEx 已卸载，保留 {preserved} 个被修改的文件。");
    }

    private static void ExtractPayload(
        string staging,
        bool installConfigurationManager)
    {
        using var payload = PayloadProvider.Open();
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        ExtractArchiveEntries(archive, staging);
        if (!installConfigurationManager)
        {
            return;
        }

        using var configurationManagerPayload =
            ConfigurationManagerProvider.Open();
        using var configurationManagerArchive = new ZipArchive(
            configurationManagerPayload, ZipArchiveMode.Read);
        ExtractArchiveEntries(configurationManagerArchive, staging);
    }

    private static void PrepareCleanRuntime(
        string gameDirectory,
        string staging,
        RuntimeReplacement replacement,
        IProgress<string>? progress)
    {
        var bepinex = Path.Combine(gameDirectory, "BepInEx");
        var plugins = Path.Combine(bepinex, "plugins");

        if (Directory.Exists(bepinex)
            && (File.GetAttributes(bepinex) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("BepInEx 是重解析点，拒绝清理不明确的目录。");
        }
        if (Directory.Exists(plugins)
            && (File.GetAttributes(plugins) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("BepInEx\\plugins 是重解析点，拒绝迁移不明确的目录。");
        }

        Directory.CreateDirectory(replacement.BackupDirectory);
        if (Directory.Exists(bepinex))
        {
            replacement.BepInExBackupPath = Path.Combine(
                replacement.BackupDirectory, "BepInEx");
            Directory.Move(bepinex, replacement.BepInExBackupPath);
            replacement.BepInExMoved = true;
        }

        if (replacement.BepInExBackupPath is not null)
        {
            var oldPlugins = Path.Combine(replacement.BepInExBackupPath, "plugins");
            if (Directory.Exists(oldPlugins))
            {
                replacement.PreservedPluginsPath = Path.Combine(staging, "preserved-plugins");
                CopyDirectoryWithoutLinks(oldPlugins, replacement.PreservedPluginsPath);
                progress?.Report("已临时保留 BepInEx\\plugins。");
            }
        }

        foreach (var rootFile in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" })
        {
            var path = Path.Combine(gameDirectory, rootFile);
            if (!File.Exists(path))
            {
                continue;
            }

            var backupPath = Path.Combine(replacement.BackupDirectory, rootFile);
            File.Move(path, backupPath);
            replacement.RootFileBackups.Add((path, backupPath));
            progress?.Report($"已暂存旧 Doorstop 文件 {rootFile}。");
        }
    }

    private static void RestorePlugins(string? preservedPlugins, string target)
    {
        if (preservedPlugins is null || !Directory.Exists(preservedPlugins))
        {
            return;
        }

        CopyDirectoryWithoutLinks(preservedPlugins, target);
    }

    private static void CopyDirectoryWithoutLinks(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"插件目录包含重解析点，拒绝迁移：{entry}");
            }

            var target = Path.Combine(destination, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyDirectoryWithoutLinks(entry, target);
            }
            else
            {
                File.Copy(entry, target, true);
            }
        }
    }

    private static void MarkPluginFilesPreserved(
        string gameDirectory,
        IEnumerable<BepInExInstalledFile> installed)
    {
        foreach (var file in installed.Where(file => IsPluginPath(file.RelativePath)))
        {
            var path = PathRules.GetSafeCombinedPath(gameDirectory, file.RelativePath);
            if (File.Exists(path))
            {
                file.Sha256 = ComputeSha256(path);
                file.Preserved = true;
            }
        }
    }

    private static bool IsPluginPath(string relativePath) =>
        PathRules.NormalizeLogicalPath(relativePath).StartsWith(
            "BepInEx/plugins/", StringComparison.OrdinalIgnoreCase);

    private static void RestorePreviousRuntime(
        string gameDirectory,
        RuntimeReplacement replacement)
    {
        var bepinex = Path.Combine(gameDirectory, "BepInEx");
        if (replacement.BepInExMoved && Directory.Exists(bepinex))
        {
            DeleteDirectoryTreeWithoutFollowingLinks(bepinex);
        }

        if (replacement.BepInExMoved
            && replacement.BepInExBackupPath is not null
            && Directory.Exists(replacement.BepInExBackupPath))
        {
            Directory.Move(replacement.BepInExBackupPath, bepinex);
        }

        foreach (var (originalPath, backupPath) in replacement.RootFileBackups)
        {
            if (File.Exists(originalPath))
            {
                File.Delete(originalPath);
            }
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, originalPath);
            }
        }

        DeleteDirectoryTreeWithoutFollowingLinks(replacement.BackupDirectory);
    }

    private static void DeleteDirectoryTreeWithoutFollowingLinks(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(entry, false);
                }
                else
                {
                    DeleteDirectoryTreeWithoutFollowingLinks(entry);
                }
            }
            else
            {
                File.Delete(entry);
            }
        }

        Directory.Delete(directory, false);
    }

    private static void RemoveEmptyTreeExceptPlugins(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            if (Path.GetFileName(child).Equals("plugins", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            RemoveEmptyTree(child);
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private static void ExtractArchiveEntries(
        ZipArchive archive,
        string staging)
    {
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var relative = PathRules.NormalizeLogicalPath(entry.FullName);
            var target = PathRules.GetSafeCombinedPath(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open();
            using var destination = File.Create(target);
            source.CopyTo(destination);
        }
    }

    private static void ValidateOwnership(
        string gameDirectory,
        IEnumerable<PayloadFile> payloadFiles,
        IReadOnlyDictionary<string, BepInExInstalledFile> previous)
    {
        foreach (var file in payloadFiles)
        {
            var target = PathRules.GetSafeCombinedPath(
                gameDirectory, file.RelativePath);
            if (!File.Exists(target)
                || previous.ContainsKey(file.RelativePath)
                || IsMutableFile(file.RelativePath))
            {
                continue;
            }

            throw new InvalidOperationException(
                file.RelativePath.Equals(
                    "winhttp.dll", StringComparison.OrdinalIgnoreCase)
                    ? "拒绝覆盖未归本安装器所有的 winhttp.dll。"
                    : $"拒绝覆盖未归本安装器所有的文件：{file.RelativePath}");
        }
    }

    private static bool IsMutableFile(string relativePath)
    {
        var normalized = PathRules.NormalizeLogicalPath(relativePath);
        return normalized.StartsWith(
                   "BepInEx/config/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(
                   "BepInEx/plugins/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(
                   "BepInEx/interop/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(
                   "BepInEx/unity-libs/", StringComparison.OrdinalIgnoreCase);
    }

    private static void RestoreBackup(
        string gameDirectory,
        BepInExInstalledFile file)
    {
        if (string.IsNullOrWhiteSpace(file.BackupRelativePath))
        {
            return;
        }

        var backup = PathRules.GetSafeCombinedPath(
            gameDirectory, file.BackupRelativePath);
        var target = PathRules.GetSafeCombinedPath(
            gameDirectory, file.RelativePath);
        if (!File.Exists(backup))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(backup, target, true);
    }

    private static void RemoveObsoleteOwnedFiles(
        string gameDirectory,
        string staging,
        IReadOnlyDictionary<string, BepInExInstalledFile> previousFiles,
        IEnumerable<string> currentPaths,
        ICollection<RollbackEntry> rollback,
        IProgress<string>? progress)
    {
        var current = currentPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previous in previousFiles.Values)
        {
            if (current.Contains(previous.RelativePath)
                || IsPluginPath(previous.RelativePath)
                || previous.Preserved)
            {
                continue;
            }

            var target = PathRules.GetSafeCombinedPath(
                gameDirectory, previous.RelativePath);
            if (!File.Exists(target)
                || !string.Equals(
                    ComputeSha256(target),
                    previous.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var backup = PathRules.GetSafeCombinedPath(
                staging,
                Path.Combine(
                    "rollback-obsolete",
                    previous.RelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(target, backup, true);
            rollback.Add(new RollbackEntry(target, null, backup));
            progress?.Report($"移除已取消的组件文件 {previous.RelativePath}");
            File.Delete(target);
        }
    }

    private static void WriteManifestAtomic(
        string path,
        BepInExInstallManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(manifest, JsonDefaults.Options));
        File.Move(temporary, path, true);
    }

    private static BepInExInstallManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BepInExInstallManifest>(
            File.ReadAllText(path), JsonDefaults.Options);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void RollBack(IEnumerable<RollbackEntry> entries)
    {
        foreach (var entry in entries.Reverse())
        {
            try
            {
                if (entry.TemporaryPath != null
                    && File.Exists(entry.TemporaryPath))
                {
                    File.Delete(entry.TemporaryPath);
                }

                if (entry.BackupPath != null)
                {
                    if (File.Exists(entry.TargetPath))
                    {
                        File.Delete(entry.TargetPath);
                    }

                    if (File.Exists(entry.BackupPath))
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(entry.TargetPath)!);
                        File.Move(entry.BackupPath, entry.TargetPath, true);
                    }
                }
                else if (entry.TemporaryPath != null
                         && File.Exists(entry.TargetPath))
                {
                    File.Delete(entry.TargetPath);
                }
            }
            catch
            {
            }
        }
    }

    private static void RemoveEmptyTree(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            RemoveEmptyTree(child);
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            var root = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(directory);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(full))
            {
                Directory.Delete(full, true);
            }
        }
        catch
        {
        }
    }
}

internal sealed record PayloadFile(string SourcePath, string RelativePath);

internal sealed class RuntimeReplacement
{
    public RuntimeReplacement(string backupDirectory)
    {
        BackupDirectory = backupDirectory;
    }

    public string BackupDirectory { get; }
    public string? BepInExBackupPath { get; set; }
    public bool BepInExMoved { get; set; }
    public string? PreservedPluginsPath { get; set; }
    public List<(string OriginalPath, string BackupPath)> RootFileBackups { get; } = new();
}

internal sealed record RollbackEntry(
    string TargetPath,
    string? TemporaryPath,
    string? BackupPath);

internal sealed class BepInExInstallManifest
{
    public int FormatVersion { get; set; } = 1;
    public string ProductVersion { get; set; } = "";
    public string BepInExVersion { get; set; } = "";
    public string UnityVersion { get; set; } = "";
    public string GameDirectory { get; set; } = "";
    public List<BepInExInstalledFile> Files { get; set; } = new();
}

internal sealed class BepInExInstalledFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? BackupRelativePath { get; set; }
    public bool Preserved { get; set; }
}

internal sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
