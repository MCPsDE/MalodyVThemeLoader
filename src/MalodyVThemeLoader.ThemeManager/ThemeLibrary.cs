using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class ThemeLibrary
{
    private readonly string gameRoot;
    private readonly string themesRoot;
    private readonly string activeThemePath;
    private readonly string settingsPath;

    public ThemeLibrary(string gameRoot)
    {
        this.gameRoot = Path.GetFullPath(gameRoot);
        themesRoot = PathRules.GetSafeCombinedPath(
            this.gameRoot, "ThemeLoader/themes");
        activeThemePath = PathRules.GetSafeCombinedPath(
            this.gameRoot, "ThemeLoader/active-theme.txt");
        settingsPath = PathRules.GetSafeCombinedPath(
            this.gameRoot, ThemeLoaderSettings.RelativePath);
        Directory.CreateDirectory(themesRoot);
    }

    public IReadOnlyList<ThemeItem> GetThemes()
    {
        var items = new List<ThemeItem>();
        foreach (var package in Directory.EnumerateFiles(
                     themesRoot, "*", SearchOption.TopDirectoryOnly).Where(ThemePackage.IsPackagePath))
        {
            try
            {
                var info = ThemePackage.Inspect(package);
                items.Add(new ThemeItem(
                    Path.GetFileNameWithoutExtension(package),
                    package,
                    false,
                    info));
            }
            catch (Exception exception)
            {
                items.Add(ThemeItem.Invalid(
                    Path.GetFileNameWithoutExtension(package),
                    package,
                    exception.Message));
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(themesRoot))
        {
            var manifestPath = Path.Combine(directory, "theme.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var info = ThemePackage.InspectDirectory(directory);
                items.Add(new ThemeItem(
                    Path.GetFileName(directory),
                    directory,
                    true,
                    info));
            }
            catch (Exception exception)
            {
                items.Add(ThemeItem.Invalid(
                    Path.GetFileName(directory),
                    directory,
                    exception.Message));
            }
        }

        if (!items.Any(item => item.IsDefault))
        {
            items.Add(ThemeItem.VirtualDefault());
        }

        return items
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.IsLegacyDirectory)
                .ThenBy(item => Path.GetExtension(item.SourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? 0 : 1).First())
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string GetActiveTheme()
    {
        var active = File.Exists(activeThemePath)
            ? File.ReadAllText(activeThemePath).Trim()
            : "Default";
        return string.IsNullOrWhiteSpace(active) ? "Default" : active;
    }

    public ThemeLoaderSettings GetSettings()
    {
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

    public void SetRealtimeMode(bool enabled)
    {
        var settings = GetSettings();
        settings.RealtimeMode = enabled;
        WriteSettings(settings);

    }

    public void SetControlMode(bool enabled)
    {
        var settings = GetSettings();
        settings.ControlMode = enabled;
        WriteSettings(settings);

    }

    public string SetControlBackgroundImage(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
        {
            throw new InvalidDataException("控制模式底图仅支持 PNG 或 JPG。");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("底图文件不存在。", sourcePath);
        }

        var relativePath = "control-background" + extension;
        var target = PathRules.GetSafeCombinedPath(
            gameRoot, "ThemeLoader/" + relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(sourcePath, temporary, true);
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        var settings = GetSettings();
        settings.ControlBackgroundImage = relativePath;
        WriteSettings(settings);

        return target;
    }

    public string SetControlTopImage(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
        {
            throw new InvalidDataException("控制模式顶图仅支持 PNG 或 JPG。");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("顶图文件不存在。", sourcePath);
        }

        var relativePath = "control-top" + extension;
        var target = PathRules.GetSafeCombinedPath(
            gameRoot, "ThemeLoader/" + relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(sourcePath, temporary, true);
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        var settings = GetSettings();
        settings.ControlTopImage = relativePath;
        WriteSettings(settings);

        return target;
    }

    public ThemeItem Import(string sourcePath, bool overwrite)
    {
        if (!ThemePackage.IsPackagePath(sourcePath))
            throw new InvalidDataException("请选择 ZIP 主题包。");
        var info = ThemePackage.Inspect(sourcePath);

        var target = PathRules.GetSafeCombinedPath(themesRoot, info.Manifest.Id);
        var legacyZipTarget = PathRules.GetSafeCombinedPath(
            themesRoot, ThemePackage.GetPackageFileName(info.Manifest));
        if ((Directory.Exists(target) || File.Exists(legacyZipTarget)) && !overwrite)
        {
            throw new ThemeAlreadyExistsException(info.Manifest.Name);
        }

        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        var backupDirectory = target + ".old-" + Guid.NewGuid().ToString("N");
        var backupZip = legacyZipTarget + ".old-" + Guid.NewGuid().ToString("N");
        var movedDirectory = false;
        var movedZip = false;
        try
        {
            ThemePackage.ExtractToDirectory(sourcePath, temporary);
            ThemePackage.InspectDirectory(temporary);
            if (Directory.Exists(target))
            {
                Directory.Move(target, backupDirectory);
                movedDirectory = true;
            }
            if (File.Exists(legacyZipTarget))
            {
                File.Move(legacyZipTarget, backupZip);
                movedZip = true;
            }
            Directory.Move(temporary, target);
            DeleteDirectoryIfPresent(backupDirectory);
            DeleteFileIfPresent(backupZip);
        }
        catch
        {
            DeleteDirectoryIfPresent(target);
            DeleteDirectoryIfPresent(temporary);
            if (movedDirectory && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, target);
            if (movedZip && File.Exists(backupZip))
                File.Move(backupZip, legacyZipTarget);
            throw;
        }
        finally
        {
            DeleteDirectoryIfPresent(temporary);
            DeleteDirectoryIfPresent(backupDirectory);
            DeleteFileIfPresent(backupZip);
        }

        return new ThemeItem(
            info.Manifest.Id,
            target,
            true,
            ThemePackage.InspectDirectory(target));
    }

    public void Export(ThemeItem item, string destinationPath)
    {
        EnsureValid(item);
        if (item.IsVirtual)
        {
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "MalodyVDefault-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.WriteAllText(Path.Combine(temporaryDirectory, "theme.json"), item.Info!.RawManifestJson);
                ThemePackage.CreateFromDirectory(temporaryDirectory, destinationPath);
            }
            finally { Directory.Delete(temporaryDirectory, true); }
            return;
        }
        if (item.IsLegacyDirectory)
        {
            ThemePackage.CreateFromDirectory(item.SourcePath, destinationPath);
            return;
        }

        var temporary = destinationPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(item.SourcePath, temporary, true);
            ThemePackage.Inspect(temporary);
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

    public void Activate(ThemeItem item)
    {
        EnsureValid(item);
        File.WriteAllText(activeThemePath, item.Key + Environment.NewLine);

    }

    public void Remove(ThemeItem item)
    {
        EnsureValid(item);
        if (item.IsVirtual) return;

        if (item.IsLegacyDirectory)
        {
            Directory.Delete(item.SourcePath, true);
        }
        else
        {
            File.Delete(item.SourcePath);
        }

        if (string.Equals(
                GetActiveTheme(), item.Key, StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(activeThemePath, "Default" + Environment.NewLine);
        }
    }

    public int RemoveAllImported()
    {
        var removed = 0;
        foreach (var item in GetThemes().Where(item => !item.IsDefault))
        {
            if (item.IsLegacyDirectory)
            {
                Directory.Delete(item.SourcePath, true);
            }
            else
            {
                File.Delete(item.SourcePath);
            }
            removed++;
        }

        File.WriteAllText(activeThemePath, "Default" + Environment.NewLine);
        return removed;
    }

    private static void EnsureValid(ThemeItem item)
    {
        if (!item.IsValid || item.Info == null)
        {
            throw new InvalidDataException(
                item.Error.Length > 0 ? item.Error : "主题包无效。");
        }
    }

    private void WriteSettings(ThemeLoaderSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var temporary = settingsPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporary, JsonSerializer.Serialize(settings, JsonDefaults.Options));
            File.Move(temporary, settingsPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static void DeleteFileIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

internal sealed class ThemeItem
{
    public ThemeItem(
        string key,
        string sourcePath,
        bool isLegacyDirectory,
        ThemePackageInfo info)
    {
        Key = key;
        SourcePath = sourcePath;
        IsLegacyDirectory = isLegacyDirectory;
        Info = info;
        IsValid = true;
    }

    private ThemeItem(string key, string sourcePath, string error)
    {
        Key = key;
        SourcePath = sourcePath;
        Error = error;
    }

    private ThemeItem(
        string key,
        ThemePackageInfo info,
        bool isVirtual)
    {
        Key = key;
        SourcePath = "";
        Info = info;
        IsValid = true;
        IsVirtual = isVirtual;
    }

    public string Key { get; }
    public string SourcePath { get; }
    public bool IsLegacyDirectory { get; }
    public bool IsVirtual { get; }
    public ThemePackageInfo? Info { get; }
    public bool IsValid { get; }
    public string Error { get; } = "";
    public bool IsDefault =>
        string.Equals(Key, "Default", StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            Info?.Manifest.Id, "default", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => Info?.Manifest.Name ?? Key + "（无效）";

    public static ThemeItem Invalid(
        string key,
        string sourcePath,
        string error)
    {
        return new ThemeItem(key, sourcePath, error);
    }

    public static ThemeItem VirtualDefault()
    {
        var manifest = new ThemeManifest
        {
            Id = "default",
            Name = "Default",
            Version = "built-in",
            Author = "Default",
            Description = "原始外观，可导出 ZIP 模板，完整原始资源随安装器提供。",
            GameVersion = "current"
        };
        var rawJson = JsonSerializer.Serialize(manifest, JsonDefaults.Options);
        return new ThemeItem(
            "Default",
            new ThemePackageInfo("", manifest, rawJson, 0, 0),
            true);
    }
}

internal sealed class ThemeAlreadyExistsException : IOException
{
    public ThemeAlreadyExistsException(string themeName)
        : base($"主题“{themeName}”已经存在。")
    {
    }
}
