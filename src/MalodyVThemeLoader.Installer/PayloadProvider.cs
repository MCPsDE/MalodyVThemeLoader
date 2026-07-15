using System.Reflection;

namespace MalodyVThemeLoader.Installer;

internal static class PayloadProvider
{
    private const string ResourceName = "MalodyVThemeLoader.Payload.zip";

    public static Stream Open()
    {
        var overridePath = Environment.GetEnvironmentVariable(
            "MALODY_THEMELOADER_PAYLOAD");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.OpenRead(overridePath);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException(
                   "The installer does not contain an embedded payload.");
    }
}
