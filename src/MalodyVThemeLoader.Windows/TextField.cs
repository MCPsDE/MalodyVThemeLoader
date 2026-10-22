namespace MalodyVThemeLoader.Windows;

/// <summary>Native text entry inside the shared themed field frame.</summary>
public sealed class TextField : UserControl
{
    private readonly TextBox editor;
    public TextField(TextBox editor)
    {
        this.editor = editor; TabStop = false; Dock = DockStyle.Top; Margin = new Padding(0, 0, 0, 8);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = AppVisuals.Surface; editor.BorderStyle = BorderStyle.None; editor.Dock = DockStyle.None;
        editor.BackColor = AppVisuals.Surface; editor.ForeColor = AppVisuals.Ink; Controls.Add(editor);
        editor.GotFocus += (_, _) => Invalidate(); editor.LostFocus += (_, _) => Invalidate();
        RefreshMetrics();
    }
    private int Px(int value) => (int)Math.Ceiling(value * DeviceDpi / 96f);
    private void RefreshMetrics()
    {
        if (editor == null) return;
        var height = Math.Max(Px(34), editor.PreferredHeight + Px(12));
        MinimumSize = new Size(0, height); Height = height; PerformLayout();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (editor == null) return;
        editor.SetBounds(Px(7), Math.Max(Px(1), (Height - editor.PreferredHeight) / 2), Math.Max(1, Width - Px(14)), editor.PreferredHeight);
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RefreshMetrics(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RefreshMetrics(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppVisuals.Surface); using var pen = new Pen(ContainsFocus ? AppVisuals.Accent : AppVisuals.Line);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
