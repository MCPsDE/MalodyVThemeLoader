using System.IO.Compression;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal static class ThemePreview
{
    public static Image? Load(ThemeItem? item)
    {
        if (item?.Info == null || item.IsVirtual || !item.IsValid) return null;
        var candidates = new[] { "preview.png", "preview.jpg", "cover.png", "cover.jpg",
            item.Info.Manifest.ImageRoot.TrimEnd('/') + "/Textures/Malody/mainlist_bg2.png" };
        try
        {
            if (item.IsLegacyDirectory)
            {
                foreach (var candidate in candidates)
                {
                    var path = PathRules.GetSafeCombinedPath(item.SourcePath,candidate);
                    if (!File.Exists(path) || new FileInfo(path).Length > 16*1024*1024) continue;
                    using var stream = File.OpenRead(path);
                    return Decode(stream);
                }
            }
            else
            {
                using var archive = ZipFile.OpenRead(item.SourcePath);
                foreach (var candidate in candidates)
                {
                    var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\','/'),candidate,StringComparison.OrdinalIgnoreCase));
                    if (entry == null || entry.Length > 16*1024*1024) continue;
                    using var stream = entry.Open();
                    return Decode(stream);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException) { }
        return null;
    }

    private static Image? Decode(Stream stream)
    {
        using var image = Image.FromStream(stream);
        if ((long)image.Width*image.Height > 32_000_000) return null;
        // Downsample to keep scrolling and theme selection inexpensive; no file or ZIP remains locked.
        var ratio = Math.Min(1,1200d/Math.Max(image.Width,image.Height));
        return new Bitmap(image,new Size(Math.Max(1,(int)(image.Width*ratio)),Math.Max(1,(int)(image.Height*ratio))));
    }
}
