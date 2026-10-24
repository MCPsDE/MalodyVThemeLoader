using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class TextAppearancePane : UserControl
{
    private readonly ChoiceBox component = Choice();
    private readonly ChoiceBox scope = Choice();
    private readonly Label content = AppVisuals.TextBlock("", 10);
    private readonly Label current = AppVisuals.TextBlock("", 9);
    private readonly Label sample = AppVisuals.TextBlock("配色预览 · Aa 文字", 15, true);
    private readonly ColorField normal = new();
    private readonly ColorField selected = new(true);
    private readonly ColorField hover = new(true);
    private readonly CheckBox alphaOverride = Check("覆盖文字不透明度");
    private readonly NumberField opacity = new() { Minimum = 0, Maximum = 100, Value = 100, Dock = DockStyle.Top };
    private readonly ChoiceBox rich = Choice();
    private readonly CheckBox advanced = Check("更多配色设置");
    private readonly TableLayoutPanel details = AppVisuals.Stack();
    private readonly ChoiceBox gradient = Choice();
    private readonly FieldGrid gradientGrid = new();
    private readonly ColorField[] corners = Enumerable.Range(0, 4).Select(_ => new ColorField()).ToArray();
    private readonly ColorField face = new(true);
    private readonly ColorField outline = new(true);
    private readonly Label materialLabel = AppVisuals.TextBlock("TMP 材质", 10, true);
    private readonly TextBox mappings = new() { Multiline = true, Height = 88, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, MaxLength = 2048, PlaceholderText = "每行：原色 = 替换色" };
    private readonly Button addMapping = AppVisuals.CreateButton("添加当前原色");
    private readonly Button save = AppVisuals.CreateButton("保存文字配色", primary: true);
    private readonly Button restore = AppVisuals.CreateButton("恢复原色");
    private readonly Label status = AppVisuals.TextBlock("", 9);
    private readonly ToolTip tips = new();
    private readonly ThemeLibrary library;
    private readonly Action<string> report;
    private ThemeResourceWorkspace? workspace;
    private ThemeItem? theme;
    private InspectorNode? node;
    private ThemeTextRule? draft;
    private string? previousIdentity;
    private bool loading;
    public bool Dirty { get; private set; }
    public bool CanRestore => previousIdentity != null;
    public string Status => status.Text;
    public event Action? Applied;
    public event Action? StateChanged;

    internal TextAppearancePane(ThemeLibrary library, Action<string> report)
    {
        this.library = library; this.report = report; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Margin = Padding.Empty;
        var stack = AppVisuals.Stack(); current.Margin = new Padding(0, 12, 0, 0); AppVisuals.AddRow(stack, current);
        Field(stack, "文字组件", component); Field(stack, "普通颜色", normal);
        sample.AutoSize = false; sample.AutoEllipsis = true; sample.Height = 34; sample.Margin = new Padding(0, 8, 0, 0); AppVisuals.AddRow(stack, sample);
        Field(stack, "选中颜色 · 留空继承普通颜色", selected); Field(stack, "悬停颜色 · 留空继承当前状态", hover); Field(stack, "应用范围", scope);
        alphaOverride.Margin = new Padding(0, 12, 0, 8); AppVisuals.AddRow(stack, alphaOverride); Field(stack, "文字不透明度 · %", opacity);
        rich.Items.AddRange(new object[] { "保留富文本配色", "统一富文本颜色" }); rich.SelectedIndex = 0; Field(stack, "富文本", rich);
        advanced.Margin = new Padding(0, 16, 0, 8); AppVisuals.AddRow(stack, advanced);
        gradient.Items.AddRange(new object[] { "保留原渐变", "使用纯色", "自定义四角渐变" }); gradient.SelectedIndex = 0;
        AppVisuals.AddRow(details, materialLabel); Field(details, "顶点渐变", gradient);
        for (var i = 0; i < 4; i++) { var column = AppVisuals.Stack(); Field(column, new[] { "左上", "右上", "左下", "右下" }[i], corners[i]); gradientGrid.AddField(column); }
        AppVisuals.AddRow(details, gradientGrid); Field(details, "面色 · 留空继承", face); Field(details, "描边颜色 · 留空继承", outline);
        Field(details, "脚本状态原色 → 替换色", mappings); AppVisuals.AddRow(details, addMapping);
        details.Visible = false; AppVisuals.AddRow(stack, details);
        Controls.Add(stack);
        foreach (var field in new[] { normal, selected, hover, face, outline }.Concat(corners)) field.ValueChanged += (_, _) => Changed();
        opacity.ValueChanged += (_, _) => Changed(); alphaOverride.CheckedChanged += (_, _) => { opacity.Enabled = alphaOverride.Checked; Changed(); };
        rich.SelectedIndexChanged += (_, _) => Changed(); gradient.SelectedIndexChanged += (_, _) => { gradientGrid.Visible = gradient.SelectedIndex == 2; Changed(); };
        scope.SelectedIndexChanged += (_, _) => Changed(); mappings.TextChanged += (_, _) => Changed();
        advanced.CheckedChanged += (_, _) => details.Visible = advanced.Checked;
        component.SelectionChangeCommitted += (_, _) => { if (ConfirmLeave()) LoadComponent(); };
        save.Click += (_, _) => Save(); restore.Click += (_, _) => Restore();
        addMapping.Click += (_, _) => { var original = CurrentText?.Color ?? "#FFFFFF"; mappings.AppendText((mappings.Text.Length == 0 ? "" : Environment.NewLine) + original + " = " + normal.Value); };
        UpdateEnabled();
    }
    private InspectorText? CurrentText => node?.Texts.FirstOrDefault(t => t.Component == component.SelectedItem?.ToString());
    private static ChoiceBox Choice() => new() { Dock = DockStyle.Top };
    private static CheckBox Check(string text) => new() { Text = text, AutoSize = true };
    private static void Field(TableLayoutPanel stack, string label, Control value)
    {
        var title = AppVisuals.TextBlock(label, 9); title.Margin = new Padding(0, 10, 0, 4); AppVisuals.AddRow(stack, title);
        value.AccessibleName = label; value.Dock = DockStyle.Top; AppVisuals.AddRow(stack, value);
    }
    public bool ConfirmLeave() => !Dirty || MessageBox.Show(FindForm(), "文字配色尚未保存，是否放弃？", "文字配色", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    public void SelectNode(ThemeResourceWorkspace? workspace, ThemeItem? theme, InspectorNode node)
    {
        this.workspace = workspace; this.theme = theme; this.node = node;
        Visible = node.Texts.Count > 0; loading = true;
        try { component.Items.Clear(); foreach (var text in node.Texts) component.Items.Add(text.Component); if (component.Items.Count > 0) component.SelectedIndex = 0; }
        finally { loading = false; }
        LoadComponent();
    }
    private void LoadComponent()
    {
        loading = true;
        try
        {
            Dirty = false; var capture = CurrentText;
            if (node == null || capture == null) { draft = null; return; }
            var matches = workspace?.Rules.Texts.Where(r => r.Matches(node.Scene, node.Address, capture.Component)).OrderBy(r => r.RepeatDepth < 0 ? 0 : 1).ToArray();
            var found = matches?.FirstOrDefault(); previousIdentity = found?.Identity;
            draft = found == null ? new ThemeTextRule { Scene = node.Scene, Address = node.Address, Hierarchy = node.Hierarchy, Component = capture.Component, Color = capture.Color,
                GradientColors = capture.GradientColors.ToArray() } : JsonSerializer.Deserialize<ThemeTextRule>(JsonSerializer.Serialize(found))!;
            content.Text = capture.Content.Length == 0 ? "（空文字，显示后仍会应用配色）" : capture.Content; tips.SetToolTip(content, capture.Content);
            sample.Text = capture.Content.Length == 0 ? "配色预览 · Aa 文字" : capture.Content; tips.SetToolTip(sample, capture.Content);
            current.Text = $"当前颜色 {capture.Color} · 不透明度 {capture.Opacity:P0}";
            scope.Items.Clear(); scope.Items.Add(new Scope(-1, "仅此节点"));
            var parts = node.Address.Split('/');
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var hash = parts[i].LastIndexOf('#'); var colon = parts[i].LastIndexOf(':');
                if (int.Parse(parts[i][(colon + 1)..]) > 1 || draft.RepeatDepth == i)
                    scope.Items.Add(new Scope(i, "所有同级 “" + Uri.UnescapeDataString(parts[i][..hash]) + "” 实例"));
            }
            scope.SelectedIndex = 0;
            for (var i = 0; i < scope.Items.Count; i++) if (((Scope)scope.Items[i]!).Depth == draft.RepeatDepth) scope.SelectedIndex = i;
            normal.Value = draft.Color; selected.Value = draft.SelectedColor; hover.Value = draft.HoverColor;
            alphaOverride.Checked = draft.Opacity.HasValue; opacity.Value = (decimal)((draft.Opacity ?? capture.Opacity) * 100);
            rich.SelectedIndex = draft.RichText == "uniform" ? 1 : 0; gradient.SelectedIndex = draft.Gradient == "custom" ? 2 : draft.Gradient == "solid" ? 1 : 0;
            for (var i = 0; i < 4; i++) corners[i].Value = draft.GradientColors[i];
            face.Value = draft.FaceColor; outline.Value = draft.OutlineColor;
            mappings.Text = string.Join(Environment.NewLine, draft.Mappings.Select(m => m.Source + " = " + m.Target));
            advanced.Checked = draft.Mappings.Count > 0 || draft.Gradient != "preserve" || draft.FaceColor.Length + draft.OutlineColor.Length > 0;
            status.Text = found == null ? "保存后实时应用；恢复原色可移除此范围的规则。" : "已有配色 · " + (draft.RepeatDepth < 0 ? "精确节点" : "重复实例范围");
        }
        finally { loading = false; UpdateEnabled(); UpdateSample(); }
    }
    private void Changed() { if (loading || draft == null) return; Dirty = true; status.Text = "文字配色尚未保存"; UpdateEnabled(); UpdateSample(); }
    private void UpdateSample()
    {
        try { TextColor.Validate(normal.Value); sample.ForeColor = ColorTranslator.FromHtml(normal.Value); } catch (InvalidDataException) { }
    }
    private void UpdateEnabled()
    {
        var edit = workspace != null && theme != null && CurrentText != null;
        component.Visible = node?.Texts.Count > 1;
        foreach (var label in Controls[0].Controls.OfType<Label>().Where(l => l.Text == "文字组件")) label.Visible = component.Visible;
        foreach (Control field in new Control[] { component, scope, normal, selected, hover, alphaOverride, rich, advanced }) field.Enabled = edit;
        opacity.Enabled = edit && alphaOverride.Checked; save.Enabled = edit && Dirty; restore.Enabled = edit && previousIdentity != null;
        var tmp = CurrentText?.Component == "TMP_Text"; gradient.Visible = materialLabel.Visible = face.Visible = outline.Visible = tmp;
        foreach (var label in details.Controls.OfType<Label>().Where(l => l.Text is "顶点渐变" or "面色 · 留空继承" or "描边颜色 · 留空继承")) label.Visible = tmp;
        gradient.Enabled = face.Enabled = outline.Enabled = tmp; gradientGrid.Visible = tmp && gradient.SelectedIndex == 2;
        StateChanged?.Invoke();
    }
    public bool Save()
    {
        if (!Dirty) return true;
        if (workspace == null || theme == null || node == null || draft == null) return false;
        try
        {
            // Persist a copy only after every field has passed validation.
            var next = JsonSerializer.Deserialize<ThemeTextRule>(JsonSerializer.Serialize(draft))!;
            next.Address = node.Address; next.Hierarchy = node.Hierarchy; next.RepeatDepth = ((Scope)scope.SelectedItem!).Depth;
            next.Color = normal.Value; next.SelectedColor = selected.Value; next.HoverColor = hover.Value;
            next.Opacity = alphaOverride.Checked ? (float)opacity.Value / 100 : null; next.RichText = rich.SelectedIndex == 1 ? "uniform" : "preserve";
            if (next.Component == "TMP_Text") { next.Gradient = gradient.SelectedIndex == 2 ? "custom" : gradient.SelectedIndex == 1 ? "solid" : "preserve"; next.GradientColors = corners.Select(c => c.Value).ToArray(); next.FaceColor = face.Value; next.OutlineColor = outline.Value; }
            next.Mappings.Clear();
            foreach (var line in mappings.Lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var pair = line.Replace("→", "=").Replace("->", "=").Split('=');
                if (pair.Length != 2) throw new InvalidDataException("状态配色每行使用：#原色 = #替换色。");
                next.Mappings.Add(new TextColorMapping { Source = pair[0].Trim().ToUpperInvariant(), Target = pair[1].Trim().ToUpperInvariant() });
            }
            workspace.SaveText(next, previousIdentity); draft = next; previousIdentity = next.Identity;
            library.SetRealtimeMode(true); library.Activate(theme); Dirty = false; UpdateEnabled();
            status.Text = "已保存文字配色 · 等待游戏刷新"; StateChanged?.Invoke(); report(status.Text); Applied?.Invoke(); return true;
        }
        catch (Exception e) { status.Text = "保存失败：" + e.Message; StateChanged?.Invoke(); report(status.Text); return false; }
    }
    public void Clear() { node = null; draft = null; previousIdentity = null; Dirty = false; Visible = false; }
    public void Restore()
    {
        if (workspace == null || theme == null || previousIdentity == null || !ConfirmLeave()) return;
        try { workspace.RemoveText(previousIdentity); library.SetRealtimeMode(true); library.Activate(theme); LoadComponent(); status.Text = "已恢复原有文字配色"; StateChanged?.Invoke(); Applied?.Invoke(); }
        catch (Exception e) { status.Text = "恢复失败：" + e.Message; StateChanged?.Invoke(); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { tips.Dispose(); save.Dispose(); restore.Dispose(); status.Dispose(); content.Dispose(); } base.Dispose(disposing); }
    private sealed record Scope(int Depth, string Label) { public override string ToString() => Label; }
}
