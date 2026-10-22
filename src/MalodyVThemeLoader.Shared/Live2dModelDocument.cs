using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MalodyVThemeLoader.Shared;

// These paths are relative to the theme's live2dRoot, never just basenames.
public sealed class Live2dBinding
{
    public int FormatVersion { get; set; } = 1;
    public string Target { get; set; } = "displayed-character";
    public string Model { get; set; } = "";
    public string IdleMotion { get; set; } = "";
    public string TapMotion { get; set; } = "";
    public float Scale { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }

    public void Validate()
    {
        if (FormatVersion != 1 || Target != "displayed-character")
            throw new InvalidDataException("不支持的 Live2D 替换配置，请重新应用模型。");
        if (string.IsNullOrWhiteSpace(Model) || !Model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择 .model3.json 模型。");
        if (!float.IsFinite(Scale) || Scale < .1f || Scale > 3
            || !float.IsFinite(OffsetX) || Math.Abs(OffsetX) > 2
            || !float.IsFinite(OffsetY) || Math.Abs(OffsetY) > 2)
            throw new InvalidDataException("模型大小或位置超出范围。");
    }
}

public sealed class Live2dLoadStatus
{
    public string PluginVersion { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Model { get; set; } = "";
    public string State { get; set; } = "waiting";
    public string Message { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Live2dMotionReference
{
    public string Group { get; set; } = "";
    public string File { get; set; } = "";
    public override string ToString() => File.Length == 0 ? Group : $"{Group} · {Path.GetFileName(File)}";
}

public sealed class Live2dModelDocument
{
    public const string BindingFile = "replacement.json";
    public string File { get; private set; } = "";
    public string Name => Path.GetFileName(File).Replace(".model3.json", "", StringComparison.OrdinalIgnoreCase);
    public string Moc { get; private set; } = "";
    public List<string> Textures { get; } = new();
    public List<Live2dMotionReference> Motions { get; } = new();

    public static Live2dModelDocument Read(string live2dRoot, string relativePath)
    {
        var file = Resolve(live2dRoot, relativePath);
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(file));
        var refs = json.RootElement.GetProperty("FileReferences");
        var document = new Live2dModelDocument { File = Relative(live2dRoot, file) };
        var modelDirectory = Path.GetDirectoryName(file)!;
        string Reference(string value) => Relative(live2dRoot, Resolve(modelDirectory, value));
        document.Moc = Reference(refs.GetProperty("Moc").GetString() ?? "");
        using (var moc = System.IO.File.OpenRead(Resolve(live2dRoot, document.Moc)))
        {
            var magic = new byte[4];
            if (moc.Read(magic, 0, 4) != 4 || magic[0] != 'M' || magic[1] != 'O' || magic[2] != 'C' || magic[3] != '3')
                throw new InvalidDataException("模型的 moc3 文件无效。");
        }
        foreach (var texture in refs.GetProperty("Textures").EnumerateArray())
            document.Textures.Add(Reference(texture.GetString() ?? ""));
        if (document.Textures.Count == 0) throw new InvalidDataException("模型没有纹理。");
        if (refs.TryGetProperty("Motions", out var motions))
            foreach (var group in motions.EnumerateObject())
                foreach (var motion in group.Value.EnumerateArray())
                    document.Motions.Add(new Live2dMotionReference
                    {
                        Group = group.Name,
                        File = Reference(motion.GetProperty("File").GetString() ?? "")
                    });
        return document;
    }

    public void ValidateBinding(string root, Live2dBinding binding)
    {
        binding.Validate();
        foreach (var motion in new[] { binding.IdleMotion, binding.TapMotion }.Where(value => value.Length > 0))
            CubismMotion.Read(Resolve(root, motion));
    }

    public static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
            || relative.Replace('\\', '/').Split('/').Any(segment => segment == ".." || segment.Contains(':')))
            throw new InvalidDataException($"模型引用路径无效：{relative}");
        var path = PathRules.GetSafeCombinedPath(root, relative.Replace('\\', '/'));
        if (!System.IO.File.Exists(path)) throw new FileNotFoundException($"模型缺少文件：{relative}", path);
        var current = new FileInfo(path) as FileSystemInfo;
        while (current != null && current.FullName.Length >= Path.GetFullPath(root).TrimEnd('\\', '/').Length)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"模型包含链接：{relative}");
            current = current is FileInfo fi ? fi.Directory : (current as DirectoryInfo)?.Parent;
        }
        return path;
    }

    private static string Relative(string root, string path) => PathRules.NormalizeLogicalPath(Path.GetRelativePath(root, path));
}
