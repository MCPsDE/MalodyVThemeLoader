namespace MalodyVThemeLoader.Windows;

public sealed class LinearProgress : Control
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 40 };
    private int? percent;
    private float phase;
    public int? Percent
    {
        get => percent;
        set { percent = value.HasValue ? Math.Clamp(value.Value, 0, 100) : null; RefreshAnimation(); Invalidate(); }
    }
    public LinearProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 6; TabStop = false; AccessibleRole = AccessibleRole.ProgressBar; AccessibleName = "更新进度";
        animation.Tick += (_, _) => { phase = (phase + .025f) % 1; Invalidate(); };
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ProgressAccessibility(this);
    private sealed class ProgressAccessibility(LinearProgress owner) : ControlAccessibleObject(owner)
    {
        public override string? Value => owner.Percent.HasValue ? owner.Percent + "%" : "正在处理";
    }
    private void RefreshAnimation() => animation.Enabled = Visible && Enabled && !percent.HasValue && !IsDisposed;
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); RefreshAnimation(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); RefreshAnimation(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var track = new SolidBrush(AppVisuals.Line); using var fill = new SolidBrush(AppVisuals.AccentFill);
        e.Graphics.FillRectangle(track, ClientRectangle);
        if (percent.HasValue) e.Graphics.FillRectangle(fill, 0, 0, Width * percent.Value / 100f, Height);
        else { var length = Width * .25f; e.Graphics.FillRectangle(fill, (Width + length) * phase - length, 0, length, Height); }
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}
