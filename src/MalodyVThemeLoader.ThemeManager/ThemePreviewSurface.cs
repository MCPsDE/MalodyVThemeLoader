using System.Drawing.Drawing2D;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class ThemePreviewSurface : Control
{
    public Image? Image { get; set; }
    public string EmptyText { get; set; } = "此主题未提供预览图";
    public ThemePreviewSurface()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer,true);
        AccessibleName = "所选主题预览";
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppVisuals.SurfaceAlt);
        if (Image is null)
        {
            TextRenderer.DrawText(e.Graphics,EmptyText,Font,ClientRectangle,AppVisuals.MutedInk,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            return;
        }
        var ratio = Math.Min((float)Width/Image.Width,(float)Height/Image.Height);
        var width = Image.Width*ratio;
        var height = Image.Height*ratio;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(Image,(Width-width)/2,(Height-height)/2,width,height);
    }
}
