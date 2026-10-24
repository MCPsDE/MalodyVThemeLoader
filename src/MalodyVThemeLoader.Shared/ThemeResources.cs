using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MalodyVThemeLoader.Shared;

public sealed class ThemeResources
{
    public const string FileName = "resources.json";
    public int FormatVersion { get; set; } = 1;
    public List<ThemeImageRule> Images { get; set; } = new();
    public List<ThemeAudioRule> Audio { get; set; } = new();
    public List<ThemeNodeRule> Nodes { get; set; } = new();
    public List<ThemeTextRule> Texts { get; set; } = new();
    public ThemeOpening? Opening { get; set; }

    public static ThemeResources Read(string themeRoot)
    {
        var path = Path.Combine(themeRoot, FileName);
        if (!File.Exists(path)) return new ThemeResources();
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("资源配置过大。");
        var value = JsonSerializer.Deserialize<ThemeResources>(File.ReadAllText(path), JsonDefaults.Options)
            ?? throw new InvalidDataException("资源配置为空。");
        value.Validate();
        return value;
    }

    public void Write(string themeRoot)
    {
        Validate();
        var target = Path.Combine(themeRoot, FileName);
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonDefaults.Options));
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Validate()
    {
        if (FormatVersion != 1 || Images == null || Audio == null || Nodes == null || Texts == null || Images.Count + Audio.Count + Nodes.Count + Texts.Count > 10000)
            throw new InvalidDataException("资源配置版本或内容无效。");
        Opening?.Validate();
        var textKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in Texts)
        {
            if (text == null) throw new InvalidDataException("文字规则无效。");
            text.Validate();
            if (!textKeys.Add(text.Identity)) throw new InvalidDataException("存在重复的文字范围。");
        }
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in Nodes)
        {
            if (rule == null) throw new InvalidDataException("节点规则无效。");
            CheckScene(rule.Scene);
            if (rule.Scene.Length == 0 || !SceneNodeAddress.IsValid(rule.Address) || rule.Hierarchy == null || rule.Hierarchy.Length > 4096)
                throw new InvalidDataException("节点位置无效，请重新从在线层级中选择。");
            CheckPath(rule.ImageFile, true); CheckPath(rule.VideoFile, true);
            if (rule.ImageFile.Length + rule.VideoFile.Length == 0) throw new InvalidDataException("请添加底图或视频。");
            if (rule.VideoFile.Length > 0 && !rule.VideoFile.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("节点视频需使用 WebM。");
            if (!float.IsFinite(rule.VideoOpacity) || rule.VideoOpacity < 0 || rule.VideoOpacity > 1
                || !float.IsFinite(rule.Width) || !float.IsFinite(rule.Height) || rule.Width < 0 || rule.Height < 0 || rule.Width > 8192 || rule.Height > 8192)
                throw new InvalidDataException("节点尺寸或透明度超出范围。");
            if (rule.Fit is not "stretch" and not "contain" and not "cover") throw new InvalidDataException("节点填充方式无效。");
            if (rule.Border != null && new[] { rule.Border.X, rule.Border.Y, rule.Border.Z, rule.Border.W }.Any(v => !float.IsFinite(v) || v < 0 || v > 16384))
                throw new InvalidDataException("底图切片边距无效。");
            if (!nodeKeys.Add(rule.Scene + "|" + rule.Address)) throw new InvalidDataException("存在重复的节点装饰。");
        }
        foreach (var rule in Images)
        {
            if (rule == null) throw new InvalidDataException("图片规则无效。");
            CheckPath(rule.LogicalId); CheckPath(rule.File, true); CheckScene(rule.Scene);
            if (rule.Hierarchy == null || rule.Hierarchy.Length > 1024 || rule.Hierarchy.Contains('\\') || rule.Hierarchy.StartsWith('/') || rule.Hierarchy.EndsWith('/'))
                throw new InvalidDataException("控件路径无效，请使用 / 分隔。");
            if (rule.Hierarchy.Length > 0 && rule.Scene.Length == 0) throw new InvalidDataException("指定控件前请先选择场景。");
            if (rule.NodeAddress == null || rule.NodeAddress.Length > 0 && (!SceneNodeAddress.IsValid(rule.NodeAddress) || rule.Scene.Length == 0 || rule.Hierarchy.Length == 0))
                throw new InvalidDataException("图片节点位置无效。");
            if (rule.Component is not "" and not "Image" and not "RawImage" and not "SpriteRenderer") throw new InvalidDataException("图片控件类型无效。");
            if (rule.Mode is not "" and not "sliced" and not "simple") throw new InvalidDataException("图片显示模式无效。");
            if (rule.Border != null && new[] { rule.Border.X, rule.Border.Y, rule.Border.Z, rule.Border.W }.Any(v => !float.IsFinite(v) || v < 0 || v > 16384))
                throw new InvalidDataException("切片边距必须是非负像素值。");
            if (rule.Mode == "sliced" && rule.Border == null) throw new InvalidDataException("九宫格切片缺少边距。");
            if (!keys.Add("image|" + rule.LogicalId + "|" + rule.Scene + "|" + rule.Hierarchy + "|" + rule.Component + "|" + rule.NodeAddress)) throw new InvalidDataException("存在重复的图片覆盖范围。");
        }
        foreach (var rule in Audio)
        {
            if (rule == null || !UiAudioEvents.IsSupported(rule.Event)) throw new InvalidDataException("音频事件无效。");
            CheckPath(rule.File); CheckScene(rule.Scene);
            if (!keys.Add("audio|" + rule.Event + "|" + rule.Scene)) throw new InvalidDataException("存在重复的音频覆盖范围。");
        }
    }

    public ThemeImageRule? ResolveImage(string logicalId, string scene = "", string hierarchy = "", string component = "", string nodeAddress = "")
    {
        var matches = Images.Where(r => r.LogicalId.Equals(logicalId, StringComparison.OrdinalIgnoreCase)
            && (r.Scene.Length == 0 || r.Scene.Equals(scene, StringComparison.Ordinal))
            && (r.Hierarchy.Length == 0 || MatchHierarchy(r.Hierarchy, hierarchy))
            && (r.Component.Length == 0 || r.Component == component) && (r.NodeAddress.Length == 0 || r.NodeAddress == nodeAddress))
            .Select(r => new { Rule = r, Priority = (r.NodeAddress.Length > 0 ? 8 : 0) + (r.Scene.Length > 0 ? 4 : 0) + (r.Hierarchy.Length > 0 ? 2 : 0) + (r.Component.Length > 0 ? 1 : 0) })
            .OrderByDescending(r => r.Priority).ToList();
        // An ambiguous match must not pick an arbitrary replacement.
        return matches.Count == 0 || matches.Count > 1 && matches[0].Priority == matches[1].Priority ? null : matches[0].Rule;
    }

    public ThemeAudioRule? ResolveAudio(string eventName, string scene)
    {
        if (!UiAudioEvents.IsSupported(eventName) || !IsAllowedScene(scene)) return null;
        return Audio.FirstOrDefault(r => r.Event == eventName && r.Scene == scene)
            ?? Audio.FirstOrDefault(r => r.Event == eventName && r.Scene.Length == 0);
    }

    public static bool MatchHierarchy(string expected, string actual) => NormalizeHierarchy(expected) == NormalizeHierarchy(actual);

    public static string NormalizeHierarchy(string value) => string.Join("/", value.Split('/').Select(part =>
        part.EndsWith("(Clone)", StringComparison.Ordinal) ? part[..^7].TrimEnd() : part));

    public static bool IsAllowedScene(string scene) => !string.IsNullOrWhiteSpace(scene)
        && !new[] { "ScenePlay", "SceneReplay", "SceneRoomWatch", "SceneWatchSub" }.Contains(scene, StringComparer.OrdinalIgnoreCase)
        && !scene.StartsWith("PlayMode", StringComparison.OrdinalIgnoreCase);

    internal static void CheckScene(string scene)
    {
        if (scene == null || scene.Length > 100 || scene.Contains('/') || scene.Contains('\\') || scene.Any(char.IsControl)
            || scene.Length > 0 && !IsAllowedScene(scene)) throw new InvalidDataException("请选择非游玩场景。");
    }

    internal static void CheckPath(string value, bool optional = false)
    {
        if (optional && value == "") return;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || Path.IsPathRooted(value)
            || value.Split('/').Any(s => s is "" or "." or ".." || s.Contains(':') || s.Any(char.IsControl)))
            throw new InvalidDataException("资源路径必须是主题内的相对路径。");
    }
}

public sealed class ThemeImageRule
{
    public string NodeAddress { get; set; } = "";
    public string LogicalId { get; set; } = "";
    public string Scene { get; set; } = "";
    public string Hierarchy { get; set; } = "";
    public string Component { get; set; } = "";
    public string File { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Float4? Border { get; set; }
    public string Mode { get; set; } = "";
}

public sealed class ThemeAudioRule
{
    public string Event { get; set; } = "";
    public string Scene { get; set; } = "";
    public string File { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public static class UiAudioEvents
{
    public static readonly string[] Effects = { "Click", "MsgOK", "MsgFail", "Switch", "Back", "Cancel", "Screenshot", "MsgNotify", "ClickLight" };
    public static bool IsSupported(string value) => Array.IndexOf(Effects, value) >= 0 || value == "music_finish";
}
