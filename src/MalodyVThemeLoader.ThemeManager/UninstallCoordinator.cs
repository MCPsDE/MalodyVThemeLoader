using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal static class UninstallCoordinator
{
    public static string Uninstall(
        string gameRoot,
        IProgress<string>? progress = null)
    {
        gameRoot = Path.GetFullPath(gameRoot);
        var manifestPath = PathRules.GetSafeCombinedPath(
            gameRoot, "ThemeLoader/install-manifest.json");
        var manifest = JsonSerializer.Deserialize<InstallManifest>(
            File.ReadAllText(manifestPath), JsonDefaults.Options)
            ?? throw new InvalidDataException("安装清单为空。");
        var currentExecutable = Environment.ProcessPath == null
            ? ""
            : Path.GetFullPath(Environment.ProcessPath);
        var deferredExecutable = "";
        var preserved = new List<string>();

        foreach (var file in manifest.Files
                     .OrderByDescending(file => file.RelativePath.Length))
        {
            var target = PathRules.GetSafeCombinedPath(gameRoot, file.RelativePath);
            if (!File.Exists(target))
            {
                RestoreBackup(gameRoot, file, target);
                continue;
            }

            if (!string.Equals(
                    ComputeSha256(target), file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                preserved.Add(file.RelativePath);
                continue;
            }

            if (string.Equals(
                    Path.GetFullPath(target),
                    currentExecutable,
                    StringComparison.OrdinalIgnoreCase))
            {
                deferredExecutable = target;
                continue;
            }

            progress?.Report($"移除 {file.RelativePath}");
            File.Delete(target);
            RestoreBackup(gameRoot, file, target);
        }

        foreach (var external in manifest.ExternalFiles)
        {
            if (!File.Exists(external.FullPath))
            {
                continue;
            }
            if (!string.Equals(
                    ComputeSha256(external.FullPath),
                    external.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                preserved.Add(external.FullPath);
                continue;
            }

            progress?.Report($"移除 {external.FullPath}");
            File.Delete(external.FullPath);
        }

        File.Delete(manifestPath);
        RemoveEmptyOwnedDirectories(gameRoot);
        if (!string.IsNullOrWhiteSpace(deferredExecutable))
        {
            ScheduleFinalCleanup(deferredExecutable, gameRoot);
        }

        return preserved.Count == 0
            ? "ThemeLoader 已完整卸载。管理器将在关闭后删除自身。"
            : $"ThemeLoader 已卸载，但保留了 {preserved.Count} 个被修改或不再归安装器所有的文件。";
    }

    private static void RemoveEmptyOwnedDirectories(string gameRoot)
    {
        foreach (var relative in new[]
                 {
                     "BepInEx/plugins/MalodyVThemeLoader",
                     "BepInEx",
                     "dotnet",
                     "ThemeLoader"
                 })
        {
            RemoveEmptyTree(PathRules.GetSafeCombinedPath(gameRoot, relative));
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
            try
            {
                Directory.Delete(directory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void RestoreBackup(
        string gameRoot,
        InstalledFile file,
        string target)
    {
        if (string.IsNullOrWhiteSpace(file.BackupRelativePath))
        {
            return;
        }

        var backup = PathRules.GetSafeCombinedPath(
            gameRoot, file.BackupRelativePath);
        if (!File.Exists(backup))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(backup, target, true);
    }

    private static void ScheduleFinalCleanup(
        string executable,
        string gameRoot)
    {
        var themeLoader = PathRules.GetSafeCombinedPath(gameRoot, "ThemeLoader");
        var command = $"/d /c ping 127.0.0.1 -n 3 > nul & "
                      + $"del /f /q \"{Escape(executable)}\" & "
                      + $"rmdir \"{Escape(themeLoader)}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = command,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
