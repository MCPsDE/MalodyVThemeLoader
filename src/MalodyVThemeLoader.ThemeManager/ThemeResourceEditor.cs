using System.Media;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceEditor : UserControl
{
    private readonly string gameRoot;
    private readonly ThemeLibrary library;
    private readonly Action<string> report;
    private readonly TreeView tree = new ResourceTreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HideSelection = false, ShowLines = false, ShowRootLines = false, FullRowSelect = true };
    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "文件名、目录或音效名称" };
    private readonly ResourcePreviewSurface preview = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label resourceName = AppVisuals.TextBlock("选择一项资源", 12F, true);
    private readonly Label fileState = AppVisuals.TextBlock("导入图片或音频后保存，即可实时应用。", 9F);
    private readonly Label state = AppVisuals.TextBlock("选择图片或界面音频开始编辑。", 9F);
    private readonly ChoiceBox sceneBox = new() { Dock = DockStyle.Top, IntegralHeight = false, DropDownHeight = 260 };
    private readonly ChoiceBox savedScopes = new() { Dock = DockStyle.Top, IntegralHeight = false, DropDownHeight = 260 };
    private readonly Button addSceneScope = AppVisuals.CreateButton("新增场景替换");
    private readonly TextBox hierarchyBox = new() { Dock = DockStyle.Top, MaxLength = 1024 };
    private readonly ChoiceBox componentBox = new() { Dock = DockStyle.Top };
    private readonly CheckBox sliceToggle = new() { Text = "九宫格切片", AutoSize = true, Margin = new Padding(0, 14, 0, 8) };
    private readonly Dictionary<string, NumberField> borders = new();
    private readonly NumberField targetWidth = Number(1, 4096, 320);
    private readonly NumberField targetHeight = Number(1, 4096, 120);
    private readonly ListBox usages = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, HorizontalScrollbar = false };
    private readonly Label usagesTitle = AppVisuals.TextBlock("使用位置 · 双击选择", 9F);
    private readonly Button import = AppVisuals.CreateButton("导入替换文件…");
    private readonly Button restore = AppVisuals.CreateButton("恢复此范围");
    private readonly Button play = AppVisuals.CreateButton("试听");
    private readonly Button stop = AppVisuals.CreateButton("停止");
    private readonly Button save = AppVisuals.CreateButton("保存并实时应用", primary: true);
    private readonly Button readScope = AppVisuals.CreateButton("读取此范围");
    private readonly Button cancelScope = AppVisuals.CreateButton("取消定位");
    private readonly TableLayoutPanel body;
    private readonly Control imageFields;
    private readonly Control locationFields;
    private readonly Control componentFields;
    private readonly Control scopeActions;
    private readonly Control playback;
    private readonly ToolTip tips = new();
    private readonly System.Windows.Forms.Timer searchTimer = new() { Interval = 200 };
    private ThemeResourceWorkspace? workspace;
    private ThemeItem? theme;
    private ImageCatalogEntry? image;
    private AudioCatalogEntry? audio;
    private string? pendingFile;
    private string currentRuleFile = "";
    private string currentFileName = "";
    private string currentMode = "";
    private string loadedScene = "";
    private string loadedHierarchy = "";
    private string loadedComponent = "";
    private Live2dAudio? decodedAudio;
    private SoundPlayer? player;
    private MemoryStream? soundStream;
    private bool selecting;
    private bool dirty;
    public bool IsBusy { get; private set; }

    public ThemeResourceEditor(string gameRoot, ThemeLibrary library, Action<string> report)
    {
        this.gameRoot = gameRoot; this.library = library; this.report = report;
        BackColor = AppVisuals.Canvas; ForeColor = AppVisuals.Ink; Font = AppVisuals.UiFont();
        body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = AppVisuals.Surface, Padding = new Padding(10), Margin = new Padding(0, 0, 12, 0) };
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        browser.RowStyles.Add(new RowStyle(SizeType.AutoSize)); browser.RowStyles.Add(new RowStyle(SizeType.AutoSize)); browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        browser.Controls.Add(AppVisuals.TextBlock("搜索资源", 9F, true), 0, 0); browser.Controls.Add(TextFrame(search), 0, 1); browser.Controls.Add(tree, 0, 2);
        search.Margin = new Padding(0, 0, 0, 10); search.AccessibleName = "搜索图片和界面音频";
        tree.BackColor = AppVisuals.Surface; tree.ForeColor = AppVisuals.Ink; tree.AccessibleName = "皮肤资源目录树";
        tree.ItemHeight = 27; tree.ShowNodeToolTips = true;
        var viewer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 16, 0) };
        viewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        viewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); viewer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); viewer.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        viewer.Controls.Add(preview, 0, 0); viewer.Controls.Add(usagesTitle, 0, 1); viewer.Controls.Add(usages, 0, 2);
        usages.BackColor = AppVisuals.Surface; usages.ForeColor = AppVisuals.Ink; usages.Font = AppVisuals.UiFont(9F); usages.AccessibleName = "源场景中使用此图片的位置";
        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var fields = AppVisuals.Stack(); fields.Margin = Padding.Empty;
        resourceName.AutoSize = false; resourceName.Height = 28; resourceName.Dock = DockStyle.Top; resourceName.AutoEllipsis = true;
        AppVisuals.AddRow(fields, resourceName);
        Field(fields, "已保存的替换", savedScopes);
        savedScopes.AccessibleName = "此资源已保存的独立替换范围";
        AppVisuals.AddRow(fields, new ButtonRow(addSceneScope) { Margin = new Padding(0, 8, 0, 10) });
        Field(fields, "应用范围", sceneBox);
        locationFields = AppVisuals.Stack(); locationFields.Margin = Padding.Empty;
        hierarchyBox.PlaceholderText = "双击使用位置，或输入路径";
        Field((TableLayoutPanel)locationFields, "指定控件（可选）", TextFrame(hierarchyBox));
        componentFields = AppVisuals.Stack(); componentFields.Margin = Padding.Empty;
        Field((TableLayoutPanel)componentFields, "控件类型", componentBox);
        AppVisuals.AddRow((TableLayoutPanel)locationFields, componentFields);
        scopeActions = Row(readScope, cancelScope); scopeActions.Margin = new Padding(0, 8, 0, 0);
        AppVisuals.AddRow((TableLayoutPanel)locationFields, scopeActions);
        AppVisuals.AddRow(fields, locationFields);
        componentBox.Items.AddRange(new object[] { "所有图片控件", "Image", "RawImage", "SpriteRenderer" }); componentBox.SelectedIndex = 0;
        hierarchyBox.AccessibleName = "控件层级路径，可留空"; sceneBox.AccessibleName = "资源应用场景"; componentBox.AccessibleName = "图片控件类型";
        import.Dock = DockStyle.Top; import.Height = 38; import.Margin = new Padding(0, 14, 0, 8); AppVisuals.AddRow(fields, import);
        fileState.Dock = DockStyle.Top; fileState.AutoSize = false; fileState.Height = 38; fileState.AutoEllipsis = true; AppVisuals.AddRow(fields, fileState);
        playback = Row(play, stop); AppVisuals.AddRow(fields, playback);
        imageFields = AppVisuals.Stack(); imageFields.Margin = Padding.Empty;
        AppVisuals.AddRow((TableLayoutPanel)imageFields, sliceToggle);
        var grid = new FieldGrid();
        var names = new[] { "左", "右", "上", "下" };
        for (var i = 0; i < names.Length; i++)
        {
            var column = AppVisuals.Stack(); column.Margin = new Padding(0, 0, i % 2 == 0 ? 10 : 0, 8);
            var number = Number(0, 16384, 0); borders[names[i]] = number; number.AccessibleName = names[i] + "切片边距（像素）";
            Field(column, names[i] + " · px", number); grid.AddField(column);
            number.ValueChanged += (_, _) => { if (!selecting) { UpdateSlicePreview(); MarkDirty(); } };
        }
        AppVisuals.AddRow((TableLayoutPanel)imageFields, grid);
        var previewSize = new FieldGrid { Margin = new Padding(0, 12, 0, 0) };
        var widthField = AppVisuals.Stack(); widthField.Margin = new Padding(0, 0, 10, 0); Field(widthField, "预览宽度", targetWidth);
        var heightField = AppVisuals.Stack(); heightField.Margin = Padding.Empty; Field(heightField, "预览高度", targetHeight);
        previewSize.AddField(widthField); previewSize.AddField(heightField); AppVisuals.AddRow((TableLayoutPanel)imageFields, previewSize);
        AppVisuals.AddRow(fields, imageFields);
        restore.Dock = DockStyle.Top; restore.Height = 38; restore.Margin = new Padding(0, 18, 0, 8); AppVisuals.AddRow(fields, restore);
        scroll.Controls.Add(fields);
        settings.Controls.Add(scroll, 0, 0); save.Dock = DockStyle.Fill; save.Margin = new Padding(0, 12, 0, 0); settings.Controls.Add(save, 0, 1);
        body.Controls.Add(browser, 0, 0); body.Controls.Add(viewer, 1, 0); body.Controls.Add(settings, 2, 0);
        Controls.Add(body);
        BuildInspectorMode();
        tree.BeforeSelect += (_, e) => { if (!selecting && !ConfirmLeave()) e.Cancel = true; };
        tree.AfterSelect += (_, e) => { if (!selecting && e.Node?.Tag != null) SelectResource(e.Node.Tag); };
        tree.AllowDrop = true; tree.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true && (image != null || audio != null) ? DragDropEffects.Copy : DragDropEffects.None;
        tree.DragDrop += async (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1) await ImportFileAsync(files[0]); };
        search.TextChanged += (_, _) => { searchTimer.Stop(); searchTimer.Start(); };
        searchTimer.Tick += (_, _) => { searchTimer.Stop(); BuildTree(); };
        sceneBox.SelectedIndexChanged += (_, _) => ScopeChanged();
        savedScopes.SelectedIndexChanged += (_, _) => SelectSavedScope();
        addSceneScope.Click += (_, _) => AddSceneScope();
        hierarchyBox.TextChanged += (_, _) => ScopeInputChanged();
        componentBox.SelectedIndexChanged += (_, _) => ScopeInputChanged();
        readScope.Click += (_, _) => CommitScope(); cancelScope.Click += (_, _) => CancelScope();
        sliceToggle.CheckedChanged += (_, _) => { if (!selecting) { currentMode = sliceToggle.Checked ? "sliced" : "simple"; MarkDirty(); } UpdateSlicePreview(); };
        targetWidth.ValueChanged += (_, _) => UpdateSlicePreview(); targetHeight.ValueChanged += (_, _) => UpdateSlicePreview();
        preview.BorderChanged += value => { SetBorders(value); MarkDirty(); };
        import.Click += async (_, _) => await PickFileAsync();
        save.Click += async (_, _) => await SaveAsync(); restore.Click += (_, _) => RestoreScope();
        play.Click += (_, _) => Audition(); stop.Click += (_, _) => { StopAudio(); UpdateEnabled(); Notify("试听已停止。"); };
        usages.DoubleClick += (_, _) => SelectUsage();
        scroll.Resize += (_, _) => fields.Width = Math.Max(1, scroll.ClientSize.Width - scroll.Padding.Horizontal);
        foreach (Control field in new Control[] { hierarchyBox, componentBox, sliceToggle, targetWidth, targetHeight }.Concat(borders.Values))
            field.Enter += (_, _) => scroll.ScrollControlIntoView(field);
        UpdateEnabled();
    }

    private static void Field(TableLayoutPanel parent, string name, Control value)
    {
        var label = AppVisuals.TextBlock(name, 9F); label.Margin = new Padding(0, 8, 0, 4);
        value.Dock = DockStyle.Top; value.Margin = new Padding(0, 0, 0, 2);
        AppVisuals.AddRow(parent, label); AppVisuals.AddRow(parent, value);
    }
    private static NumberField Number(int minimum, int maximum, int value) => new() { Minimum = minimum, Maximum = maximum, Value = value, Dock = DockStyle.Top };
    private static Control TextFrame(TextBox text)
    {
        return new TextField(text);
    }
    private static Control Row(params Button[] buttons)
    {
        return new ButtonRow(buttons);
    }

    public bool ConfirmLeave()
    {
        if (inspectorPane != null && !inspectorPane.ConfirmLeave()) return false;
        if (IsBusy) return false;
        if (!dirty) return true;
        return MessageBox.Show(FindForm(), "当前修改尚未保存，是否放弃？", "资源编辑", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { StopAudio(); searchTimer.Dispose(); tips.Dispose(); }
        base.Dispose(disposing);
    }
}
