using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeManagerForm
{
    private readonly TextBox searchBox = new();
    private readonly Label listState = AppVisuals.TextBlock("主题库", 10F, true);
    private readonly ThemePreviewSurface preview = new();
    private readonly Button manifestButton = AppVisuals.CreateButton("查看 theme.json");
    private readonly Label emptyHint = AppVisuals.TextBlock("试试其他搜索词，或导入一个 ZIP 主题包。");
    private Control? metadataPanel;
    private readonly Panel pageHost = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, Control> pages = new();
    private readonly Dictionary<string, Button> navigation = new();
    private bool themeOperationBusy;
    private ListBox? live2dModels;
    private ListBox? live2dAnimations;
    private Label? live2dState;
    private Button? live2dImportDirectory;
    private Button? live2dImportArchive;
    private Button? live2dImportAnimation;
    private Button? live2dImportTexture;

    public ThemeManagerForm(string gameRoot)
    {
        this.gameRoot = gameRoot;
        library = new ThemeLibrary(gameRoot);
        Text = "ThemeLoader · 主题管理器";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 800);
        MinimumSize = new Size(960, 700);
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();
        Shown += (_, _) => WindowsBackdrop.TryApplyMica(this);
        AllowDrop = true;

        exportButton = AppVisuals.CreateButton("导出 ZIP");
        activateButton = AppVisuals.CreateButton("应用主题", primary: true);
        removeButton = AppVisuals.CreateButton("移除");
        exportButton.Click += (_, _) => ExportSelected();
        activateButton.Click += (_, _) => ActivateSelected();
        removeButton.Click += (_, _) => RemoveSelected();
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        shell.Controls.Add(BuildNavigation(), 0, 0);
        shell.SetRowSpan(shell.GetControlFromPosition(0, 0)!, 2);
        shell.Controls.Add(pageHost, 1, 0);
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.AutoSize = false;
        statusLabel.Padding = new Padding(24, 8, 24, 8);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.ForeColor = AppVisuals.MutedInk;
        statusLabel.BackColor = AppVisuals.Surface;
        statusLabel.UseMnemonic = false;
        statusLabel.AccessibleName = "操作结果";
        shell.Controls.Add(statusLabel, 1, 1);
        pages["themes"] = BuildThemePage();
        pages["live2d"] = BuildLive2dPage();
        pages["settings"] = BuildSettingsPage();
        pages["maintenance"] = BuildMaintenancePage();
        foreach (var page in pages.Values) { page.Dock = DockStyle.Fill; page.Visible = false; pageHost.Controls.Add(page); }
        Controls.Add(shell);
        Controls.Add(new BrandBar());
        Navigate("themes");
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        Shown += async (_, _) =>
        {
            RefreshRealtimeMode();
            RefreshControlMode();
            RefreshThemes();
            await CheckForUpdatesAsync();
        };
        FormClosed += (_, _) => { preview.Image?.Dispose(); };
        FormClosing += (_,e) => { if (themeOperationBusy) e.Cancel = true; };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F) { Navigate("themes"); searchBox.Focus(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.O) { ImportFromDialog(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.F5) { RefreshThemes(SelectedTheme?.Key); e.SuppressKeyPress = true; }
        };
        AppAppearance.Initialize(this);
    }

    private Control BuildNavigation()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppVisuals.Surface, Padding = new Padding(20, 28, 16, 20), Margin = Padding.Empty };
        var stack = AppVisuals.Stack();
        var logo = new PictureBox { Image = AppVisuals.CreateLogoImage(), Size = new Size(42, 42), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, 12) };
        AppVisuals.AddRow(stack, logo);
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("ThemeLoader", 13F, true));
        var subtitle = AppVisuals.TextBlock("Malody V 主题管理", 9F);
        subtitle.Margin = new Padding(0, 0, 0, 32);
        AppVisuals.AddRow(stack, subtitle);
        foreach (var (key, text) in new[] { ("themes", "主题库"), ("live2d", "Live2D"), ("settings", "设置"), ("maintenance", "维护") })
        {
            var button = AppVisuals.CreateButton(text);
            button.Dock = DockStyle.Top;
            button.Height = 46;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12,0,0,0);
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(0,0,0,8);
            button.Click += (_, _) => Navigate(key);
            navigation[key] = button;
            AppVisuals.AddRow(stack, button);
        }
        var version = AppVisuals.TextBlock("版本 " + typeof(ThemeManagerForm).Assembly.GetName().Version?.ToString(3), 9F);
        version.Dock = DockStyle.Bottom;
        panel.Controls.Add(stack);
        panel.Controls.Add(version);
        return panel;
    }

    private void Navigate(string key)
    {
        foreach (var (name, page) in pages) page.Visible = name == key;
        if (pages.TryGetValue(key, out var current)) current.BringToFront();
        foreach (var (name, button) in navigation)
        {
            button.BackColor = name == key ? AppVisuals.AccentSoft : AppVisuals.Surface;
            button.ForeColor = name == key ? AppVisuals.Accent : AppVisuals.MutedInk;
            button.Font = AppVisuals.UiFont(10F, name == key ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private void SetThemeBusy(bool value, string? message = null)
    {
        themeOperationBusy = value;
        pageHost.Enabled = !value;
        UseWaitCursor = value;
        if (message is not null) ShowStatus(message);
        UpdateCommandState();
    }

    private async Task CheckForUpdatesAsync()
    {
        var result = await UpdateService.CheckAndStartAsync(
            gameRoot,
            new Progress<string>(ShowStatus));
        if (result.State == UpdateState.Started)
        {
            BeginInvoke(Close);
        }
        else if (result.State == UpdateState.Failed)
        {
            ShowStatus(result.Message);
        }
    }

    private Control BuildThemePage()
    {
        var page = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Padding = new Padding(28,24,28,20), Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0,0,0,22) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));
        header.Controls.Add(AppVisuals.TextBlock("你的主题库",22F,true),0,0);
        var import = AppVisuals.CreateButton("导入 ZIP…", primary: true);
        import.Dock = DockStyle.Fill;
        import.Margin = new Padding(8,0,0,8);
        import.Click += (_,_) => ImportFromDialog();
        header.Controls.Add(import,1,0);
        header.Controls.Add(AppVisuals.TextBlock("选择喜欢的外观，也可以拖入主题包。"),0,1);
        header.SetColumnSpan(header.GetControlFromPosition(0,1)!,2);
        page.Controls.Add(header,0,0);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));
        var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = AppVisuals.Surface, Padding = new Padding(12), Margin = new Padding(0,0,20,0) };
        listPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        searchBox.PlaceholderText = "搜索名称或作者";
        searchBox.AccessibleName = "搜索主题名称或作者";
        searchBox.Dock = DockStyle.Top;
        searchBox.Margin = new Padding(0,0,0,12);
        searchBox.TextChanged += (_,_) => RefreshThemes(SelectedTheme?.Key);
        listPanel.Controls.Add(AppVisuals.TextBlock("搜索主题",9F,true),0,0);
        listPanel.Controls.Add(searchBox,0,1);
        listPanel.Controls.Add(listState,0,2);
        themeList.Dock = DockStyle.Fill;
        themeList.DrawMode = DrawMode.OwnerDrawFixed;
        themeList.ItemHeight = 64;
        themeList.IntegralHeight = false;
        themeList.HorizontalScrollbar = false;
        themeList.SelectionMode = SelectionMode.One;
        themeList.DisplayMember = nameof(ThemeItem.DisplayName);
        themeList.BorderStyle = BorderStyle.None;
        themeList.BackColor = AppVisuals.Surface;
        themeList.ForeColor = AppVisuals.Ink;
        themeList.AccessibleName = "已安装的主题";
        themeList.DrawItem += (_,e) => DrawThemeRow(e);
        themeList.SelectedIndexChanged += (_,_) => ShowSelection();
        themeList.DoubleClick += (_,_) => ActivateSelected();
        listPanel.Controls.Add(themeList,0,3);
        columns.Controls.Add(listPanel,0,0);
        columns.Controls.Add(BuildThemeDetail(),1,0);
        page.Controls.Add(columns,0,1);
        return page;
    }

    private void DrawThemeRow(DrawItemEventArgs e)
    {
        if (e.Index < 0 || themeList.Items[e.Index] is not ThemeItem item) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? AppVisuals.AccentSoft : AppVisuals.Surface);
        e.Graphics.FillRectangle(brush,e.Bounds);
        var bounds = e.Bounds;
        using var titleFont = AppVisuals.UiFont(10F,FontStyle.Bold);
        using var detailFont = AppVisuals.UiFont(9F);
        TextRenderer.DrawText(e.Graphics,item.DisplayName,titleFont,new Rectangle(bounds.X+10,bounds.Y+9,bounds.Width-20,24),item.IsValid ? AppVisuals.Ink : AppVisuals.Danger,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        var active = string.Equals(item.Key,library.GetActiveTheme(),StringComparison.OrdinalIgnoreCase);
        var detail = active ? "正在使用" : item.Info?.Manifest.Author;
        if (string.IsNullOrWhiteSpace(detail)) detail = item.Info?.Manifest.Version ?? "需要修复";
        TextRenderer.DrawText(e.Graphics,detail,detailFont,new Rectangle(bounds.X+10,bounds.Y+35,bounds.Width-20,22),active ? AppVisuals.Accent : AppVisuals.MutedInk,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if (selected && themeList.Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(bounds,-2,-2),AppVisuals.Accent,AppVisuals.AccentSoft);
    }

    private Control BuildThemeDetail()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var detail = AppVisuals.Stack();
        var previewPanel = new Panel { Dock = DockStyle.Top, Height = 184, BackColor = AppVisuals.SurfaceAlt, Margin = new Padding(0,0,0,16) };
        preview.Dock = DockStyle.Fill;
        previewPanel.Controls.Add(preview);
        AppVisuals.AddRow(detail,previewPanel);
        nameValue.Dock = DockStyle.Top;
        nameValue.Font = AppVisuals.UiFont(20F,FontStyle.Bold);
        nameValue.Height = 42;
        AppVisuals.AddRow(detail,nameValue);
        activeValue.Dock = DockStyle.Top;
        activeValue.Height = 26;
        AppVisuals.AddRow(detail,activeValue);
        AppVisuals.AddRow(detail,emptyHint);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, WrapContents = false, Margin = new Padding(0,8,0,12) };
        foreach (var button in new[] {activateButton,exportButton,removeButton})
        {
            button.AutoSize = true;
            button.MinimumSize = new Size(button == activateButton ? 116 : 90,42);
            button.Margin = new Padding(0,0,8,8);
            actions.Controls.Add(button);
        }
        AppVisuals.AddRow(detail,actions);
        var info = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4, RowCount = 3, Margin = Padding.Empty };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        void Field(string label, Control value, int column, int row)
        {
            var key = AppVisuals.TextBlock(label,9F);
            key.Dock = DockStyle.Fill; key.TextAlign = ContentAlignment.MiddleLeft; key.Margin = Padding.Empty;
            info.Controls.Add(key,column,row); info.Controls.Add(value,column+1,row);
        }
        Field("作者",authorValue,0,0);
        Field("版本",versionValue,2,0);
        Field("游戏",gameVersionValue,0,1);
        Field("内容",sizeValue,2,1);
        Field("说明",descriptionValue,0,2);
        info.SetColumnSpan(descriptionValue,3);
        AppVisuals.AddRow(detail,info);
        metadataPanel = info;
        manifestButton.AutoSize = true;
        manifestButton.MinimumSize = new Size(164,42);
        manifestButton.ForeColor = AppVisuals.Accent;
        manifestButton.Margin = new Padding(0,8,0,8);
        manifestButton.FlatAppearance.BorderSize = 0;
        manifestButton.Click += (_,_) =>
        {
            if (SelectedTheme?.Info is null) return;
            using var dialog = new Form { Text = "theme.json", ClientSize = new Size(640,480), StartPosition = FormStartPosition.CenterParent, Font = AppVisuals.UiFont() };
            jsonBox.Dock = DockStyle.Fill;
            jsonBox.Multiline = jsonBox.ReadOnly = true;
            jsonBox.WordWrap = false;
            jsonBox.ScrollBars = ScrollBars.Both;
            jsonBox.Font = AppVisuals.MonoFont();
            dialog.Controls.Add(jsonBox);
            AppAppearance.Initialize(dialog);
            dialog.ShowDialog(this);
            dialog.Controls.Remove(jsonBox);
        };
        AppVisuals.AddRow(detail,manifestButton);
        scroll.Controls.Add(detail);
        return scroll;
    }

    private Control BuildSettingsPage()
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(32,28,32,24) };
        var stack = AppVisuals.Stack();
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("设置",22F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("调整主题更新方式和画面控制。"));
        AppVisuals.AddRow(stack,new Separator { Margin = new Padding(0,12,0,24) });
        realtimeToggle.Text = "实时更新主题";
        realtimeToggle.AutoSize = true;
        realtimeToggle.Margin = new Padding(0,0,0,12);
        realtimeToggle.CheckedChanged += (_,_) => ToggleRealtimeMode();
        AppVisuals.AddRow(stack,realtimeToggle);
        var realtimeHint = AppVisuals.TextBlock("启用后，修改主题或切换主题会自动更新运行中的游戏画面。关闭时，下次启动游戏生效。");
        realtimeHint.Margin = new Padding(0,0,0,32);
        AppVisuals.AddRow(stack,realtimeHint);
        controlModeToggle.Text = "启用画面控制";
        controlModeToggle.AutoSize = true;
        controlModeToggle.Margin = new Padding(0,0,0,12);
        controlModeToggle.CheckedChanged += (_,_) => ToggleControlMode();
        AppVisuals.AddRow(stack,controlModeToggle);
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("按住 Ctrl，使用 WASD、鼠标和滚轮调整相机。可为控制模式设置底图和顶图。"));
        var images = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Margin = new Padding(0,0,0,32) };
        foreach (var button in new[] {controlBackgroundButton,controlTopButton})
        {
            button.AutoSize = true; button.MinimumSize = new Size(124,42); button.Margin = new Padding(0,0,12,8); images.Controls.Add(button);
        }
        controlBackgroundButton.Click += (_,_) => SetControlBackgroundImage();
        controlTopButton.Click += (_,_) => SetControlTopImage();
        AppVisuals.AddRow(stack,images);
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("文件位置",11F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock(gameRoot));
        var folder = AppVisuals.CreateButton("打开主题目录");
        folder.AutoSize = true; folder.MinimumSize = new Size(150,42);
        folder.Click += (_,_) => OpenThemeFolder();
        AppVisuals.AddRow(stack,folder);
        page.Controls.Add(stack);
        return page;
    }

    private Control BuildLive2dPage()
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(32, 28, 32, 24) };
        var stack = AppVisuals.Stack();
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("Live2D 工作台", 22F, true));
        AppVisuals.AddRow(stack, AppVisuals.TextBlock(
            "管理当前主题的模型、动画和纹理。"));
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 260,
            ColumnCount = 2,
            Margin = new Padding(0, 16, 0, 12)
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        live2dModels = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            BackColor = AppVisuals.Surface,
            ForeColor = AppVisuals.Ink,
            Font = AppVisuals.UiFont(10F),
            SelectionMode = SelectionMode.One,
            AccessibleName = "Live2D 模型列表"
        };
        live2dModels.SelectedIndexChanged += (_, _) => RefreshLive2dAnimations();
        live2dAnimations = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            BackColor = AppVisuals.Surface,
            ForeColor = AppVisuals.MutedInk,
            Font = AppVisuals.MonoFont(9F),
            SelectionMode = SelectionMode.One,
            AccessibleName = "当前模型动画列表"
        };
        var modelPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12), BackColor = AppVisuals.Surface, Margin = new Padding(0, 0, 12, 0) };
        modelPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modelPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modelPanel.Controls.Add(AppVisuals.TextBlock("模型", 10F, true), 0, 0);
        modelPanel.Controls.Add(live2dModels, 0, 1);
        var animationPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12), BackColor = AppVisuals.Surface, Margin = Padding.Empty };
        animationPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        animationPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        animationPanel.Controls.Add(AppVisuals.TextBlock("已有动画", 10F, true), 0, 0);
        animationPanel.Controls.Add(live2dAnimations, 0, 1);
        columns.Controls.Add(modelPanel, 0, 0);
        columns.Controls.Add(animationPanel, 1, 0);
        AppVisuals.AddRow(stack, columns);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 0, 0, 8) };
        var importDirectory = AppVisuals.CreateButton("导入模型目录");
        var importArchive = AppVisuals.CreateButton("导入 Live2D ZIP");
        var importAnimation = AppVisuals.CreateButton("导入动画");
        var importTexture = AppVisuals.CreateButton("替换纹理");
        live2dImportDirectory = importDirectory;
        live2dImportArchive = importArchive;
        live2dImportAnimation = importAnimation;
        live2dImportTexture = importTexture;
        foreach (var button in new[] { importDirectory, importArchive, importAnimation, importTexture })
        {
            button.AutoSize = true;
            button.MinimumSize = new Size(button == importArchive ? 150 : 120, 36);
            button.Margin = new Padding(0, 0, 8, 8);
            actions.Controls.Add(button);
        }
        importDirectory.Click += (_, _) => ImportLive2dDirectory();
        importArchive.Click += (_, _) => ImportLive2dArchive();
        importAnimation.Click += (_, _) => ImportLive2dAnimations();
        importTexture.Click += (_, _) => ImportLive2dTexture();
        AppVisuals.AddRow(stack, actions);
        live2dState = AppVisuals.TextBlock("请先在主题库选择主题。", 9F);
        live2dState.AutoEllipsis = true;
        AppVisuals.AddRow(stack, live2dState);
        page.Controls.Add(stack);
        return page;
    }

    private void RefreshLive2d()
    {
        if (live2dModels is null) return;
        live2dModels.Items.Clear();
        live2dAnimations?.Items.Clear();
        var item = SelectedTheme;
        var editable = item is { IsValid: true, IsVirtual: false };
        if (live2dImportDirectory is not null) live2dImportDirectory.Enabled = editable;
        if (live2dImportArchive is not null) live2dImportArchive.Enabled = editable;
        if (live2dImportAnimation is not null) live2dImportAnimation.Enabled = false;
        if (live2dImportTexture is not null) live2dImportTexture.Enabled = editable;
        if (item is null || !item.IsValid || item.IsVirtual)
        {
            if (live2dState is not null) live2dState.Text = "请先在主题库选择主题。";
            return;
        }
        try
        {
            foreach (var model in Live2dWorkspace.GetModels(item)) live2dModels.Items.Add(model);
            if (live2dModels.Items.Count > 0) live2dModels.SelectedIndex = 0;
            if (live2dState is not null)
                live2dState.Text = $"当前主题：{item.DisplayName} · {live2dModels.Items.Count} 个模型";
        }
        catch (Exception exception)
        {
            if (live2dState is not null) live2dState.Text = "读取 Live2D 目录失败：" + exception.Message;
        }
    }

    private void RefreshLive2dAnimations()
    {
        if (live2dAnimations is null) return;
        live2dAnimations.Items.Clear();
        if (live2dImportAnimation is not null) live2dImportAnimation.Enabled = false;
        if (SelectedTheme is not { } item || live2dModels?.SelectedItem is not string model) return;
        foreach (var animation in Live2dWorkspace.GetAnimationPaths(item, model)) live2dAnimations.Items.Add(animation);
        if (live2dImportAnimation is not null) live2dImportAnimation.Enabled = true;
    }

    private void ImportLive2dDirectory()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) { ShowError("请先选择一个可编辑的目录主题。"); return; }
        using var dialog = new FolderBrowserDialog { Description = "选择 Live2D 模型目录或包含多个模型的目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Live2dWorkspace.ImportDirectory(item, dialog.SelectedPath); RefreshThemes(item.Key); RefreshLive2d(); ShowStatus("模型目录已导入。"); }
        catch (Exception exception) { ShowError("导入 Live2D 目录失败：" + exception.Message); }
    }

    private void ImportLive2dArchive()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) { ShowError("请先选择一个可编辑的目录主题。"); return; }
        using var dialog = new OpenFileDialog { Title = "导入 Live2D ZIP", Filter = "ZIP 文件 (*.zip;*.mtp)|*.zip;*.mtp", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Live2dWorkspace.ImportArchive(item, dialog.FileName); RefreshThemes(item.Key); RefreshLive2d(); ShowStatus("Live2D ZIP 已提取到当前主题目录。"); }
        catch (Exception exception) { ShowError("导入 Live2D ZIP 失败：" + exception.Message); }
    }

    private void ImportLive2dAnimations()
    {
        if (SelectedTheme is not { } item || item.IsVirtual || live2dModels?.SelectedItem is not string model) { ShowError("请先选择一个模型。"); return; }
        using var dialog = new OpenFileDialog { Title = "导入动画", Filter = "动画文件 (*.motion3.json;*.mtn;*.bytes;*.anim)|*.motion3.json;*.mtn;*.bytes;*.anim", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Live2dWorkspace.ImportAnimations(item, model, dialog.FileNames); RefreshLive2dAnimations(); ShowStatus($"已导入 {dialog.FileNames.Length} 个动画文件。"); }
        catch (Exception exception) { ShowError("添加动画失败：" + exception.Message); }
    }

    private void ImportLive2dTexture()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) { ShowError("请先选择一个可编辑的目录主题。"); return; }
        using var dialog = new OpenFileDialog { Title = "替换纹理", Filter = "静态图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|GIF 首帧 (*.gif)|*.gif", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var targets = Live2dWorkspace.GetTexturePaths(item);
        if (targets.Count == 0) { ShowError("当前主题没有可替换的 Live2D PNG/JPG 纹理。"); return; }
        using var chooser = new Form { Text = "选择要替换的纹理", ClientSize = new Size(560, 150), StartPosition = FormStartPosition.CenterParent, Font = AppVisuals.UiFont() };
        var combo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = targets.ToList(), Margin = new Padding(16) };
        var ok = AppVisuals.CreateButton("替换", primary: true); ok.Dock = DockStyle.Bottom; ok.Height = 36; ok.DialogResult = DialogResult.OK;
        chooser.Controls.Add(combo); chooser.Controls.Add(ok); chooser.AcceptButton = ok;
        AppAppearance.Initialize(chooser);
        if (chooser.ShowDialog(this) != DialogResult.OK || combo.SelectedItem is not string target) return;
        try { Live2dWorkspace.ImportImageOrGif(item, dialog.FileName, target); RefreshLive2d(); ShowStatus("纹理已替换。"); }
        catch (Exception exception) { ShowError("替换 Live2D 纹理失败：" + exception.Message); }
    }

    private Control BuildMaintenancePage()
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(32,28,32,24) };
        var stack = AppVisuals.Stack();
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("维护",22F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("整理本地主题，或移除加载器。"));
        AppVisuals.AddRow(stack,new Separator { Margin = new Padding(0,12,0,24) });
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("清理导入的主题",12F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("移除导入的自定义主题，保留 Default，并恢复原始外观。建议先导出需要保留的主题。"));
        var clear = AppVisuals.CreateButton("清空导入主题");
        clear.AutoSize = true; clear.MinimumSize = new Size(156,42); clear.Margin = new Padding(0,0,0,36);
        clear.Click += (_,_) => RemoveAllImported();
        AppVisuals.AddRow(stack,clear);
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("卸载 ThemeLoader",12F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("按安装清单移除加载器和管理器。你的主题、修改过的文件，以及原有的 BepInEx 会保留。"));
        var uninstall = AppVisuals.CreateButton("卸载 ThemeLoader",danger:true);
        uninstall.AutoSize = true; uninstall.MinimumSize = new Size(180,42);
        uninstall.Click += (_,_) => CompleteUninstall();
        AppVisuals.AddRow(stack,uninstall);
        page.Controls.Add(stack);
        return page;
    }

    private void OpenThemeFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(gameRoot,"ThemeLoader","themes")) { UseShellExecute = true }); }
        catch (Exception exception) { ShowError("无法打开主题目录：" + exception.Message); }
    }

    private void SetPreview(ThemeItem? item)
    {
        var old = preview.Image;
        preview.Image = ThemePreview.Load(item);
        preview.EmptyText = item == null ? "没有搜索结果" : item.IsValid ? "此主题未提供预览图" : "无法预览此主题";
        old?.Dispose();
        preview.Invalidate();
    }
}
