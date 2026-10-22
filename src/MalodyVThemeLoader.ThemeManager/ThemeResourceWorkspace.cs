using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using SkiaSharp;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceWorkspace
{
    public AssetCatalog Catalog { get; }
    public ThemeResources Rules { get; private set; }
    public string Root { get; }
    public ThemeManifest Manifest { get; private set; }
    private readonly string defaultRoot;

    public ThemeResourceWorkspace(string gameRoot, ThemeItem item)
    {
        var catalogPath = Path.Combine(gameRoot, "BepInEx", "plugins", "MalodyVThemeLoader", "catalog.json");
        Catalog = JsonSerializer.Deserialize<AssetCatalog>(File.ReadAllText(catalogPath), JsonDefaults.Options)
            ?? throw new InvalidDataException("资源目录为空，请修复安装。");
        Root = Live2dWorkspace.EnsureEditableDirectory(item);
        Manifest = ThemePackage.InspectDirectory(Root).Manifest;
        Rules = ThemeResources.Read(Root);
        defaultRoot = Path.Combine(gameRoot, "ThemeLoader", "themes", "Default");
    }

    public string? ImageFile(ImageCatalogEntry entry, string scene, string hierarchy, string component, string address = "")
    {
        var rule = Rules.ResolveImage(entry.LogicalId, scene, hierarchy, component, address);
        var root = PathRules.GetSafeCombinedPath(Root, Manifest.ImageRoot);
        var path = PathRules.GetSafeCombinedPath(root, string.IsNullOrEmpty(rule?.File) ? entry.LogicalId : rule.File);
        if (File.Exists(path)) return path;
        var fallback = PathRules.GetSafeCombinedPath(Path.Combine(defaultRoot, "images"), entry.LogicalId);
        return File.Exists(fallback) ? fallback : null;
    }

    public string? AudioFile(AudioCatalogEntry entry, string scene)
    {
        var rule = Rules.ResolveAudio(entry.Event, scene.Length > 0 ? scene : "SceneSong");
        var root = PathRules.GetSafeCombinedPath(Root, Manifest.AudioRoot);
        var path = PathRules.GetSafeCombinedPath(root, rule?.File ?? entry.LogicalId);
        if (File.Exists(path)) return path;
        var fallback = PathRules.GetSafeCombinedPath(Path.Combine(defaultRoot, "audio"), entry.LogicalId);
        return File.Exists(fallback) ? fallback : null;
    }

    public ThemeImageRule? ExactImage(string id, string scene, string hierarchy, string component, string address = "") => Rules.Images.FirstOrDefault(r =>
        r.LogicalId == id && r.Scene == scene && r.Hierarchy == hierarchy && r.Component == component && r.NodeAddress == address);
    public ThemeAudioRule? ExactAudio(string eventName, string scene) => Rules.Audio.FirstOrDefault(r => r.Event == eventName && r.Scene == scene);

    public void SaveImage(ThemeImageRule rule, string? importFile)
    {
        var next = Fresh();
        if (importFile != null)
        {
            rule.DisplayName = Path.GetFileName(importFile);
            using var bitmap = ReadBitmap(importFile);
            ValidateBorder(rule.Border, bitmap.Width, bitmap.Height);
            using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
            rule.File = Store(Manifest.ImageRoot, output.ToArray(), ".png");
        }
        else
        {
            var entry = Catalog.Images.Single(e => e.LogicalId == rule.LogicalId);
            var path = rule.File.Length > 0 ? PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(Root, Manifest.ImageRoot), rule.File)
                : ImageFile(entry, rule.Scene, rule.Hierarchy, rule.Component, rule.NodeAddress);
            if (path == null || !File.Exists(path)) throw new InvalidDataException("请先导入一张图片。");
            using var bitmap = ReadBitmap(path);
            ValidateBorder(rule.Border, bitmap.Width, bitmap.Height);
            // A partial theme can slice an inherited Default image. Persist the
            // source bytes as a rule file, so export/reload is self-contained.
            var basePath = PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(Root, Manifest.ImageRoot), entry.LogicalId);
            if (rule.File.Length == 0 && !File.Exists(basePath))
            {
                using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
                rule.File = Store(Manifest.ImageRoot, output.ToArray(), ".png");
            }
        }
        next.Images.RemoveAll(r => r.LogicalId == rule.LogicalId && r.Scene == rule.Scene && r.Hierarchy == rule.Hierarchy && r.Component == rule.Component && r.NodeAddress == rule.NodeAddress);
        next.Images.Add(rule); next.Write(Root); Rules = next;
    }

    public void SaveAudio(ThemeAudioRule rule, string? importFile)
    {
        var next = Fresh();
        if (importFile != null)
        {
            rule.DisplayName = Path.GetFileName(importFile);
            Live2dAudio.Decode(importFile);
            var extension = Path.GetExtension(importFile).ToLowerInvariant();
            if (extension is not ".ogg" and not ".wav" and not ".mp3") throw new InvalidDataException("请选择 OGG、WAV 或 MP3 音频。");
            rule.File = Store(Manifest.AudioRoot, File.ReadAllBytes(importFile), extension);
        }
        if (rule.File.Length == 0) throw new InvalidDataException("请先导入音频。");
        Live2dAudio.Decode(PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(Root, Manifest.AudioRoot), rule.File));
        next.Audio.RemoveAll(r => r.Event == rule.Event && r.Scene == rule.Scene);
        next.Audio.Add(rule); next.Write(Root); Rules = next;
    }

    public void RemoveImage(string id, string scene, string hierarchy, string component, string address = "")
    {
        var next = Fresh();
        next.Images.RemoveAll(r => r.LogicalId == id && r.Scene == scene && r.Hierarchy == hierarchy && r.Component == component && r.NodeAddress == address);
        next.Write(Root); Rules = next;
    }

    public void RemoveAudio(string eventName, string scene)
    {
        var next = Fresh(); next.Audio.RemoveAll(r => r.Event == eventName && r.Scene == scene);
        next.Write(Root); Rules = next;
    }

    private ThemeResources Fresh() => ThemeResources.Read(Root);
    public void Reload()
    {
        var manifest = ThemePackage.InspectDirectory(Root).Manifest;
        var rules = ThemeResources.Read(Root);
        Manifest = manifest; Rules = rules;
    }

    private string Store(string folder, byte[] bytes, string extension)
    {
        var relative = "overrides/" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + extension;
        var target = PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(Root, folder), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target) && SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(SHA256.HashData(bytes))) return relative;
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return relative;
    }

    public static Bitmap ReadBitmap(string path)
    {
        if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("图片超过 128 MB。");
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream);
        if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 16384 || codec.Info.Height > 16384
            || (long)codec.Info.Width * codec.Info.Height > 64 * 1024 * 1024) throw new InvalidDataException("图片格式或尺寸不支持，请使用 PNG、JPG 或 WebP。");
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap == null) throw new InvalidDataException("图片数据不完整。");
        using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var data = png.AsStream(); using var decoded = Image.FromStream(data);
        return new Bitmap(decoded);
    }

    public static void ValidateBorder(Float4? border, int width, int height)
    {
        if (border == null) return;
        var values = new[] { border.X, border.Y, border.Z, border.W };
        if (values.Any(v => !float.IsFinite(v) || v < 0) || border.X + border.Z > width || border.Y + border.W > height)
            throw new InvalidDataException($"左右边距之和不能超过 {width}，上下边距之和不能超过 {height} 像素。");
    }
}
