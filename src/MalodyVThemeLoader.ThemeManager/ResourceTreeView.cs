using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class ResourceTreeView : TreeView
{
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.Style |= 0x8000; return value; } // TVS_NOHSCROLL
    }

    public ResourceTreeView()
    {
        DrawMode = TreeViewDrawMode.OwnerDrawText; Indent = 12;
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node == null) return;
        var selected = (e.State & TreeNodeStates.Selected) != 0;
        var bounds = new Rectangle(e.Bounds.Left, e.Bounds.Top, Math.Max(0, ClientSize.Width - e.Bounds.Left), e.Bounds.Height);
        using var brush = new SolidBrush(selected ? AppVisuals.AccentSoft : AppVisuals.Surface);
        e.Graphics.FillRectangle(brush, bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, bounds, selected ? AppVisuals.Accent : AppVisuals.Ink,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        if (selected && Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -1, -1));
    }
}
