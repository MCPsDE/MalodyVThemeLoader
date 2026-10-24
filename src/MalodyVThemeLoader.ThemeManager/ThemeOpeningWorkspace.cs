using System.Drawing.Imaging;
using System.Security.Cryptography;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceWorkspace
{
    public void SaveOpening(ThemeOpening? opening, string? importFile)
    {
        var next = Fresh();
        if (opening != null)
        {
            if (importFile != null)
            {
                opening.DisplayName = Path.GetFileName(importFile);
                if (Path.GetExtension(importFile).Equals(".webm", StringComparison.OrdinalIgnoreCase))
                {
                    opening.Kind = "video"; opening.File = StoreOpeningVideo(importFile);
                }
                else
                {
                    using var bitmap = ReadBitmap(importFile);
                    if (bitmap.Width > 8192 || bitmap.Height > 8192 || (long)bitmap.Width * bitmap.Height > 16 * 1024 * 1024)
                        throw new InvalidDataException("开屏图片最多为 8192 像素、1600 万像素。请缩小图片后重试。");
                    using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
                    opening.Kind = "image"; opening.File = Store(Manifest.ImageRoot, output.ToArray(), ".png");
                }
            }
            opening.Validate(); var path = NodeFile(opening.File);
            if (opening.Kind == "video") WebmVideo.Inspect(path); else { using var bitmap = ReadBitmap(path); }
        }
        next.Opening = opening; next.Write(Root); Rules = next;
    }
    private string StoreOpeningVideo(string file)
    {
        WebmVideo.Inspect(file);
        using var source = File.OpenRead(file); using var sha = SHA256.Create();
        var digest = Convert.ToHexString(sha.ComputeHash(source)).ToLowerInvariant();
        var relative = "overrides/" + digest + ".webm"; var target = NodeFile(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
            using var existing = File.OpenRead(target);
            if (Convert.ToHexString(sha.ComputeHash(existing)).Equals(digest, StringComparison.OrdinalIgnoreCase)) return relative;
        }
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            source.Position = 0; using (var output = File.Create(temporary)) source.CopyTo(output);
            WebmVideo.Inspect(temporary); File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return relative;
    }
}
