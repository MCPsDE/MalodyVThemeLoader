using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

// One editor for every text/material/gradient color. The RGB controls reuse
// NumberField, including its keyboard, accessibility and theme behavior.
internal sealed class ColorField : UserControl
{
    private readonly TextBox hex = new() { Dock = DockStyle.Fill, MaxLength = 7 };
    private readonly Button pick = AppVisuals.CreateButton("选色…");
    private readonly CheckBox rgbToggle = new() { Text = "RGB", AutoSize = true, Margin = new Padding(8, 4, 0, 0) };
    private readonly FieldGrid rgb = new() { Visible = false };
    private readonly NumberField[] channels = Enumerable.Range(0, 3).Select(_ => new NumberField { Minimum = 0, Maximum = 255, Dock = DockStyle.Top }).ToArray();
    private bool updating;
    public string Value { get => hex.Text.Trim().ToUpperInvariant(); set { hex.Text = value; UpdateRgb(); } }
    public event EventHandler? ValueChanged;
    internal ColorField(bool optional = false)
    {
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Margin = Padding.Empty;
        var stack = AppVisuals.Stack(); var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        hex.PlaceholderText = optional ? "继承原色" : "#RRGGBB"; hex.BackColor = AppVisuals.Surface; hex.ForeColor = AppVisuals.Ink;
        hex.AccessibleName = "HEX 颜色"; hex.MinimumSize = new Size(100, 0); pick.Margin = new Padding(8, 0, 0, 0);
        row.Controls.Add(hex, 0, 0); row.Controls.Add(pick, 1, 0); row.Controls.Add(rgbToggle, 2, 0);
        for (var i = 0; i < 3; i++)
        {
            var column = AppVisuals.Stack(); var name = new[] { "R · 红", "G · 绿", "B · 蓝" }[i];
            AppVisuals.AddRow(column, AppVisuals.TextBlock(name, 9)); channels[i].AccessibleName = name; AppVisuals.AddRow(column, channels[i]); rgb.AddField(column);
            channels[i].ValueChanged += (_, _) =>
            {
                if (updating) return;
                updating = true;
                try { hex.Text = $"#{(int)channels[0].Value:X2}{(int)channels[1].Value:X2}{(int)channels[2].Value:X2}"; }
                finally { updating = false; }
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
        }
        rgb.Margin = new Padding(0, 8, 0, 0); AppVisuals.AddRow(stack, row); AppVisuals.AddRow(stack, rgb); Controls.Add(stack);
        hex.TextChanged += (_, _) => { if (!updating) { UpdateRgb(); ValueChanged?.Invoke(this, EventArgs.Empty); } };
        rgbToggle.CheckedChanged += (_, _) => { rgb.Visible = rgbToggle.Checked; UpdateRgb(); };
        pick.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { FullOpen = true };
            try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(Value); } catch { dialog.Color = System.Drawing.Color.White; }
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) Value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        };
    }
    private void UpdateRgb()
    {
        if (updating) return; updating = true;
        try { var color = TextColor.Rgb(Value); channels[0].Value = (color >> 16) & 255; channels[1].Value = (color >> 8) & 255; channels[2].Value = color & 255; }
        catch (InvalidDataException) { }
        finally { updating = false; }
    }
}
