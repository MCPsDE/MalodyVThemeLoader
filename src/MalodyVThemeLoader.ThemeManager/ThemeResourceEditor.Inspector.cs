using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceEditor
{
    private SceneInspectorPane? inspectorPane;
    private Button? resourceMode;
    private Button? inspectorMode;
    private string liveNodeAddress = "";
    public event Action? ResourcePageRequested;
    private void BuildInspectorMode()
    {
        Controls.Remove(body);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resourceMode = AppVisuals.CreateButton("资源文件"); inspectorMode = AppVisuals.CreateButton("在线调试");
        shell.Controls.Add(new ButtonRow(resourceMode, inspectorMode) { Margin = new Padding(0, 0, 0, 12) }, 0, 0);
        var content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        inspectorPane = new SceneInspectorPane(gameRoot, library, report) { Dock = DockStyle.Fill, Visible = false };
        inspectorPane.ResourcePicked += OpenRuntimeResource;
        inspectorPane.Applied += () => Applied?.Invoke();
        content.Controls.Add(body); content.Controls.Add(inspectorPane); shell.Controls.Add(content, 0, 1); Controls.Add(shell);
        resourceMode.Click += (_, _) => { if (ConfirmLeave()) ShowInspector(false); };
        inspectorMode.Click += (_, _) => { if (ConfirmLeave()) ShowInspector(true); };
        ShowInspector(false);
    }
    internal void ShowInspector(bool show)
    {
        body.Visible = !show; if (inspectorPane != null) inspectorPane.Visible = show;
        if (resourceMode != null) { resourceMode.BackColor = show ? AppVisuals.Surface : AppVisuals.AccentSoft; resourceMode.ForeColor = show ? AppVisuals.Ink : AppVisuals.Accent; }
        if (inspectorMode != null) { inspectorMode.BackColor = show ? AppVisuals.AccentSoft : AppVisuals.Surface; inspectorMode.ForeColor = show ? AppVisuals.Accent : AppVisuals.Ink; }
    }
    private void OpenRuntimeResource(InspectorNode picked)
    {
        if (workspace == null) return;
        var entry = workspace.Catalog.Images.SingleOrDefault(e => e.LogicalId == picked.LogicalId);
        if (entry == null) return;
        if (image?.LogicalId == entry.LogicalId && loadedScene == picked.Scene && liveNodeAddress == picked.Address)
        { ShowInspector(false); ResourcePageRequested?.Invoke(); return; }
        if (!ConfirmLeave()) return;
        search.Text = ""; SelectResource(entry);
        selecting = true; SetScene(picked.Scene); hierarchyBox.Text = picked.Hierarchy; componentBox.SelectedItem = picked.Component; liveNodeAddress = picked.Address; selecting = false;
        BuildTree(); LoadSelection(); tips.SetToolTip(hierarchyBox, picked.Hierarchy + Environment.NewLine + picked.Address);
        ShowInspector(false); ResourcePageRequested?.Invoke(); Notify("已定位游戏节点 · 修改仅应用到此节点。");
    }
}
