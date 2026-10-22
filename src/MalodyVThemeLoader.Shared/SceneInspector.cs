using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MalodyVThemeLoader.Shared;

public sealed class ThemeNodeRule
{
    public string Scene { get; set; } = "";
    public string Address { get; set; } = "";
    public string Hierarchy { get; set; } = "";
    public string ImageFile { get; set; } = "";
    public string ImageName { get; set; } = "";
    public string VideoFile { get; set; } = "";
    public string VideoName { get; set; } = "";
    public float VideoOpacity { get; set; } = 1;
    public float Width { get; set; }
    public float Height { get; set; }
    public string Fit { get; set; } = "stretch";
    public Float4? Border { get; set; }
}

public static class SceneNodeAddress
{
    // Count only siblings with the same name. Unrelated inserted children do
    // not change this address; a changed duplicate count fails to match.
    public static string Segment(string name, int occurrence, int count) => Uri.EscapeDataString(name) + "#" + occurrence + ":" + count;
    public static bool IsValid(string? address)
    {
        if (string.IsNullOrEmpty(address) || address.Length > 4096) return false;
        var parts = address.Split('/');
        if (parts.Length > 96) return false;
        foreach (var part in parts)
        {
            var hash = part.LastIndexOf('#'); var colon = part.LastIndexOf(':');
            if (hash < 0 || colon <= hash || !int.TryParse(part.Substring(hash + 1, colon - hash - 1), out var index)
                || !int.TryParse(part.Substring(colon + 1), out var count) || index < 0 || count <= index || count > 100000) return false;
            try { if (Uri.EscapeDataString(Uri.UnescapeDataString(part.Substring(0, hash))) != part.Substring(0, hash)) return false; }
            catch { return false; }
        }
        return true;
    }
}

public sealed class InspectorControl
{
    public string Client { get; set; } = "";
    public DateTime LeaseUtc { get; set; }
    public bool Enabled { get; set; }
    public bool ShowNames { get; set; } = true;
    public bool ShowFrames { get; set; } = true;
    public bool IncludeInactive { get; set; }
    public string Runtime { get; set; } = "";
    public int SelectedId { get; set; }
    public long SelectRequest { get; set; }
    public bool IsLive => Enabled && Client.Length > 0 && LeaseUtc > DateTime.UtcNow.AddSeconds(-5) && LeaseUtc < DateTime.UtcNow.AddSeconds(5);
}

public sealed class InspectorSnapshot
{
    public string Runtime { get; set; } = "";
    public string Client { get; set; } = "";
    public string PluginVersion { get; set; } = "";
    public string Theme { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
    public long SelectionRevision { get; set; }
    public string SelectionOrigin { get; set; } = "";
    public int SelectedId { get; set; }
    public bool Truncated { get; set; }
    public List<InspectorNode> Nodes { get; set; } = new();
}

public sealed class InspectorNode
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Scene { get; set; } = "";
    public string Address { get; set; } = "";
    public string Hierarchy { get; set; } = "";
    public string Component { get; set; } = "";
    public string LogicalId { get; set; } = "";
    public bool Active { get; set; }
    public bool CanDecorate { get; set; }
    public bool HasBounds { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string DecorationStatus { get; set; } = "";
}

public static class InspectorFiles
{
    public const string ControlName = "inspector/control.json";
    public const string SnapshotName = "inspector/snapshot.json";
    public static T? Read<T>(string root, string name) where T : class
    {
        var path = Path.Combine(root, name);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<T>(stream, JsonDefaults.Options);
        }
        catch { return null; }
    }
    public static void Write<T>(string root, string name, T value)
    {
        var target = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options)); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
