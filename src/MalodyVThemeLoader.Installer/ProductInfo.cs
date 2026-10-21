using System.Reflection;

namespace MalodyVThemeLoader.Installer;

internal static class ProductInfo
{
    public const string Name = "ThemeLoader";
    public static string Version =>
        typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? "1.3.0";
    public const string ManifestRelativePath = "ThemeLoader/install-manifest.json";
    public const string BackupRootRelativePath = "ThemeLoader/.backup";
    public const string ManagerRelativePath =
        "ThemeLoader/MalodyVThemeManager.exe";
}
