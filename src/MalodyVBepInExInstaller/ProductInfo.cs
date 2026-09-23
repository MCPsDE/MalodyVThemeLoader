using System.Reflection;

namespace MalodyVBepInExInstaller;

internal static class ProductInfo
{
    public const string Name = "Malody V BepInEx Installer";
    public const string BepInExVersion = "6.0.0-be.785";
    public const string UnityVersion = "2022.3.62f2";
    public const string ConfigurationManagerVersion = "19.0";
    public const string ManifestRelativePath = "BepInEx/MalodyV-BepInEx.install.json";
    public const string BackupRootRelativePath = "BepInEx/.malody-bepinex-backup";

    public static string InstallerVersion =>
        typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "1.0.0";
}
