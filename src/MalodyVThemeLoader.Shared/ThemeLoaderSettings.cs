namespace MalodyVThemeLoader.Shared;

public sealed class ThemeLoaderSettings
{
    public const string RelativePath = "ThemeLoader/settings.json";

    public int FormatVersion { get; set; } = 1;
    public bool RealtimeMode { get; set; }
    public bool ControlMode { get; set; }
    public string? ControlBackgroundImage { get; set; }
}
