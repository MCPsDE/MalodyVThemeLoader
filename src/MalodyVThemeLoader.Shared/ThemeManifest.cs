using System;
using System.Collections.Generic;

namespace MalodyVThemeLoader.Shared;

public sealed class ThemeManifest
{
    public int FormatVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public string Website { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public string ImageRoot { get; set; } = "images";
    public string FontRoot { get; set; } = "fonts";
    public string Live2dRoot { get; set; } = "live2d";
    public string AudioRoot { get; set; } = "audio";
    public List<FontMapping> FontMappings { get; set; } = new();
}

public sealed class FontMapping
{
    public List<string> SourceNames { get; set; } = new();
    public string File { get; set; } = "";
    public string Family { get; set; } = "";
    public int Size { get; set; } = 90;
    public int AtlasSize { get; set; } = 1024;
}
