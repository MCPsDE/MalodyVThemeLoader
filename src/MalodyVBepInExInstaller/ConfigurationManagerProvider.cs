using System.Reflection;

namespace MalodyVBepInExInstaller;

internal static class ConfigurationManagerProvider
{
    private const string ResourceName =
        "MalodyVBepInExInstaller.ConfigurationManager.zip";

    public static Stream Open()
    {
        var overridePath = Environment.GetEnvironmentVariable(
            "MALODY_BEPINEX_CONFIGURATION_MANAGER_PAYLOAD");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.OpenRead(overridePath);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException(
                   "安装器不包含官方 ConfigurationManager payload。");
    }
}
