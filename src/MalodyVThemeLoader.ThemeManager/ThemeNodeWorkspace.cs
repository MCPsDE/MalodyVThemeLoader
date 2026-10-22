using System.Drawing.Imaging;
using System.Security.Cryptography;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceWorkspace
{
    public ThemeNodeRule? NodeRule(string scene, string address) => Rules.Nodes.SingleOrDefault(r => r.Scene == scene && r.Address == address);
    public string NodeFile(string file) => PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(Root, Manifest.ImageRoot), file);
    public void SaveNode(ThemeNodeRule rule, string? imageImport, string? videoImport)
    {
        var next = Fresh();
        if (imageImport != null)
        {
            using var bitmap = ReadBitmap(imageImport); ValidateBorder(rule.Border, bitmap.Width, bitmap.Height);
            using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
            rule.ImageFile = Store(Manifest.ImageRoot, output.ToArray(), ".png"); rule.ImageName = Path.GetFileName(imageImport);
        }
        if (videoImport != null)
        {
            WebmVideo.Inspect(videoImport);
            using var source = File.OpenRead(videoImport); using var sha = SHA256.Create();
            var digest = Convert.ToHexString(sha.ComputeHash(source)).ToLowerInvariant();
            rule.VideoFile = "overrides/" + digest + ".webm"; rule.VideoName = Path.GetFileName(videoImport);
            var target = NodeFile(rule.VideoFile); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var same = false;
            if (File.Exists(target)) { using var existing = File.OpenRead(target); same = Convert.ToHexString(sha.ComputeHash(existing)).Equals(digest, StringComparison.OrdinalIgnoreCase); }
            if (!same)
            {
                var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
                try { source.Position = 0; using (var output = File.Create(temporary)) source.CopyTo(output); WebmVideo.Inspect(temporary); File.Move(temporary, target, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        if (rule.ImageFile.Length > 0)
        { using var bitmap = ReadBitmap(NodeFile(rule.ImageFile)); ValidateBorder(rule.Border, bitmap.Width, bitmap.Height); }
        if (rule.VideoFile.Length > 0) WebmVideo.Inspect(NodeFile(rule.VideoFile));
        next.Nodes.RemoveAll(r => r.Scene == rule.Scene && r.Address == rule.Address);
        next.Nodes.Add(rule); next.Write(Root); Rules = next;
    }
    public void RemoveNode(string scene, string address)
    {
        var next = Fresh(); next.Nodes.RemoveAll(r => r.Scene == scene && r.Address == address); next.Write(Root); Rules = next;
    }
}
