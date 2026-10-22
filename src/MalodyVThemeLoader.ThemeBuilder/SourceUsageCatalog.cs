using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeBuilder;

internal static class SourceUsageCatalog
{
    private static readonly Regex Documents = new(@"^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n(.*?)(?=^--- !u!|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
    private static string Value(string text, string name) => Regex.Match(text, @"^  " + Regex.Escape(name) + @": (.*)$", RegexOptions.Multiline).Groups[1].Value.Trim();
    private static string FileId(string text, string name) => Regex.Match(Value(text, name), @"fileID: (-?\d+)").Groups[1].Value;
    private static string Guid(string text) => Regex.Match(text, @"guid: ([a-f0-9]{32})").Groups[1].Value;

    public static void Build(string assetsRoot, AssetCatalog catalog, string? audioRoot)
    {
        var imageGuids = new Dictionary<string, ImageCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in catalog.Images)
        {
            var meta = PathRules.GetSafeCombinedPath(assetsRoot, image.LogicalId) + ".meta";
            if (File.Exists(meta))
            {
                var guid = Guid(File.ReadAllText(meta));
                if (guid.Length > 0) imageGuids.TryAdd(guid, image);
            }
        }
        var sceneRoot = Path.Combine(assetsRoot, "Scenes");
        foreach (var file in Directory.EnumerateFiles(sceneRoot, "*.unity", SearchOption.AllDirectories).OrderBy(v => v, StringComparer.Ordinal))
        {
            var scene = Path.GetFileNameWithoutExtension(file);
            if (!ThemeResources.IsAllowedScene(scene)) continue;
            catalog.Scenes.Add(scene);
            AddUsages(File.ReadAllText(file), scene, imageGuids);
        }
        catalog.Scenes = catalog.Scenes.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();

        // Resolve the actual serialized SoundManager slots. Cancel deliberately
        // shares MsgFail's source file, but remains a separate authoring event.
        var sounds = Directory.EnumerateFiles(Path.Combine(assetsRoot, "Sounds"), "*.meta")
            .Select(path => new { Guid = Guid(File.ReadAllText(path)), File = path[..^5] })
            .Where(v => v.Guid.Length > 0).ToDictionary(v => v.Guid, v => v.File, StringComparer.OrdinalIgnoreCase);
        var logo = File.ReadAllText(Path.Combine(sceneRoot, "SceneLogo.unity"));
        var soundList = Regex.Match(logo, @"^  soundFiles:\r?\n((?:  - [^\r\n]*\r?\n)+)", RegexOptions.Multiline).Groups[1].Value;
        var slots = Regex.Matches(soundList, @"guid: ([a-f0-9]{32})");
        if (slots.Count != UiAudioEvents.Effects.Length) throw new InvalidDataException("SoundManager serialized effect slots differ from the supported nine events.");
        var titles = new[] { "点击", "操作成功", "操作失败", "开关切换", "返回", "取消", "截图", "消息提醒", "轻点击" };
        for (var i = 0; i < slots.Count; i++) AddAudio(UiAudioEvents.Effects[i], titles[i], slots[i].Groups[1].Value);
        var result = File.ReadAllText(Path.Combine(sceneRoot, "SceneResult.unity"));
        AddAudio("music_finish", "结算音乐", Guid(Value(result, "musicFinish")));

        void AddAudio(string eventName, string title, string guid)
        {
            if (!sounds.TryGetValue(guid, out var source)) throw new InvalidDataException("Unknown source UI audio: " + eventName);
            var logical = PathRules.NormalizeLogicalPath(Path.GetRelativePath(assetsRoot, source));
            catalog.Audio.Add(new AudioCatalogEntry { Event = eventName, Title = title, LogicalId = logical,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            if (audioRoot == null) return;
            var target = PathRules.GetSafeCombinedPath(audioRoot, logical);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
        }
    }

    private static void AddUsages(string yaml, string scene, Dictionary<string, ImageCatalogEntry> images)
    {
        var docs = Documents.Matches(yaml).Select(m => new Doc(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)).ToList();
        var names = docs.Where(d => d.Kind == "1").ToDictionary(d => d.Id, d => ReadName(Value(d.Text, "m_Name")));
        var transforms = docs.Where(d => d.Kind is "4" or "224").GroupBy(d => FileId(d.Text, "m_GameObject"))
            .Where(g => g.Key.Length > 0).ToDictionary(g => g.Key, g => g.First());
        var byId = docs.ToDictionary(d => d.Id);
        foreach (var doc in docs)
        {
            var kind = doc.Kind == "212" ? "SpriteRenderer" : doc.Kind == "114" && Value(doc.Text, "m_Sprite").Length > 0 ? "Image"
                : doc.Kind == "114" && Value(doc.Text, "m_Texture").Length > 0 ? "RawImage" : "";
            if (kind.Length == 0) continue;
            var guid = Guid(Value(doc.Text, kind == "RawImage" ? "m_Texture" : "m_Sprite"));
            if (!images.TryGetValue(guid, out var image)) continue;
            var go = FileId(doc.Text, "m_GameObject");
            var path = new List<string>(); var seen = new HashSet<string>();
            while (go.Length > 0 && seen.Add(go) && path.Count < 96 && names.TryGetValue(go, out var name))
            {
                path.Add(name);
                if (!transforms.TryGetValue(go, out var transform) || !byId.TryGetValue(FileId(transform.Text, "m_Father"), out var parent)) break;
                go = FileId(parent.Text, "m_GameObject");
            }
            path.Reverse();
            if (path.Count == 0) continue;
            image.Usages.Add(new ImageUsage { Scene = scene, Hierarchy = string.Join("/", path), Component = kind,
                Sliced = kind == "Image" && Value(doc.Text, "m_Type") == "1" });
        }
    }

    private static string ReadName(string value)
    {
        if (value.StartsWith('"')) { try { return JsonSerializer.Deserialize<string>(value) ?? value; } catch (JsonException) { } }
        return value;
    }

    private sealed record Doc(string Kind, string Id, string Text);
}
