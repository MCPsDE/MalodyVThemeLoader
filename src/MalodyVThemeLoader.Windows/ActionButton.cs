using System.Drawing.Drawing2D;

namespace MalodyVThemeLoader.Windows;

internal sealed class ActionButton : Button
{
    private bool hovered;
    private bool pressed;
    public bool Primary { get; init; }
    public bool Danger { get; init; }

    public ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var ancestor = Parent;
        while (ancestor is not null && ancestor.BackColor.A != 255) ancestor = ancestor.Parent;
        e.Graphics.Clear(ancestor?.BackColor ?? AppVisuals.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var rect = new RectangleF(0.5f, 0.5f, Width-1, Height-1);
        using var path = Rounded(rect, 4*scale);
        var fill = !Enabled
            ? AppVisuals.SurfaceAlt
            : pressed
                ? (Primary ? AppVisuals.AccentPressed
                    : Danger ? AppVisuals.DangerPressed
                    : Color.FromArgb(237, 237, 237))
                : hovered
                    ? (Primary ? AppVisuals.AccentHover
                        : Danger ? AppVisuals.DangerHover
                        : AppVisuals.SurfaceAlt)
                    : BackColor;
        var ink = !Enabled
            ? AppVisuals.MutedInk
            : Primary || Danger ? Color.White : ForeColor;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush,path);
        if (!Primary && !Danger && FlatAppearance.BorderSize != 0)
        {
            using var pen = new Pen(AppVisuals.Line,scale);
            e.Graphics.DrawPath(pen,path);
        }
        var textRect = Rectangle.Inflate(ClientRectangle,-(int)(12*scale),-2);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        flags |= TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics,Text,Font,textRect,ink,flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),ink,fill);
    }
    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
        path.AddArc(rect.X,rect.Y,diameter,diameter,180,90);
        path.AddArc(rect.Right-diameter,rect.Y,diameter,diameter,270,90);
        path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(rect.X,rect.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();
        return path;
    }
}
