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
    private readonly UpdateStrip updateStrip = new() { Visible = false };
    private CancellationTokenSource? updateCancellation;
    private bool updateReadyToClose;
    private TableLayoutPanel? shell;
    private Panel? navigationPanel;
    private readonly List<Control> navigationLabels = new();
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
        shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 208));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        shell.Controls.Add(BuildNavigation(), 0, 0);
        shell.SetRowSpan(shell.GetControlFromPosition(0, 0)!, 3);
        shell.Controls.Add(pageHost, 1, 0);
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.AutoSize = false;
        statusLabel.Padding = new Padding(24, 8, 24, 8);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.ForeColor = AppVisuals.MutedInk;
        statusLabel.BackColor = AppVisuals.Surface;
        statusLabel.UseMnemonic = false;
        statusLabel.AccessibleName = "操作结果";
        shell.Controls.Add(updateStrip, 1, 1);
        shell.Controls.Add(statusLabel, 1, 2);
        updateStrip.ActionRequested += async () => { if (updateCancellation != null) updateCancellation.Cancel(); else await CheckForUpdatesAsync(); };
        pages["themes"] = BuildThemePage();
        pages["resources"] = BuildResourcesPage();
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
        FormClosed += (_, _) => { updateCancellation?.Cancel(); preview.Image?.Dispose(); };
        FormClosing += (_,e) => { if (themeOperationBusy || !updateReadyToClose && resourceEditor?.ConfirmLeave() == false) e.Cancel = true; };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F) { Navigate("themes"); searchBox.Focus(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.O) { ImportFromDialog(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.F5) { RefreshThemes(SelectedTheme?.Key); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.S && pages["resources"].Visible) { _ = resourceEditor?.SaveAsync(); e.SuppressKeyPress = true; }
        };
        AppAppearance.Initialize(this);
        Shown += (_, _) => FitNavigation();
        DpiChanged += (_, _) => BeginInvoke(FitNavigation);
    }

    private Control BuildNavigation()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppVisuals.Surface, Padding = new Padding(20, 28, 16, 20), Margin = Padding.Empty };
        navigationPanel = panel;
        var stack = AppVisuals.Stack();
        var logo = new PictureBox { Image = AppVisuals.CreateLogoImage(), Size = new Size(42, 42), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, 12) };
        AppVisuals.AddRow(stack, logo);
        var wordmark = AppVisuals.SingleLineText("ThemeLoader", 13F, true);
        navigationLabels.Add(wordmark); AppVisuals.AddRow(stack, wordmark);
        var subtitle = AppVisuals.SingleLineText("Malody V 主题管理", 9F);
        navigationLabels.Add(subtitle);
        subtitle.Margin = new Padding(0, 0, 0, 32);
        AppVisuals.AddRow(stack, subtitle);
        foreach (var (key, text) in new[] { ("themes", "主题库"), ("resources", "资源编辑"), ("live2d", "Live2D"), ("settings", "设置"), ("maintenance", "维护") })
        {
            var button = AppVisuals.CreateButton(text);
            button.Dock = DockStyle.Top;
            button.Height = 46;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = Padding.Empty;
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(0,0,0,8);
            button.Click += (_, _) => Navigate(key);
            navigation[key] = button;
            AppVisuals.AddRow(stack, button);
        }
        var version = AppVisuals.SingleLineText("版本 " + typeof(ThemeManagerForm).Assembly.GetName().Version?.ToString(3), 9F);
        navigationLabels.Add(version);
        version.Dock = DockStyle.Bottom;
        panel.Controls.Add(stack);
        panel.Controls.Add(version);
        return panel;
    }

    private void FitNavigation()
    {
        if (shell == null || navigationPanel == null || IsDisposed) return;
        var preferred = navigationLabels.Concat(navigation.Values).Select(c => c.GetPreferredSize(Size.Empty).Width + c.Margin.Horizontal).DefaultIfEmpty(0).Max();
        var width = Math.Max((int)Math.Ceiling(208 * DeviceDpi / 96f), preferred + navigationPanel.Padding.Horizontal + (int)Math.Ceiling(8 * DeviceDpi / 96f));
        if (Math.Abs(shell.ColumnStyles[0].Width - width) > 1) shell.ColumnStyles[0].Width = width;
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
        FitNavigation();
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
        if (updateCancellation != null) return;
        using var cancellation = new CancellationTokenSource(); updateCancellation = cancellation;
        try
        {
            var result = await UpdateService.CheckAndStartAsync(gameRoot,
                new Progress<UpdateProgress>(value => { if (!IsDisposed && updateCancellation == cancellation) updateStrip.ShowProgress(value); }), cancellation.Token,
                async () =>
                {
                    while (themeOperationBusy || resourceEditor?.IsBusy == true) await Task.Delay(250, cancellation.Token);
                    if (IsDisposed || resourceEditor?.ConfirmLeave() == false) return false;
                    updateReadyToClose = true; return true;
                });
            if (IsDisposed) return;
            if (result.State == UpdateState.Started) BeginInvoke(Close);
            else if (result.State == UpdateState.None) updateStrip.Visible = false;
            else { updateReadyToClose = false; updateStrip.ShowResult(result); }
        }
        finally { updateCancellation = null; }
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
