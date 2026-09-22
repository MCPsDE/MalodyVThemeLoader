using System.Reflection;

namespace MalodyVBepInExInstaller;

internal static class PayloadProvider
{
    private const string ResourceName = "MalodyVBepInExInstaller.Payload.zip";

    public static Stream Open()
    {
        var overridePath = Environment.GetEnvironmentVariable("MALODY_BEPINEX_PAYLOAD");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.OpenRead(overridePath);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException("安装器不包含 BepInEx payload。");
    }
}
