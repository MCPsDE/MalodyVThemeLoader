using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace MalodyVThemeLoader.Installer;

internal static class GameLocator
{
    private static readonly string[] RequiredFiles =
    {
        "Malody V.exe",
        "UnityPlayer.dll",
        "GameAssembly.dll"
    };

    public static string? FindGameDirectory()
    {
        foreach (var candidate in FindCandidates())
        {
            if (IsGameDirectory(candidate, out _))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static bool IsGameDirectory(string path, out string error)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            error = "The selected directory does not exist.";
            return false;
        }

        foreach (var file in RequiredFiles)
        {
            if (!File.Exists(Path.Combine(path, file)))
            {
                error = $"Missing {file}.";
                return false;
            }
        }

        var dataPath = Path.Combine(path, "Malody V_Data");
        if (!Directory.Exists(dataPath)
            || !Directory.Exists(Path.Combine(dataPath, "il2cpp_data")))
        {
            error = "Missing Malody V_Data or IL2CPP data.";
            return false;
        }

        error = "";
        return true;
    }

    private static IEnumerable<string> FindCandidates()
    {
        yield return AppContext.BaseDirectory;

        foreach (var steamRoot in FindSteamRoots())
        {
            yield return Path.Combine(
                steamRoot, "steamapps", "common", "MalodyV");
        }
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registryPath in new[]
                 {
                     @"HKEY_CURRENT_USER\Software\Valve\Steam",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"
                 })
        {
            var value = Registry.GetValue(registryPath, "SteamPath", null) as string
                        ?? Registry.GetValue(registryPath, "InstallPath", null) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                roots.Add(value);
            }
        }

        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");

        foreach (var root in roots.ToArray())
        {
            var libraries = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraries))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(
                         File.ReadAllText(libraries),
                         "\"path\"\\s+\"(?<path>[^\"]+)\"",
                         RegexOptions.IgnoreCase))
            {
                roots.Add(match.Groups["path"].Value.Replace(@"\\", @"\"));
            }
        }

        return roots;
    }
}
