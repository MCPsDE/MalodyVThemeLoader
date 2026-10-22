using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class SceneInspectorPane : UserControl
{
    private readonly string loaderRoot;
    private readonly ThemeLibrary library;
    private readonly Action<string> report;
    private readonly InspectorControl control = new() { Client = Guid.NewGuid().ToString("N") };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private readonly CheckBox connect = Check("连接游戏");
    private readonly CheckBox frames = Check("层级边框", true);
    private readonly CheckBox names = Check("节点名称", true);
    private readonly CheckBox inactive = Check("隐藏节点");
    private readonly Label connection = AppVisuals.TextBlock("开启连接后，在游戏中按鼠标中键选取。", 9F);
    private readonly TextBox search = new() { PlaceholderText = "搜索节点名称或路径", Dock = DockStyle.Top };
    private readonly TreeView tree = new ResourceTreeView { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.None, ShowNodeToolTips = true, FullRowSelect = true };
    private readonly Label title = AppVisuals.TextBlock("选择一个游戏节点", 12F, true);
    private readonly Label location = AppVisuals.TextBlock("在左侧展开层级，或在游戏中按中键。", 9F);
    private readonly Label imageName = AppVisuals.TextBlock("尚未添加底图", 9F);
    private readonly Label videoName = AppVisuals.TextBlock("尚未添加视频", 9F);
    private readonly Label state = AppVisuals.TextBlock("同一位置连续按中键可切换重叠层级。", 9F);
    private readonly Button locate = AppVisuals.CreateButton("定位图片资源");
    private readonly Button addImage = AppVisuals.CreateButton("添加底图…");
    private readonly Button removeImage = AppVisuals.CreateButton("移除底图");
    private readonly Button addVideo = AppVisuals.CreateButton("添加 WebM…");
    private readonly Button removeVideo = AppVisuals.CreateButton("移除视频");
    private readonly Button save = AppVisuals.CreateButton("保存并实时应用", primary: true);
    private readonly Button restore = AppVisuals.CreateButton("恢复此节点");
    private readonly ResourcePreviewSurface preview = new() { Dock = DockStyle.Top, Height = 120, EmptyText = "添加底图后预览" };
    private readonly NumberField opacity = Number(0, 100, 100);
    private readonly NumberField width = Number(0, 8192, 0);
    private readonly NumberField height = Number(0, 8192, 0);
    private readonly ChoiceBox fit = new() { Dock = DockStyle.Top };
    private readonly CheckBox slice = Check("九宫格切片");
    private readonly NumberField[] border = Enumerable.Range(0, 4).Select(_ => Number(0, 16384, 0)).ToArray();
    private readonly ToolTip tips = new();
    private ThemeResourceWorkspace? workspace;
    private ThemeItem? theme;
    private InspectorSnapshot? snapshot;
    private InspectorNode? node;
    private ThemeNodeRule draft = new();
    private string? pendingImage;
    private string? pendingVideo;
    private string treeSignature = "";
    private long lastRevision = -1;
    private string lastRuntime = "";
    private bool selecting;
    private bool dirty;
    public bool IsBusy { get; private set; }
    public event Action<InspectorNode>? ResourcePicked;
    public event Action? Applied;

    public SceneInspectorPane(string gameRoot, ThemeLibrary library, Action<string> report)
    {
        loaderRoot = Path.Combine(gameRoot, "ThemeLoader"); this.library = library; this.report = report;
        Font = AppVisuals.UiFont(); BackColor = AppVisuals.Canvas; ForeColor = AppVisuals.Ink;
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty };
        options.Controls.AddRange(new Control[] { connect, frames, names, inactive });
        connection.Margin = new Padding(0, 8, 0, 14); connection.Dock = DockStyle.Top;
        shell.Controls.Add(options, 0, 0); shell.Controls.Add(connection, 0, 1);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10), BackColor = AppVisuals.Surface, Margin = new Padding(0, 0, 16, 0) };
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); browser.RowStyles.Add(new RowStyle(SizeType.AutoSize)); browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        browser.Controls.Add(AppVisuals.TextBlock("搜索节点名称或路径", 9F, true), 0, 0); browser.Controls.Add(new TextField(search), 0, 1); browser.Controls.Add(tree, 0, 2);
        tree.BackColor = search.BackColor = AppVisuals.Surface; tree.ForeColor = search.ForeColor = AppVisuals.Ink; tree.ItemHeight = 27;
        tree.AccessibleName = "实时场景层级"; search.AccessibleName = "搜索游戏节点";
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); editor.RowStyles.Add(new RowStyle(SizeType.AutoSize)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var fields = AppVisuals.Stack(); fields.Margin = Padding.Empty;
        title.AutoSize = false; title.AutoEllipsis = true; title.Height = 28; title.Dock = DockStyle.Top;
        location.AutoSize = false; location.AutoEllipsis = true; location.Height = 48; location.Dock = DockStyle.Top;
        AppVisuals.AddRow(fields, title); AppVisuals.AddRow(fields, location); AppVisuals.AddRow(fields, locate);
        Section(fields, "节点底图");
        AppVisuals.AddRow(fields, new ButtonRow(addImage, removeImage)); DataLabel(fields, imageName);
        AppVisuals.AddRow(fields, preview);
        Section(fields, "视频叠加"); AppVisuals.AddRow(fields, new ButtonRow(addVideo, removeVideo)); DataLabel(fields, videoName);
        Field(fields, "视频不透明度 · %", opacity);
        fit.Items.AddRange(new object[] { "拉伸填满", "完整显示", "裁切填满" }); fit.SelectedIndex = 0; Field(fields, "图片与视频填充", fit);
        var sizes = new FieldGrid(); var w = AppVisuals.Stack(); var h = AppVisuals.Stack(); Field(w, "宽度 · 0 为自动", width); Field(h, "高度 · 0 为自动", height); sizes.AddField(w); sizes.AddField(h); AppVisuals.AddRow(fields, sizes);
        AppVisuals.AddRow(fields, slice); var borders = new FieldGrid();
        for (var i = 0; i < 4; i++) { var column = AppVisuals.Stack(); Field(column, new[] { "左 · px", "下 · px", "右 · px", "上 · px" }[i], border[i]); borders.AddField(column); }
        AppVisuals.AddRow(fields, borders); scroll.Controls.Add(fields);
        editor.Controls.Add(scroll, 0, 0); editor.Controls.Add(new ButtonRow(save, restore) { Margin = new Padding(0, 10, 0, 0) }, 0, 1);
        state.AutoSize = false; state.Dock = DockStyle.Fill; state.Margin = new Padding(0, 8, 0, 0); editor.Controls.Add(state, 0, 2);
        body.Controls.Add(browser, 0, 0); body.Controls.Add(editor, 1, 0); shell.Controls.Add(body, 0, 2); Controls.Add(shell);
        connect.CheckedChanged += (_, _) =>
        {
            var owner = InspectorFiles.Read<InspectorControl>(loaderRoot, InspectorFiles.ControlName);
            if (connect.Checked && owner?.IsLive == true && owner.Client != control.Client)
            { connect.Checked = false; connection.Text = "另一个管理器正在调试，请先断开其连接"; return; }
            WriteControl(); if (!connect.Checked) connection.Text = "连接已关闭";
        };
        frames.CheckedChanged += (_, _) => WriteControl(); names.CheckedChanged += (_, _) => WriteControl(); inactive.CheckedChanged += (_, _) => { treeSignature = ""; RefreshTree(); WriteControl(); };
        search.TextChanged += (_, _) => { treeSignature = ""; RefreshTree(); };
        tree.BeforeSelect += (_, e) => { if (!selecting && e.Node?.Tag is InspectorNode selected && selected.Id != node?.Id && !ConfirmLeave()) e.Cancel = true; };
        tree.AfterSelect += (_, e) => { if (!selecting && e.Node?.Tag is InspectorNode selected) { SelectNode(selected); SendSelection(selected); } };
        locate.Click += (_, _) => { if (node != null) ResourcePicked?.Invoke(node); };
        addImage.Click += async (_, _) => await PickImage(); addVideo.Click += async (_, _) => await PickVideo();
        removeImage.Click += (_, _) => { draft.ImageFile = draft.ImageName = ""; pendingImage = null; preview.SetImage(null); slice.Checked = false; MarkDirty(); UpdateMediaLabels(); };
        removeVideo.Click += (_, _) => { draft.VideoFile = draft.VideoName = ""; pendingVideo = null; MarkDirty(); UpdateMediaLabels(); };
        foreach (var number in new[] { opacity, width, height }.Concat(border)) number.ValueChanged += (_, _) => { MarkDirty(); UpdatePreview(); };
        fit.SelectedIndexChanged += (_, _) => MarkDirty(); slice.CheckedChanged += (_, _) => { MarkDirty(); UpdatePreview(); UpdateEnabled(); };
        preview.BorderChanged += value =>
        {
            var previous = selecting; selecting = true;
            try { var values = new[] { value.X, value.Y, value.Z, value.W }; for (var i = 0; i < 4; i++) border[i].Value = (decimal)values[i]; }
            finally { selecting = previous; }
            MarkDirty();
        };
        save.Click += async (_, _) => await SaveAsync(); restore.Click += (_, _) => Restore();
        foreach (Control field in new Control[] { locate, addImage, addVideo, opacity, width, height, fit, slice }.Concat(border)) field.Enter += (_, _) => scroll.ScrollControlIntoView(field);
        timer.Tick += (_, _) => Poll(); timer.Start(); UpdateEnabled();
    }
    private static CheckBox Check(string text, bool value = false) => new() { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 0, 18, 0) };
    private static void Section(TableLayoutPanel fields, string text) { var label = AppVisuals.TextBlock(text, 11F, true); label.Margin = new Padding(0, 20, 0, 8); AppVisuals.AddRow(fields, label); }
    private static NumberField Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Top };
    private static void Field(TableLayoutPanel stack, string label, Control control)
    {
        var title = AppVisuals.TextBlock(label, 9F); title.Margin = new Padding(0, 10, 0, 4); AppVisuals.AddRow(stack, title);
        control.Dock = DockStyle.Top; control.AccessibleName = label; AppVisuals.AddRow(stack, control);
    }
    private static void DataLabel(TableLayoutPanel fields, Label label) { label.AutoSize = false; label.AutoEllipsis = true; label.Dock = DockStyle.Top; label.Height = 30; AppVisuals.AddRow(fields, label); }
    public bool ConfirmLeave() => !IsBusy && (!dirty || MessageBox.Show(FindForm(), "节点修改尚未保存，是否放弃？", "在线调试", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
    public void SetWorkspace(ThemeResourceWorkspace? value, ThemeItem? item)
    {
        if (dirty && workspace?.Root == value?.Root) return;
        workspace = value; theme = item;
        if (node != null) SelectNode(node); else UpdateEnabled();
    }
    private void WriteControl()
    {
        try
        {
            var owner = InspectorFiles.Read<InspectorControl>(loaderRoot, InspectorFiles.ControlName);
            if (owner?.IsLive == true && owner.Client != control.Client) return;
            control.Enabled = connect.Checked; control.LeaseUtc = DateTime.UtcNow; control.ShowFrames = frames.Checked; control.ShowNames = names.Checked; control.IncludeInactive = inactive.Checked;
            InspectorFiles.Write(loaderRoot, InspectorFiles.ControlName, control);
        }
        catch (Exception e) { connection.Text = "无法连接：" + e.Message; }
    }
    private void SendSelection(InspectorNode selected)
    { control.SelectedId = selected.Id; control.Runtime = snapshot?.Runtime ?? ""; control.SelectRequest++; WriteControl(); }
    private void Poll()
    {
        if (!connect.Checked) return;
        WriteControl();
        var next = InspectorFiles.Read<InspectorSnapshot>(loaderRoot, InspectorFiles.SnapshotName);
        if (next == null || next.Client != control.Client || next.UpdatedUtc < DateTime.UtcNow.AddSeconds(-5) || next.UpdatedUtc > DateTime.UtcNow.AddSeconds(5))
        { connection.Text = "等待游戏连接 · 请使用 2.6.0 或更高版本启动游戏"; return; }
        AcceptSnapshot(next);
    }
    internal void AcceptSnapshot(InspectorSnapshot next)
    {
        if (next.Runtime != lastRuntime)
        {
            lastRuntime = next.Runtime; lastRevision = -1; treeSignature = "";
            if (node != null) node.Id = 0; // Instance IDs belong to one game session.
        }
        snapshot = next;
        if (node != null && !dirty && !IsBusy)
        {
            var live = next.Nodes.FirstOrDefault(n => n.Id == node.Id && n.Address == node.Address && n.Scene == node.Scene)
                ?? next.Nodes.FirstOrDefault(n => n.Address == node.Address && n.Scene == node.Scene);
            if (live == null) state.Text = "节点暂未出现 · 保存的规则会在它出现时应用";
            else { node = live; if (live.DecorationStatus.Length > 0) state.Text = live.DecorationStatus; }
        }
        connection.Text = $"已连接 · {next.Nodes.Count} 个节点 · 主题 {next.Theme}" + (next.Truncated ? " · 层级过多，部分未显示" : "");
        RefreshTree();
        if (next.SelectionRevision == lastRevision || IsBusy) return;
        lastRevision = next.SelectionRevision;
        var picked = next.Nodes.FirstOrDefault(n => n.Id == next.SelectedId);
        if (picked == null) return;
        if (node?.Id != picked.Id && !ConfirmLeave()) { if (node != null) SendSelection(node); return; }
        if (node?.Id != picked.Id || !dirty) SelectNode(picked);
        if (next.SelectionOrigin == "game" && picked.LogicalId.Length > 0) ResourcePicked?.Invoke(picked);
    }
    private void RefreshTree()
    {
        if (snapshot == null) return;
        var signature = string.Join("|", snapshot.Nodes.Select(n => n.Id + ":" + n.ParentId + ":" + n.Name + ":" + n.Active)) + search.Text + inactive.Checked;
        if (signature == treeSignature) return; treeSignature = signature;
        var expanded = new HashSet<string>();
        void Remember(TreeNodeCollection items) { foreach (TreeNode item in items) { if (item.IsExpanded) expanded.Add(item.Name); Remember(item.Nodes); } }
        Remember(tree.Nodes); var all = snapshot.Nodes.ToDictionary(n => n.Id); var visible = new HashSet<int>(); var query = search.Text.Trim();
        foreach (var item in snapshot.Nodes.Where(n => (inactive.Checked || n.Active) && (n.Name + n.Hierarchy).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            var current = item; var seen = new HashSet<int>();
            while (seen.Add(current.Id)) { visible.Add(current.Id); if (!all.TryGetValue(current.ParentId, out var parent)) break; current = parent; }
        }
        selecting = true; tree.BeginUpdate(); tree.Nodes.Clear();
        try
        {
            var scenes = new Dictionary<string, TreeNode>(); var built = new Dictionary<int, TreeNode>();
            foreach (var item in snapshot.Nodes.Where(n => visible.Contains(n.Id)))
            {
                if (!scenes.TryGetValue(item.Scene, out var scene)) { scene = tree.Nodes.Add(item.Scene, item.Scene); scenes[item.Scene] = scene; scene.Expand(); }
                var parent = built.TryGetValue(item.ParentId, out var p) ? p : scene;
                var leaf = parent.Nodes.Add(item.Scene + "|" + item.Address, item.Name + (item.Active ? "" : " · 隐藏")); leaf.Tag = item; leaf.ToolTipText = item.Hierarchy + Environment.NewLine + item.Component;
                if (!item.Active) leaf.ForeColor = AppVisuals.MutedInk;
                built[item.Id] = leaf; if (expanded.Contains(leaf.Name) || query.Length > 0) leaf.Expand();
                if (node?.Id == item.Id) tree.SelectedNode = leaf;
            }
        }
        finally { tree.EndUpdate(); selecting = false; }
    }
    private void SelectNode(InspectorNode selected)
    {
        selecting = true;
        try
        {
            node = selected; pendingImage = pendingVideo = null; dirty = false;
            var rule = workspace?.NodeRule(selected.Scene, selected.Address);
            draft = rule == null ? new ThemeNodeRule { Scene = selected.Scene, Address = selected.Address, Hierarchy = selected.Hierarchy }
                : JsonSerializer.Deserialize<ThemeNodeRule>(JsonSerializer.Serialize(rule, JsonDefaults.Options), JsonDefaults.Options)!;
            title.Text = selected.Name; location.Text = selected.Scene + " · " + selected.Component + Environment.NewLine + selected.Hierarchy;
            tips.SetToolTip(title, selected.Name); tips.SetToolTip(location, location.Text + Environment.NewLine + selected.Address);
            opacity.Value = (decimal)(draft.VideoOpacity * 100); width.Value = (decimal)draft.Width; height.Value = (decimal)draft.Height;
            fit.SelectedIndex = draft.Fit == "contain" ? 1 : draft.Fit == "cover" ? 2 : 0; slice.Checked = draft.Border != null;
            var b = draft.Border ?? new Float4(); var values = new[] { b.X, b.Y, b.Z, b.W }; for (var i = 0; i < 4; i++) border[i].Value = (decimal)values[i];
            preview.SetImage(workspace != null && draft.ImageFile.Length > 0 ? ThemeResourceWorkspace.ReadBitmap(workspace.NodeFile(draft.ImageFile)) : null);
            UpdatePreview();
            state.Text = !selected.CanDecorate ? "此节点没有 UI 区域，可在层级中检查。" : selected.DecorationStatus.Length > 0 ? selected.DecorationStatus
                : selected.Width <= 0 || selected.Height <= 0 ? "节点尺寸为 0，请设置宽高。" : "可添加底图与视频，保存后实时应用。";
            UpdateMediaLabels(); UpdateEnabled();
            void Find(TreeNodeCollection items) { foreach (TreeNode item in items) { if (item.Tag is InspectorNode info && info.Id == selected.Id) { tree.SelectedNode = item; item.EnsureVisible(); return; } Find(item.Nodes); } }
            Find(tree.Nodes);
        }
        catch (Exception e) { state.Text = "读取节点失败：" + e.Message; }
        finally { selecting = false; UpdatePreview(); }
    }
    private void MarkDirty() { if (selecting || node == null) return; dirty = true; state.Text = "尚未保存"; UpdateEnabled(); }
    private void UpdatePreview()
    {
        if (selecting) return;
        preview.Sliced = slice.Checked; preview.TargetSize = new Size(width.Value > 0 ? (int)width.Value : (int)Math.Max(1, node?.Width ?? 320), height.Value > 0 ? (int)height.Value : (int)Math.Max(1, node?.Height ?? 200));
        preview.SetBorder(new Float4((float)border[0].Value, (float)border[1].Value, (float)border[2].Value, (float)border[3].Value));
    }
    private void UpdateMediaLabels()
    {
        imageName.Text = pendingImage != null ? Path.GetFileName(pendingImage) + " · 待保存" : draft.ImageFile.Length > 0 ? (draft.ImageName.Length > 0 ? draft.ImageName : Path.GetFileName(draft.ImageFile)) : "尚未添加底图";
        videoName.Text = pendingVideo != null ? Path.GetFileName(pendingVideo) + " · 待保存" : draft.VideoFile.Length > 0 ? (draft.VideoName.Length > 0 ? draft.VideoName : Path.GetFileName(draft.VideoFile)) : "尚未添加视频";
        tips.SetToolTip(imageName, pendingImage ?? draft.ImageFile); tips.SetToolTip(videoName, pendingVideo ?? draft.VideoFile);
    }
    private void UpdateEnabled()
    {
        var edit = workspace != null && node?.CanDecorate == true && !IsBusy;
        locate.Enabled = workspace != null && node?.LogicalId.Length > 0 && !IsBusy;
        locate.Visible = node?.LogicalId.Length > 0;
        addImage.Enabled = addVideo.Enabled = opacity.Enabled = fit.Enabled = width.Enabled = height.Enabled = edit;
        removeImage.Enabled = edit && (pendingImage != null || draft.ImageFile.Length > 0); removeVideo.Enabled = edit && (pendingVideo != null || draft.VideoFile.Length > 0);
        slice.Enabled = removeImage.Enabled; foreach (var number in border) number.Enabled = edit && slice.Checked;
        save.Enabled = edit && dirty; restore.Enabled = edit && workspace!.NodeRule(node!.Scene, node.Address) != null;
    }
    private async Task PickImage()
    {
        using var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        await ImportImageAsync(dialog.FileName);
    }
    internal async Task ImportImageAsync(string path)
    {
        if (workspace == null || node?.CanDecorate != true || IsBusy) return;
        IsBusy = true; UpdateEnabled();
        try { var bitmap = await Task.Run(() => ThemeResourceWorkspace.ReadBitmap(path)); preview.SetImage(bitmap); pendingImage = path; MarkDirty(); UpdateMediaLabels(); }
        catch (Exception e) { state.Text = "无法导入底图：" + e.Message; }
        finally { IsBusy = false; UpdateEnabled(); }
    }
    private async Task PickVideo()
    {
        using var dialog = new OpenFileDialog { Filter = "WebM 视频（VP8）|*.webm" };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await ImportVideoAsync(dialog.FileName);
    }
    internal async Task ImportVideoAsync(string path)
    {
        if (workspace == null || node?.CanDecorate != true || IsBusy) return;
        IsBusy = true; UpdateEnabled();
        try { var info = await Task.Run(() => WebmVideo.Inspect(path)); pendingVideo = path; MarkDirty(); UpdateMediaLabels(); videoName.Text += $" · {info.Width} × {info.Height}" + (info.HasAlpha ? " · 透明" : ""); }
        catch (Exception e) { state.Text = "无法导入视频：" + e.Message; }
        finally { IsBusy = false; UpdateEnabled(); }
    }
    public async Task SaveAsync()
    {
        if (workspace == null || theme == null || node == null || IsBusy || !dirty) return;
        IsBusy = true; UpdateEnabled();
        try
        {
            draft.VideoOpacity = (float)opacity.Value / 100; draft.Width = (float)width.Value; draft.Height = (float)height.Value;
            draft.Fit = fit.SelectedIndex == 1 ? "contain" : fit.SelectedIndex == 2 ? "cover" : "stretch";
            draft.Border = slice.Checked ? new Float4((float)border[0].Value, (float)border[1].Value, (float)border[2].Value, (float)border[3].Value) : null;
            if (draft.ImageFile.Length == 0 && pendingImage == null && draft.VideoFile.Length == 0 && pendingVideo == null) workspace.RemoveNode(node.Scene, node.Address);
            else await Task.Run(() => workspace.SaveNode(draft, pendingImage, pendingVideo));
            library.SetRealtimeMode(true); library.Activate(theme); dirty = false; pendingImage = pendingVideo = null;
            Applied?.Invoke(); state.Text = "已保存并应用主题 · 等待游戏刷新"; report(state.Text); UpdateMediaLabels();
        }
        catch (Exception e) { state.Text = "保存失败：" + e.Message; }
        finally { IsBusy = false; UpdateEnabled(); }
    }
    private void Restore()
    {
        if (workspace == null || node == null || theme == null || !ConfirmLeave()) return;
        try { workspace.RemoveNode(node.Scene, node.Address); library.SetRealtimeMode(true); library.Activate(theme); SelectNode(node); state.Text = "已移除此节点的底图和视频"; Applied?.Invoke(); }
        catch (Exception e) { state.Text = "恢复失败：" + e.Message; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); control.Enabled = false; try { if (InspectorFiles.Read<InspectorControl>(loaderRoot, InspectorFiles.ControlName)?.Client == control.Client) InspectorFiles.Write(loaderRoot, InspectorFiles.ControlName, control); } catch { } tips.Dispose(); }
        base.Dispose(disposing);
    }
}
