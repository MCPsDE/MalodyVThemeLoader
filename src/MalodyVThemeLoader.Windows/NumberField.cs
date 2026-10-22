using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace MalodyVThemeLoader.Windows;

/// <summary>Theme-painted numeric field retaining native parsing and keyboard behavior.</summary>
public sealed class NumberField : UserControl
{
    private readonly SpinnerlessNumeric editor;
    private readonly Panel inputHost;
    private readonly StepButton minus;
    private readonly StepButton plus;
    private bool arranging;
    private decimal committedValue;
    public event EventHandler? ValueChanged;

    public decimal Value { get => editor.Value; set => editor.Value = value; }
    public decimal Minimum { get => editor.Minimum; set { editor.Minimum = value; RefreshMetrics(); } }
    public decimal Maximum { get => editor.Maximum; set { editor.Maximum = value; RefreshMetrics(); } }
    public decimal Increment { get => editor.Increment; set => editor.Increment = value; }
    public int DecimalPlaces { get => editor.DecimalPlaces; set { editor.DecimalPlaces = value; RefreshMetrics(); } }
    public bool ThousandsSeparator { get => editor.ThousandsSeparator; set { editor.ThousandsSeparator = value; RefreshMetrics(); } }
    public bool ReadOnly { get => editor.ReadOnly; set { editor.ReadOnly = value; UpdateSteps(); } }
    [AllowNull]
    public override string Text { get => editor?.Text ?? base.Text; set { if (editor != null) editor.Text = value; else base.Text = value; } }
    public void UpButton() => editor.UpButton();
    public void DownButton() => editor.DownButton();
    public void CommitEdit() => editor.CommitEdit();
    public new bool Focus() => editor.Focus();

    public NumberField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        TabStop = true; AccessibleRole = AccessibleRole.SpinButton;
        BackColor = AppVisuals.Surface; ForeColor = AppVisuals.Ink;
        inputHost = new Panel { BackColor = AppVisuals.Surface, TabStop = false };
        editor = new SpinnerlessNumeric { BorderStyle = BorderStyle.None, BackColor = AppVisuals.Surface, ForeColor = AppVisuals.Ink, TabIndex = 0 };
        inputHost.Controls.Add(editor);
        minus = new StepButton(this, false); plus = new StepButton(this, true);
        Controls.Add(inputHost); Controls.Add(minus); Controls.Add(plus);
        editor.ValueChanged += (_, e) => { committedValue = editor.Value; UpdateSteps(); ValueChanged?.Invoke(this, e); };
        editor.Enter += (_, _) => { editor.AccessibleName = AccessibleName; Invalidate(); };
        editor.Leave += (_, _) => Invalidate();
        Enter += (_, _) => editor.Focus();
        RefreshMetrics();
    }

    public override Size GetPreferredSize(Size proposedSize) => RequiredSize();
    private int Px(int value) => (int)Math.Ceiling(value * DeviceDpi / 96f);
    private Size RequiredSize()
    {
        if (editor == null) return new Size(90, 32);
        var format = ThousandsSeparator ? "N" + DecimalPlaces : "F" + DecimalPlaces;
        var samples = new[] { Minimum.ToString(format, CultureInfo.CurrentCulture), Maximum.ToString(format, CultureInfo.CurrentCulture) };
        var widest = new string('8', samples.Max(s => s.Length));
        var width = samples.Append(widest).Max(text => TextRenderer.MeasureText(text, Font,
            Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
        return new Size(width + Px(56), Math.Max(Px(32), Font.Height + Px(12)));
    }
    private void RefreshMetrics()
    {
        if (editor == null || arranging) return;
        editor.Font = Font;
        var size = RequiredSize(); MinimumSize = size; Height = size.Height;
        PerformLayout(); Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging || editor == null) return;
        arranging = true;
        try
        {
            var width = Px(22); var inset = Px(1);
            plus.SetBounds(ClientSize.Width - width - inset, inset, width, Math.Max(1, ClientSize.Height - inset * 2));
            minus.SetBounds(plus.Left - width, inset, width, plus.Height);
            inputHost.SetBounds(Px(6), inset, Math.Max(1, minus.Left - Px(10)), Math.Max(1, ClientSize.Height - inset * 2));
            // Native UpDownBase reserves its own spinner width. Keep that region
            // outside the clipped host, in addition to hiding its buttons.
            editor.SetBounds(0, Math.Max(0, (inputHost.Height - editor.PreferredHeight) / 2), inputHost.Width + editor.SpinnerWidth, editor.PreferredHeight);
            UpdateSteps();
        }
        finally { arranging = false; }
        base.OnLayout(e);
    }
    private void UpdateSteps()
    {
        if (minus == null) return;
        minus.Enabled = Enabled && !ReadOnly && committedValue > Minimum;
        plus.Enabled = Enabled && !ReadOnly && committedValue < Maximum;
        Invalidate(true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Enabled ? AppVisuals.Surface : AppVisuals.SurfaceAlt);
        using var line = new Pen(ContainsFocus ? AppVisuals.Accent : AppVisuals.Line);
        e.Graphics.DrawRectangle(line, 0, 0, Width - 1, Height - 1);
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RefreshMetrics(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RefreshMetrics(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); UpdateSteps(); }

    protected override AccessibleObject CreateAccessibilityInstance() => new NumberAccessibleObject(this);
    private sealed class NumberAccessibleObject : ControlAccessibleObject
    {
        private readonly NumberField owner;
        public NumberAccessibleObject(NumberField owner) : base(owner) { this.owner = owner; }
        public override string? Value
        {
            get => owner.Value.ToString(CultureInfo.CurrentCulture);
            set { if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)) owner.Value = Math.Clamp(parsed, owner.Minimum, owner.Maximum); }
        }
    }
    private sealed class SpinnerlessNumeric : NumericUpDown
    {
        internal int SpinnerWidth => Controls.Cast<Control>().FirstOrDefault(c => c.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal))?.Width
            ?? (int)Math.Ceiling(16 * DeviceDpi / 96f);
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            foreach (Control child in Controls)
                if (child.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal)) { child.Visible = false; child.TabStop = false; }
        }
        protected override void OnMouseWheel(MouseEventArgs e) { if (ContainsFocus) base.OnMouseWheel(e); }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Parent?.Parent?.PerformLayout(); }
        internal void CommitEdit() => ValidateEditText();
    }
    private sealed class StepButton : Button
    {
        private readonly NumberField owner;
        private readonly bool increase;
        private bool hovered;
        private bool pressed;
        public StepButton(NumberField owner, bool increase)
        {
            this.owner = owner; this.increase = increase;
            Text = ""; TabStop = false; Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat;
            AccessibleName = increase ? "增加数值" : "减少数值";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnClick(EventArgs e)
        {
            owner.Focus();
            if (owner.Enabled && !owner.ReadOnly) { if (increase) owner.UpButton(); else owner.DownButton(); }
            base.OnClick(e);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(!Enabled ? AppVisuals.SurfaceAlt : pressed ? AppVisuals.ControlPressed : hovered ? AppVisuals.SurfaceAlt : AppVisuals.Surface);
            var scale = DeviceDpi / 96f;
            using var line = new Pen(Enabled ? AppVisuals.Ink : AppVisuals.MutedInk, Math.Max(1, scale));
            var cx = Width / 2f; var cy = Height / 2f; var radius = 4 * scale;
            e.Graphics.DrawLine(line, cx - radius, cy, cx + radius, cy);
            if (increase) e.Graphics.DrawLine(line, cx, cy - radius, cx, cy + radius);
        }
    }
}
