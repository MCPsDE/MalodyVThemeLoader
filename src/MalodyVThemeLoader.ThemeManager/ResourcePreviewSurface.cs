using System.Drawing.Drawing2D;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class ResourcePreviewSurface : Control
{
    private Bitmap? image;
    private Live2dAudio? audio;
    private float[]? waveMin;
    private float[]? waveMax;
    private Float4 border = new();
    private RectangleF sourceBounds;
    private int dragging = -1;
    public bool Sliced { get; set; }
    public Size TargetSize { get; set; } = new(320, 120);
    public event Action<Float4>? BorderChanged;
    public Size ImageSize => image?.Size ?? Size.Empty;
    public string EmptyText { get; set; } = "从左侧选择图片或音频";

    public ResourcePreviewSurface()
    {
        DoubleBuffered = true; ResizeRedraw = true; TabStop = true;
        AccessibleName = "资源预览与九宫格切片";
    }

    public void SetImage(Bitmap? bitmap)
    {
        image?.Dispose(); image = bitmap; audio = null; Invalidate();
    }

    public void SetAudio(Live2dAudio? value)
    {
        image?.Dispose(); image = null; audio = value;
        waveMin = value == null ? null : new float[1024]; waveMax = value == null ? null : new float[1024];
        if (value != null)
        {
            for (var i = 0; i < value.Samples.Length; i++)
            {
                var bin = (int)((long)i * 1024 / value.Samples.Length);
                waveMin![bin] = Math.Min(waveMin[bin], value.Samples[i]); waveMax![bin] = Math.Max(waveMax[bin], value.Samples[i]);
            }
        }
        Invalidate();
    }

    public void SetBorder(Float4 value) { border = new(value.X, value.Y, value.Z, value.W); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.Clear(AppVisuals.SurfaceAlt); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (image == null)
        {
            if (audio != null) PaintAudio(g, audio);
            else TextRenderer.DrawText(g, EmptyText, Font, Rectangle.Inflate(ClientRectangle, -20, -20), AppVisuals.MutedInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        var upper = new RectangleF(16, 34, Math.Max(1, Width - 32), Math.Max(20, Height * .58f - 42));
        sourceBounds = Fit(image.Size, upper);
        TextRenderer.DrawText(g, $"原图 · {image.Width} × {image.Height}", Font, new Point(16, 12), AppVisuals.MutedInk);
        Checker(g, sourceBounds); g.DrawImage(image, sourceBounds);
        var lower = new RectangleF(16, Height * .6f + 32, Math.Max(1, Width - 32), Math.Max(10, Height * .4f - 48));
        TextRenderer.DrawText(g, Sliced ? "九宫格拉伸预览" : "拉伸预览", Font, new Point(16, (int)(Height * .6f + 8)), AppVisuals.MutedInk);
        var target = Fit(TargetSize, lower);
        Checker(g, target);
        if (Sliced)
        {
            PaintSlices(g, target);
            using var guide = new Pen(AppVisuals.Accent, 1) { DashStyle = DashStyle.Dash };
            var positions = Guides();
            g.DrawLine(guide, positions[0], sourceBounds.Top, positions[0], sourceBounds.Bottom);
            g.DrawLine(guide, positions[1], sourceBounds.Top, positions[1], sourceBounds.Bottom);
            g.DrawLine(guide, sourceBounds.Left, positions[2], sourceBounds.Right, positions[2]);
            g.DrawLine(guide, sourceBounds.Left, positions[3], sourceBounds.Right, positions[3]);
        }
        else g.DrawImage(image, target);
        if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -3, -3));
    }

    private void PaintSlices(Graphics g, RectangleF target)
    {
        if (image == null) return;
        var left = Math.Clamp(border.X, 0, image.Width); var right = Math.Clamp(border.Z, 0, image.Width - left);
        var bottom = Math.Clamp(border.Y, 0, image.Height); var top = Math.Clamp(border.W, 0, image.Height - bottom);
        var sx = new[] { 0f, left, image.Width - right, image.Width };
        var sy = new[] { 0f, top, image.Height - bottom, image.Height };
        var factor = target.Width / Math.Max(1, TargetSize.Width);
        var horizontal = Math.Min(1, TargetSize.Width / Math.Max(1, left + right));
        var vertical = Math.Min(1, TargetSize.Height / Math.Max(1, top + bottom));
        var dx = new[] { target.Left, target.Left + left * factor * horizontal, target.Right - right * factor * horizontal, target.Right };
        var dy = new[] { target.Top, target.Top + top * factor * vertical, target.Bottom - bottom * factor * vertical, target.Bottom };
        for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
        {
            var source = RectangleF.FromLTRB(sx[x], sy[y], sx[x + 1], sy[y + 1]);
            var dest = RectangleF.FromLTRB(dx[x], dy[y], dx[x + 1], dy[y + 1]);
            if (source.Width > 0 && source.Height > 0 && dest.Width > 0 && dest.Height > 0) g.DrawImage(image, dest, source, GraphicsUnit.Pixel);
        }
    }

    private void PaintAudio(Graphics g, Live2dAudio value)
    {
        var seconds = value.Samples.Length / (double)value.Channels / value.SampleRate;
        TextRenderer.DrawText(g, $"{seconds:0.0} 秒 · {value.SampleRate / 1000.0:0.#} kHz · {(value.Channels == 1 ? "单声道" : "立体声")}", Font, new Point(16, 16), AppVisuals.MutedInk);
        var bounds = new Rectangle(20, Height / 4, Math.Max(1, Width - 40), Math.Max(20, Height / 2));
        using var pen = new Pen(AppVisuals.Accent, 1);
        using var line = new Pen(AppVisuals.Line, 1);
        g.DrawLine(line, bounds.Left, bounds.Top + bounds.Height / 2, bounds.Right, bounds.Top + bounds.Height / 2);
        for (var x = 0; x < bounds.Width; x++)
        {
            var start = x * 1024 / bounds.Width;
            var end = Math.Min(1024, Math.Max(start + 1, (x + 1) * 1024 / bounds.Width));
            float min = 0, max = 0;
            for (var i = start; i < end; i++) { min = Math.Min(min, waveMin![i]); max = Math.Max(max, waveMax![i]); }
            var center = bounds.Top + bounds.Height / 2f;
            g.DrawLine(pen, bounds.Left + x, center - max * bounds.Height / 2, bounds.Left + x, center - min * bounds.Height / 2);
        }
    }

    private float[] Guides() => image == null ? new float[4] : new[] {
        sourceBounds.Left + border.X / image.Width * sourceBounds.Width,
        sourceBounds.Right - border.Z / image.Width * sourceBounds.Width,
        sourceBounds.Top + border.W / image.Height * sourceBounds.Height,
        sourceBounds.Bottom - border.Y / image.Height * sourceBounds.Height };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (!Sliced || image == null || e.Button != MouseButtons.Left || !sourceBounds.Contains(e.Location)) return;
        var guides = Guides();
        var distances = new[] { Math.Abs(e.X - guides[0]), Math.Abs(e.X - guides[1]), Math.Abs(e.Y - guides[2]), Math.Abs(e.Y - guides[3]) };
        dragging = Array.IndexOf(distances, distances.Min());
        if (distances[dragging] > 9 * DeviceDpi / 96f) { dragging = -1; return; }
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (image == null || !Sliced) return;
        if (dragging < 0)
        {
            var guides = Guides();
            Cursor = sourceBounds.Contains(e.Location) && (Math.Abs(e.X - guides[0]) < 8 || Math.Abs(e.X - guides[1]) < 8) ? Cursors.VSplit
                : sourceBounds.Contains(e.Location) && (Math.Abs(e.Y - guides[2]) < 8 || Math.Abs(e.Y - guides[3]) < 8) ? Cursors.HSplit : Cursors.Default;
            return;
        }
        var x = Math.Clamp((e.X - sourceBounds.Left) / sourceBounds.Width * image.Width, 0, image.Width);
        var y = Math.Clamp((e.Y - sourceBounds.Top) / sourceBounds.Height * image.Height, 0, image.Height);
        if (dragging == 0) border.X = MathF.Round(Math.Min(x, image.Width - border.Z));
        if (dragging == 1) border.Z = MathF.Round(Math.Min(image.Width - x, image.Width - border.X));
        if (dragging == 2) border.W = MathF.Round(Math.Min(y, image.Height - border.Y));
        if (dragging == 3) border.Y = MathF.Round(Math.Min(image.Height - y, image.Height - border.W));
        BorderChanged?.Invoke(new Float4(border.X, border.Y, border.Z, border.W)); Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = -1; Capture = false; }
    private static RectangleF Fit(Size size, RectangleF bounds)
    {
        var scale = Math.Min(bounds.Width / Math.Max(1, size.Width), bounds.Height / Math.Max(1, size.Height));
        var width = size.Width * scale; var height = size.Height * scale;
        return new RectangleF(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
    }
    private static void Checker(Graphics g, RectangleF bounds)
    {
        using var tile = new Bitmap(16, 16); using (var paint = Graphics.FromImage(tile))
        { paint.Clear(Color.FromArgb(102, 102, 102)); using var brush = new SolidBrush(Color.FromArgb(128, 128, 128)); paint.FillRectangle(brush, 0, 0, 8, 8); paint.FillRectangle(brush, 8, 8, 8, 8); }
        using var pattern = new TextureBrush(tile); g.FillRectangle(pattern, bounds);
    }
    protected override void Dispose(bool disposing) { if (disposing) image?.Dispose(); base.Dispose(disposing); }
}
