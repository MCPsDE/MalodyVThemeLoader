using System.Globalization;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class ThemeManagerForm : Form
{
    private readonly string gameRoot;
    private readonly ThemeLibrary library;
    private readonly ListView themeList = new();
    private readonly Label nameValue = CreateValueLabel();
    private readonly Label versionValue = CreateValueLabel();
    private readonly Label authorValue = CreateValueLabel();
    private readonly Label gameVersionValue = CreateValueLabel();
    private readonly Label sizeValue = CreateValueLabel();
    private readonly Label activeValue = CreateValueLabel();
    private readonly Label descriptionValue = CreateValueLabel();
    private readonly TextBox jsonBox = new();
    private readonly Label statusLabel = new();
    private readonly CheckBox realtimeToggle = new();
    private readonly CheckBox controlModeToggle = new();
    private readonly Button controlBackgroundButton =
        AppVisuals.CreateButton("设置底图");
    private readonly Button exportButton;
    private readonly Button activateButton;
    private readonly Button removeButton;
    private bool updatingRealtimeToggle;
    private bool updatingControlModeToggle;

    public ThemeManagerForm(string gameRoot)
    {
        this.gameRoot = gameRoot;
        library = new ThemeLibrary(gameRoot);

        Text = "主题管理器";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1040, 680);
        MinimumSize = new Size(900, 600);
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();
        AllowDrop = true;

        var header = BuildHeader();
        var commandBar = BuildCommandBar(
            out exportButton, out activateButton, out removeButton);
        var workspace = BuildWorkspace();
        var dangerBar = BuildDangerBar();

        Controls.Add(workspace);
        Controls.Add(dangerBar);
        Controls.Add(commandBar);
        Controls.Add(header);

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        Shown += (_, _) =>
        {
            RefreshRealtimeMode();
            RefreshControlMode();
            RefreshThemes();
        };
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = AppVisuals.Sidebar,
            Padding = new Padding(26, 16, 26, 14)
        };
        var logo = new PictureBox
        {
            Image = AppVisuals.CreateLogoImage(),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Location = new Point(26, 18),
            Size = new Size(44, 44)
        };
        var title = AppVisuals.CreateLabel(
            "Theme Pack", 17F, Color.White, FontStyle.Bold);
        title.Location = new Point(82, 15);
        var subtitle = AppVisuals.CreateLabel(
            "本地主题包管理",
            9F,
            AppVisuals.SidebarMuted);
        subtitle.Location = new Point(84, 49);
        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        statusLabel.Font = AppVisuals.UiFont(9F, FontStyle.Bold);
        statusLabel.ForeColor = AppVisuals.Success;
        statusLabel.Location = new Point(800, 31);
        header.Controls.AddRange(new Control[] { logo, title, subtitle, statusLabel });
        header.Resize += (_, _) =>
            statusLabel.Left = header.ClientSize.Width - statusLabel.Width - 28;
        return header;
    }

    private Control BuildCommandBar(
        out Button export,
        out Button activate,
        out Button remove)
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = AppVisuals.Surface,
            Padding = new Padding(0)
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppVisuals.Surface,
            Padding = new Padding(22, 14, 22, 10)
        };

        var import = AppVisuals.CreateButton("导入 Theme Pack", primary: true);
        import.Size = new Size(132, 36);
        import.Click += (_, _) => ImportFromDialog();

        export = AppVisuals.CreateButton("导出");
        export.Size = new Size(90, 36);
        export.Click += (_, _) => ExportSelected();

        activate = AppVisuals.CreateButton("设为当前");
        activate.Size = new Size(104, 36);
        activate.Click += (_, _) => ActivateSelected();

        remove = AppVisuals.CreateButton("移除");
        remove.Size = new Size(88, 36);
        remove.Click += (_, _) => RemoveSelected();

        var openFolder = AppVisuals.CreateButton("打开主题目录");
        openFolder.Size = new Size(118, 36);
        openFolder.Click += (_, _) =>
        {
            var path = PathRules.GetSafeCombinedPath(
                gameRoot, "ThemeLoader/themes");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        };

        realtimeToggle.Text = "实时模式";
        realtimeToggle.AutoSize = true;
        realtimeToggle.Font = AppVisuals.UiFont(9.25F, FontStyle.Bold);
        realtimeToggle.ForeColor = AppVisuals.Ink;
        realtimeToggle.BackColor = AppVisuals.Surface;
        realtimeToggle.Cursor = Cursors.Hand;
        realtimeToggle.Margin = new Padding(14, 9, 8, 0);
        realtimeToggle.CheckedChanged += (_, _) => ToggleRealtimeMode();

        controlModeToggle.Text = "控制模式";
        controlModeToggle.AutoSize = true;
        controlModeToggle.Font = AppVisuals.UiFont(9.25F, FontStyle.Bold);
        controlModeToggle.ForeColor = AppVisuals.Ink;
        controlModeToggle.BackColor = AppVisuals.Surface;
        controlModeToggle.Cursor = Cursors.Hand;
        controlModeToggle.Margin = new Padding(8, 9, 0, 0);
        controlModeToggle.CheckedChanged += (_, _) => ToggleControlMode();

        controlBackgroundButton.Size = new Size(94, 36);
        controlBackgroundButton.Visible = false;
        controlBackgroundButton.Click += (_, _) => SetControlBackgroundImage();

        foreach (var button in new[]
                 {
                     import,
                     export,
                     activate,
                     remove,
                     openFolder
                 })
        {
            button.Margin = new Padding(0, 0, 10, 0);
            actions.Controls.Add(button);
        }

        actions.Controls.Add(realtimeToggle);
        actions.Controls.Add(controlModeToggle);
        controlBackgroundButton.Margin = new Padding(10, 0, 10, 0);
        actions.Controls.Add(controlBackgroundButton);
        bar.Controls.Add(actions);
        bar.Controls.Add(new Separator());
        return bar;
    }

    private Control BuildWorkspace()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 310,
            FixedPanel = FixedPanel.Panel1,
            BackColor = AppVisuals.Line,
            BorderStyle = BorderStyle.None
        };

        split.Panel1.BackColor = AppVisuals.Surface;
        split.Panel1.Padding = new Padding(18, 16, 14, 14);
        var listTitle = AppVisuals.CreateLabel(
            "已安装主题", 10F, AppVisuals.Ink, FontStyle.Bold);
        listTitle.Dock = DockStyle.Top;
        listTitle.Height = 30;

        themeList.Dock = DockStyle.Fill;
        themeList.View = View.Details;
        themeList.FullRowSelect = true;
        themeList.HideSelection = false;
        themeList.MultiSelect = false;
        themeList.BorderStyle = BorderStyle.None;
        themeList.BackColor = AppVisuals.Surface;
        themeList.ForeColor = AppVisuals.Ink;
        themeList.Font = AppVisuals.UiFont(9.5F);
        themeList.Columns.Add("主题", 175);
        themeList.Columns.Add("版本", 82);
        themeList.SelectedIndexChanged += (_, _) => ShowSelection();
        themeList.DoubleClick += (_, _) => ActivateSelected();
        split.Panel1.Controls.Add(themeList);
        split.Panel1.Controls.Add(listTitle);

        split.Panel2.BackColor = AppVisuals.Canvas;
        split.Panel2.Padding = new Padding(24, 16, 22, 18);
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = AppVisuals.UiFont(9F, FontStyle.Bold)
        };
        tabs.TabPages.Add(BuildOverviewPage());
        tabs.TabPages.Add(BuildJsonPage());
        split.Panel2.Controls.Add(tabs);

        return split;
    }

    private TabPage BuildOverviewPage()
    {
        var page = new TabPage("概览")
        {
            BackColor = AppVisuals.Surface,
            Padding = new Padding(24, 22, 24, 20)
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 7,
            BackColor = AppVisuals.Surface
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDetailRow(table, 0, "名称", nameValue);
        AddDetailRow(table, 1, "版本", versionValue);
        AddDetailRow(table, 2, "作者", authorValue);
        AddDetailRow(table, 3, "游戏版本", gameVersionValue);
        AddDetailRow(table, 4, "包内容", sizeValue);
        AddDetailRow(table, 5, "状态", activeValue);
        AddDetailRow(table, 6, "说明", descriptionValue, 92);
        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildJsonPage()
    {
        var page = new TabPage("theme.json")
        {
            BackColor = AppVisuals.Surface,
            Padding = new Padding(12)
        };
        jsonBox.Dock = DockStyle.Fill;
        jsonBox.Multiline = true;
        jsonBox.ReadOnly = true;
        jsonBox.ScrollBars = ScrollBars.Both;
        jsonBox.WordWrap = false;
        jsonBox.BorderStyle = BorderStyle.None;
        jsonBox.BackColor = Color.FromArgb(248, 249, 250);
        jsonBox.ForeColor = AppVisuals.Ink;
        jsonBox.Font = AppVisuals.MonoFont(9F);
        page.Controls.Add(jsonBox);
        return page;
    }

    private Control BuildDangerBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = AppVisuals.Surface,
            Padding = new Padding(22, 17, 22, 17)
        };
        bar.Controls.Add(new Separator());
        var note = AppVisuals.CreateLabel(
            "高风险操作不会启动游戏，也不会删除安装清单之外的文件。",
            9F,
            AppVisuals.MutedInk);
        note.Location = new Point(22, 28);

        var clear = AppVisuals.CreateButton("清空导入主题");
        clear.Size = new Size(126, 36);
        clear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clear.Click += (_, _) => RemoveAllImported();

        var uninstall = AppVisuals.CreateButton("完整卸载", danger: true);
        uninstall.Size = new Size(108, 36);
        uninstall.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        uninstall.Click += (_, _) => CompleteUninstall();

        bar.Controls.AddRange(new Control[] { note, clear, uninstall });
        void PlaceButtons()
        {
            uninstall.Location = new Point(bar.ClientSize.Width - 130, 18);
            clear.Location = new Point(uninstall.Left - 138, 18);
        }
        bar.Resize += (_, _) => PlaceButtons();
        PlaceButtons();
        return bar;
    }

    private void RefreshThemes(string? selectKey = null)
    {
        var active = library.GetActiveTheme();
        var items = library.GetThemes();
        themeList.BeginUpdate();
        themeList.Items.Clear();
        foreach (var item in items)
        {
            var activeMark = string.Equals(
                active, item.Key, StringComparison.OrdinalIgnoreCase)
                ? "● "
                : "";
            var listItem = new ListViewItem(activeMark + item.DisplayName)
            {
                Tag = item,
                ForeColor = item.IsValid ? AppVisuals.Ink : AppVisuals.Danger
            };
            listItem.SubItems.Add(item.Info?.Manifest.Version ?? "无效");
            themeList.Items.Add(listItem);
            if (string.Equals(
                    item.Key, selectKey, StringComparison.OrdinalIgnoreCase))
            {
                listItem.Selected = true;
            }
        }
        themeList.EndUpdate();

        statusLabel.Text = $"{items.Count} 个主题  ·  当前：{active}";
        if (themeList.SelectedItems.Count == 0 && themeList.Items.Count > 0)
        {
            themeList.Items[0].Selected = true;
        }
        UpdateCommandState();
    }

    private void ShowSelection()
    {
        var item = SelectedTheme;
        if (item?.Info == null)
        {
            nameValue.Text = item?.DisplayName ?? "未选择";
            versionValue.Text = "—";
            authorValue.Text = "—";
            gameVersionValue.Text = "—";
            sizeValue.Text = "—";
            activeValue.Text = item?.Error ?? "—";
            activeValue.ForeColor = AppVisuals.Danger;
            descriptionValue.Text = "主题包未通过校验。";
            jsonBox.Text = item?.Error ?? "";
            UpdateCommandState();
            return;
        }

        var manifest = item.Info.Manifest;
        nameValue.Text = manifest.Name;
        versionValue.Text = manifest.Version;
        authorValue.Text = string.IsNullOrWhiteSpace(manifest.Author)
            ? "未填写"
            : manifest.Author;
        gameVersionValue.Text = string.IsNullOrWhiteSpace(manifest.GameVersion)
            ? "未填写"
            : manifest.GameVersion;
        sizeValue.Text =
            $"{item.Info.FileCount:N0} 个文件  ·  {FormatBytes(item.Info.ExpandedSize)}";
        var active = string.Equals(
            library.GetActiveTheme(), item.Key, StringComparison.OrdinalIgnoreCase);
        activeValue.Text = active
            ? "当前使用"
            : item.IsVirtual
                ? "游戏原始外观"
            : item.IsLegacyDirectory
                ? "旧式目录，可导出为 Theme Pack"
                : "已安装";
        activeValue.ForeColor = active ? AppVisuals.Accent : AppVisuals.Ink;
        descriptionValue.Text = string.IsNullOrWhiteSpace(manifest.Description)
            ? "未填写说明"
            : manifest.Description;
        jsonBox.Text = item.Info.RawManifestJson;
        UpdateCommandState();
    }

    private void ImportFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入 Theme Pack",
            Filter = "Theme Pack (*.mtp)|*.mtp",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            ImportPackage(path);
        }
    }

    private void ImportPackage(string path)
    {
        try
        {
            ThemeItem imported;
            try
            {
                imported = library.Import(path, false);
            }
            catch (ThemeAlreadyExistsException exception)
            {
                var overwrite = MessageBox.Show(
                    this,
                    exception.Message + Environment.NewLine + "是否覆盖现有主题包？",
                    "导入主题",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (overwrite != DialogResult.Yes)
                {
                    return;
                }
                imported = library.Import(path, true);
            }

            RefreshThemes(imported.Key);
            ShowStatus($"已导入“{imported.DisplayName}”。");
        }
        catch (Exception exception)
        {
            ShowError($"导入失败：{exception.Message}");
        }
    }

    private void ExportSelected()
    {
        var item = SelectedTheme;
        if (item?.Info == null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 Theme Pack",
            Filter = "Theme Pack (*.mtp)|*.mtp",
            FileName = item.Info.Manifest.Id + ThemePackage.Extension,
            AddExtension = true,
            DefaultExt = "mtp"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            library.Export(item, dialog.FileName);
            ShowStatus($"已导出到 {dialog.FileName}");
        }
        catch (Exception exception)
        {
            ShowError($"导出失败：{exception.Message}");
        }
    }

    private void ActivateSelected()
    {
        var item = SelectedTheme;
        if (item?.Info == null)
        {
            return;
        }

        try
        {
            library.Activate(item);
            RefreshThemes(item.Key);
            ShowStatus(library.GetSettings().RealtimeMode
                ? $"“{item.DisplayName}”已设为当前；游戏运行中会尝试热重载。"
                : $"“{item.DisplayName}”将在下次启动游戏时生效。");
        }
        catch (Exception exception)
        {
            ShowError($"无法启用主题：{exception.Message}");
        }
    }

    private void RemoveSelected()
    {
        var item = SelectedTheme;
        if (item == null || item.IsDefault)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"确认移除“{item.DisplayName}”？",
                "移除主题",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            library.Remove(item);
            RefreshThemes();
            ShowStatus("主题已移除，当前主题已在必要时恢复为 Default。");
        }
        catch (Exception exception)
        {
            ShowError($"移除失败：{exception.Message}");
        }
    }

    private void RemoveAllImported()
    {
        if (MessageBox.Show(
                this,
                "这会移除所有导入主题并恢复 Default。是否继续？",
                "清空导入主题",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var count = library.RemoveAllImported();
            RefreshThemes("Default");
            ShowStatus($"已移除 {count} 个导入主题。");
        }
        catch (Exception exception)
        {
            ShowError($"清理失败：{exception.Message}");
        }
    }

    private void CompleteUninstall()
    {
        if (MessageBox.Show(
                this,
                "这会删除全部主题，并按安装清单卸载 ThemeLoader、桌面快捷方式和管理器。"
                + Environment.NewLine
                + "被修改或不再归安装器所有的文件会保留。是否继续？",
                "完整卸载",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Stop) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            library.RemoveEveryThemeForUninstall();
            var message = UninstallCoordinator.Uninstall(
                gameRoot, new Progress<string>(ShowStatus));
            MessageBox.Show(
                this,
                message,
                "完整卸载",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            BeginInvoke(Close);
        }
        catch (Exception exception)
        {
            ShowError($"卸载失败：{exception.Message}");
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetDataPresent(DataFormats.FileDrop) == true
            && eventArgs.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.All(file => string.Equals(
                Path.GetExtension(file),
                ThemePackage.Extension,
                StringComparison.OrdinalIgnoreCase)))
        {
            eventArgs.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }
        foreach (var file in files)
        {
            ImportPackage(file);
        }
    }

    private void UpdateCommandState()
    {
        var item = SelectedTheme;
        var valid = item?.IsValid == true;
        exportButton.Enabled = valid && item?.IsVirtual == false;
        activateButton.Enabled = valid;
        removeButton.Enabled = valid && item?.IsDefault == false;
    }

    private void RefreshRealtimeMode()
    {
        updatingRealtimeToggle = true;
        try
        {
            realtimeToggle.Checked = library.GetSettings().RealtimeMode;
        }
        finally
        {
            updatingRealtimeToggle = false;
        }
    }

    private void RefreshControlMode()
    {
        updatingControlModeToggle = true;
        try
        {
            controlModeToggle.Checked = library.GetSettings().ControlMode;
            controlBackgroundButton.Visible = controlModeToggle.Checked;
        }
        finally
        {
            updatingControlModeToggle = false;
        }
    }

    private void ToggleRealtimeMode()
    {
        if (updatingRealtimeToggle)
        {
            return;
        }

        try
        {
            library.SetRealtimeMode(realtimeToggle.Checked);
            ShowStatus(realtimeToggle.Checked
                ? "实时模式已启用；运行中的游戏会自动接入热重载。"
                : "实时模式已关闭；主题变更将在下次启动游戏时生效。");
        }
        catch (Exception exception)
        {
            RefreshRealtimeMode();
            ShowError($"无法修改实时模式：{exception.Message}");
        }
    }

    private void ToggleControlMode()
    {
        if (updatingControlModeToggle)
        {
            return;
        }

        try
        {
            library.SetControlMode(controlModeToggle.Checked);
            controlBackgroundButton.Visible = controlModeToggle.Checked;
            ShowStatus(controlModeToggle.Checked
                ? "控制模式已启用；按住 Ctrl 后用 WASD、鼠标和滚轮控制相机。"
                : "控制模式已关闭；游戏会恢复原始完整画面。");
        }
        catch (Exception exception)
        {
            RefreshControlMode();
            ShowError($"无法修改控制模式：{exception.Message}");
        }
    }

    private void SetControlBackgroundImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "设置控制模式底图",
            Filter = "图像文件 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var target = library.SetControlBackgroundImage(dialog.FileName);
            ShowStatus($"控制模式底图已更新：{target}");
        }
        catch (Exception exception)
        {
            ShowError($"无法设置控制模式底图：{exception.Message}");
        }
    }

    private ThemeItem? SelectedTheme =>
        themeList.SelectedItems.Count == 0
            ? null
            : themeList.SelectedItems[0].Tag as ThemeItem;

    private void ShowStatus(string message)
    {
        statusLabel.Text = message;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            this,
            message,
            "主题管理器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = AppVisuals.UiFont(9.5F),
            ForeColor = AppVisuals.Ink,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static void AddDetailRow(
        TableLayoutPanel table,
        int row,
        string label,
        Control value,
        int height = 42)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var key = AppVisuals.CreateLabel(
            label, 9F, AppVisuals.MutedInk, FontStyle.Bold);
        key.Dock = DockStyle.Fill;
        key.TextAlign = ContentAlignment.MiddleLeft;
        value.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(key, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KiB", "MiB", "GiB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString(
                   value >= 100 ? "N0" : "N1",
                   CultureInfo.CurrentCulture)
               + " "
               + units[unit];
    }
}
