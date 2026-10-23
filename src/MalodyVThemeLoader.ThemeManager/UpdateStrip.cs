using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class UpdateStrip : UserControl
{
    private readonly Label phase = AppVisuals.TextBlock("正在检查更新…", 9F, true);
    private readonly SingleLineLabel detail = AppVisuals.SingleLineText("", 9F);
    private readonly LinearProgress progress = new() { Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };
    private readonly Button action = AppVisuals.CreateButton("取消更新");
    internal event Action? ActionRequested;
    internal UpdateStrip()
    {
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Fill;
        BackColor = AppVisuals.Surface; Padding = new Padding(24, 10, 24, 12); Margin = Padding.Empty;
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        phase.Dock = DockStyle.Top; phase.Margin = Padding.Empty;
        detail.Dock = DockStyle.Top; detail.Margin = new Padding(0, 3, 0, 0); detail.ForeColor = AppVisuals.MutedInk;
        var labels = AppVisuals.Stack(); labels.Margin = Padding.Empty; AppVisuals.AddRow(labels, phase); AppVisuals.AddRow(labels, detail);
        action.Margin = new Padding(16, 0, 0, 0); action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        action.Click += (_, _) => ActionRequested?.Invoke();
        layout.Controls.Add(labels, 0, 0); layout.Controls.Add(action, 1, 0); layout.Controls.Add(progress, 0, 1); layout.SetColumnSpan(progress, 2);
        Controls.Add(layout);
    }
    internal void ShowProgress(UpdateProgress value)
    {
        Visible = true; phase.Text = value.Message; phase.ForeColor = AppVisuals.Ink; detail.Text = value.Detail;
        detail.Visible = value.Detail.Length > 0; progress.Visible = true; progress.Percent = value.Percent;
        action.Text = "取消更新"; action.Enabled = true;
        AccessibleDescription = value.Message + " " + value.Detail;
    }
    internal void ShowResult(UpdateResult value)
    {
        phase.Text = value.Message; phase.ForeColor = value.State == UpdateState.Failed ? AppVisuals.Danger : AppVisuals.Ink;
        detail.Visible = false; progress.Visible = false; action.Text = "重试更新"; action.Enabled = true;
    }
}
