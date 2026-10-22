using System.Diagnostics;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class Live2dPreviewSurface : Control
{
    private readonly NativeCubismPreview model;
    private readonly string live2dRoot;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 66 };
    private readonly Stopwatch clock = new();
    private Bitmap? frame;
    private CubismMotion? motion;
    private string motionFile = "";
    private RectangleF modelRectangle;
    private PointF? dragStart;
    private PointF dragEnd;
    private bool loop;
    private System.Media.SoundPlayer? sound;
    private MemoryStream? audioStream;
    public bool DrawRegion { get; set; }
    public Live2dBinding Binding { get; }
    public int SelectedRegion { get; set; } = -1;
    public event Action<Live2dClickRegion>? RegionCreated;
    public event Action<int>? RegionSelected;
    public event Action<string>? PreviewError;

    public Live2dPreviewSurface(string gameRoot, string root, Live2dBinding binding)
    {
        live2dRoot = root; Binding = binding;
        model = new NativeCubismPreview(gameRoot, root, binding.Model);
        DoubleBuffered = true; TabStop = true; BackColor = AppVisuals.SurfaceAlt;
        AccessibleName = "模型预览，鼠标拖动框选点击区域";
        timer.Tick += (_, _) =>
        {
            if (!Visible || Width < 40 || Height < 40) return;
            timer.Stop();
            try { RenderFrame(); }
            catch (Exception e) { PreviewError?.Invoke(e.Message); return; }
            if (!IsDisposed) timer.Start();
        };
        timer.Start();
    }

    public void Play(string file, bool repeat = false)
    {
        StopAudio();
        motion = string.IsNullOrEmpty(file) ? null : CubismMotion.Read(Live2dModelDocument.Resolve(live2dRoot, file));
        if (Binding.MotionAudio.TryGetValue(file, out var audio) && audio.Length > 0)
        {
            audioStream = new MemoryStream(Live2dAudio.Decode(Live2dModelDocument.Resolve(live2dRoot, audio)).ToWave());
            sound = new System.Media.SoundPlayer(audioStream); sound.Load();
        }
        motionFile = file; loop = repeat; clock.Restart(); sound?.Play(); timer.Start(); RenderFrame();
    }

    private void StopAudio() { sound?.Stop(); sound?.Dispose(); sound = null; audioStream?.Dispose(); audioStream = null; }

    public void BeginRegion()
    {
        Play(""); DrawRegion = true; Cursor = Cursors.Cross;
    }

    public void CancelRegion() { DrawRegion = false; dragStart = null; Cursor = Cursors.Default; Capture = false; Invalidate(); }

    public void RenderFrame()
    {
        if (Width < 40 || Height < 40) return;
        var time = (float)clock.Elapsed.TotalSeconds;
        if (motion != null && time > motion.Duration)
        {
            if (loop) time %= motion.Duration;
            else if (!string.IsNullOrEmpty(Binding.IdleMotion) && motionFile != Binding.IdleMotion)
            { Play(Binding.IdleMotion, true); return; }
            else { StopAudio(); time = motion.Duration; }
        }
        model.Update(motion, time);
        var factor = Math.Min(1f, (model.IsGpu ? 960f : 480f) / Math.Max(Width, Height));
        var renderWidth = Math.Max(40, (int)(Width * factor)); var renderHeight = Math.Max(40, (int)(Height * factor));
        var next = model.Render(renderWidth, renderHeight, AppVisuals.SurfaceAlt, out var renderedRectangle);
        modelRectangle = new RectangleF(renderedRectangle.X * Width / renderWidth, renderedRectangle.Y * Height / renderHeight,
            renderedRectangle.Width * Width / renderWidth, renderedRectangle.Height * Height / renderHeight);
        var old = frame; frame = next; old?.Dispose(); Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppVisuals.SurfaceAlt);
        if (frame != null) e.Graphics.DrawImage(frame, ClientRectangle);
        for (var i = 0; i < Binding.ClickRegions.Count; i++)
        {
            var region = Binding.ClickRegions[i];
            var rectangle = PixelRectangle(region.X, region.Y, region.Width, region.Height);
            using var fill = new SolidBrush(Color.FromArgb(i == SelectedRegion ? 42 : 15, AppVisuals.Accent));
            using var pen = new Pen(i == SelectedRegion ? AppVisuals.Accent : AppVisuals.MutedInk, i == SelectedRegion ? 2 : 1);
            e.Graphics.FillRectangle(fill, rectangle); e.Graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            TextRenderer.DrawText(e.Graphics, region.Name, Font, new Point((int)rectangle.X + 4, (int)rectangle.Y + 3), AppVisuals.Ink, AppVisuals.Surface, TextFormatFlags.NoPrefix);
        }
        if (dragStart is { } start)
        {
            var rectangle = PixelRectangle(Math.Min(start.X, dragEnd.X), Math.Min(start.Y, dragEnd.Y), Math.Abs(start.X - dragEnd.X), Math.Abs(start.Y - dragEnd.Y));
            using var pen = new Pen(AppVisuals.Accent, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2), AppVisuals.Accent, AppVisuals.SurfaceAlt);
    }

    private RectangleF PixelRectangle(float x, float y, float w, float h) => new(modelRectangle.X + x * modelRectangle.Width,
        modelRectangle.Y + y * modelRectangle.Height, w * modelRectangle.Width, h * modelRectangle.Height);
    private PointF Normalized(Point point) => new(Math.Clamp((point.X - modelRectangle.X) / modelRectangle.Width, 0, 1),
        Math.Clamp((point.Y - modelRectangle.Y) / modelRectangle.Height, 0, 1));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (e.Button != MouseButtons.Left || !modelRectangle.Contains(e.Location)) return;
        var point = Normalized(e.Location);
        if (DrawRegion) { dragStart = point; dragEnd = point; Capture = true; }
        else
        {
            var index = Binding.ClickRegions.FindLastIndex(region => region.Contains(point.X, point.Y));
            if (index >= 0) RegionSelected?.Invoke(index);
            try { Play(Live2dEvents.ClickMotion(Binding, point.X, point.Y)); }
            catch (Exception ex) { PreviewError?.Invoke(ex.Message); }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart == null) return; dragEnd = Normalized(e.Location); Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragStart is not { } start) return;
        var end = Normalized(e.Location); CancelRegion();
        var region = new Live2dClickRegion { X = Math.Min(start.X, end.X), Y = Math.Min(start.Y, end.Y), Width = Math.Abs(start.X - end.X), Height = Math.Abs(start.Y - end.Y) };
        if (region.Width < .015f || region.Height < .015f) return;
        RegionCreated?.Invoke(region);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) { CancelRegion(); e.Handled = true; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { StopAudio(); timer.Dispose(); frame?.Dispose(); model.Dispose(); }
        base.Dispose(disposing);
    }
}
