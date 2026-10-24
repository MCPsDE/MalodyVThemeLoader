using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MalodyVThemeLoader.Shared;

public sealed class ThemeTextRule
{
    public string Scene { get; set; } = "";
    public string Address { get; set; } = "";
    public string Hierarchy { get; set; } = "";
    public string Component { get; set; } = "Text";
    // -1 is an exact instance; otherwise only this explicitly chosen path segment repeats.
    public int RepeatDepth { get; set; } = -1;
    public string Color { get; set; } = "#FFFFFF";
    public string SelectedColor { get; set; } = "";
    public string HoverColor { get; set; } = "";
    public float? Opacity { get; set; }
    public string RichText { get; set; } = "preserve";
    public string Gradient { get; set; } = "preserve";
    public string[] GradientColors { get; set; } = new[] { "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF" };
    public string FaceColor { get; set; } = "";
    public string OutlineColor { get; set; } = "";
    public List<TextColorMapping> Mappings { get; set; } = new();

    public void Validate()
    {
        ThemeResources.CheckScene(Scene);
        if (string.IsNullOrEmpty(Scene) || !SceneNodeAddress.IsValid(Address)
            || Hierarchy == null || Hierarchy.Length > 4096 || Component is not "Text" and not "TMP_Text"
            || RepeatDepth < -1 || RepeatDepth >= Address.Split('/').Length)
            throw new InvalidDataException("文字节点位置无效，请重新选择。");
        TextColor.Validate(Color);
        foreach (var color in new[] { SelectedColor, HoverColor, FaceColor, OutlineColor }) TextColor.Validate(color, true);
        if (Opacity.HasValue && (!float.IsFinite(Opacity.Value) || Opacity < 0 || Opacity > 1))
            throw new InvalidDataException("文字不透明度应为 0–100%。");
        if (RichText is not "preserve" and not "uniform" || Gradient is not "preserve" and not "solid" and not "custom"
            || GradientColors == null || GradientColors.Length != 4 || Mappings == null || Mappings.Count > 32)
            throw new InvalidDataException("文字配色选项无效。");
        foreach (var color in GradientColors) TextColor.Validate(color);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in Mappings)
        {
            if (mapping == null) throw new InvalidDataException("状态配色无效。");
            TextColor.Validate(mapping.Source); TextColor.Validate(mapping.Target);
            if (!sources.Add(mapping.Source)) throw new InvalidDataException("状态原色重复。");
        }
        if (Component != "TMP_Text" && (Gradient != "preserve" || FaceColor.Length > 0 || OutlineColor.Length > 0))
            throw new InvalidDataException("渐变、面色和描边仅适用于 TMP 文字。");
    }

    public bool Matches(string scene, string address, string component)
    {
        if (Scene != scene || Component != component) return false;
        if (RepeatDepth < 0) return Address == address;
        var expected = Address.Split('/'); var actual = address.Split('/');
        if (actual.Length != expected.Length) return false;
        for (var i = 0; i < expected.Length; i++)
            if (i == RepeatDepth ? Name(expected[i]) != Name(actual[i]) : expected[i] != actual[i]) return false;
        return true;
    }
    public string Identity => Scene + "|" + Component + "|" + string.Join("/", Address.Split('/').Select((p, i) => i == RepeatDepth ? Name(p) + "#*" : p));
    public string ColorFor(string original, bool selected, bool hovered)
    {
        var color = Mappings.FirstOrDefault(m => m.Source.Equals(original, StringComparison.OrdinalIgnoreCase))?.Target ?? Color;
        if (selected && SelectedColor.Length > 0) color = SelectedColor;
        if (hovered && HoverColor.Length > 0) color = HoverColor;
        return color;
    }
    private static string Name(string segment) => segment.Substring(0, segment.LastIndexOf('#'));
}

public sealed class TextColorMapping
{
    public string Source { get; set; } = "#FFFFFF";
    public string Target { get; set; } = "#FFFFFF";
}

public static class TextColor
{
    public static void Validate(string? hex, bool optional = false)
    {
        if (optional && hex == "") return;
        if (hex == null || hex.Length != 7 || hex[0] != '#' || hex.Skip(1).Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("颜色请使用 #RRGGBB，例如 #69AFED。");
    }
    public static uint Rgb(string hex) { Validate(hex); return uint.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
}

public sealed class InspectorText
{
    public string Component { get; set; } = "";
    public string Content { get; set; } = "";
    public string Color { get; set; } = "#FFFFFF";
    public float Opacity { get; set; } = 1;
    public bool RichText { get; set; }
    public bool Gradient { get; set; }
    public string[] GradientColors { get; set; } = new[] { "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF" };
    public string FaceColor { get; set; } = "#FFFFFF";
    public string OutlineColor { get; set; } = "#000000";
}

public sealed class ThemeOpening
{
    public string File { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Kind { get; set; } = "image";
    public string Fit { get; set; } = "contain";
    public string Background { get; set; } = "#000000";
    public float Duration { get; set; } = 4;
    public void Validate()
    {
        ThemeResources.CheckPath(File);
        if (Kind is not "image" and not "video" || Kind == "video" && !File.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
            || Kind == "image" && !File.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("开屏文件格式无效。");
        if (Fit is not "contain" and not "cover" and not "stretch" || !float.IsFinite(Duration) || Duration < 1 || Duration > 30
            || DisplayName == null || DisplayName.Length > 512) throw new InvalidDataException("开屏显示设置无效。");
        TextColor.Validate(Background);
    }
}
