using System.Globalization;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeManagerForm : Form
{
    private readonly string gameRoot;
    private readonly ThemeLibrary library;
    private readonly ListBox themeList = new();
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
    private readonly Button controlTopButton =
        AppVisuals.CreateButton("设置顶图");
    private readonly Button exportButton;
    private readonly Button activateButton;
    private readonly Button removeButton;
    private bool updatingRealtimeToggle;
    private bool updatingControlModeToggle;

    private void RefreshThemes(string? selectKey = null)
    {
        var active = library.GetActiveTheme();
        var items = library.GetThemes();
        themeList.BeginUpdate();
        themeList.Items.Clear();
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(searchBox.Text)
                && !(item.DisplayName + " " + item.Info?.Manifest.Author + " " + item.Key).Contains(searchBox.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)) continue;
            var index = themeList.Items.Add(item);
            if (string.Equals(item.Key, selectKey, StringComparison.OrdinalIgnoreCase))
                themeList.SelectedIndex = index;
        }
        themeList.EndUpdate();

        listState.Text = $"{themeList.Items.Count} 个主题";
        ShowStatus($"当前主题：{active}。支持拖入 ZIP 主题包。文本框中可搜索名称和作者。");
        if (themeList.SelectedIndex < 0 && themeList.Items.Count > 0)
        {
            var activeIndex = themeList.Items.Cast<ThemeItem>().ToList()
                .FindIndex(item => string.Equals(item.Key, active, StringComparison.OrdinalIgnoreCase));
            themeList.SelectedIndex = Math.Max(0, activeIndex);
        }
        ShowSelection();
    }

    private void ShowSelection()
    {
        var item = SelectedTheme;
        SetPreview(item);
        RefreshLive2d();
        if (item?.Info == null)
        {
            nameValue.Text = item?.DisplayName ?? "没有匹配的主题";
            versionValue.Text = "—";
            authorValue.Text = "—";
            gameVersionValue.Text = "—";
            sizeValue.Text = "—";
            activeValue.Text = item?.Error ?? "";
            activeValue.ForeColor = AppVisuals.Danger;
            descriptionValue.Text = item == null ? "试试其他搜索词，或导入一个 ZIP 主题包。" : "主题包未通过校验，请检查 theme.json。";
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
                ? "目录主题，可实时编辑并导出 ZIP"
                : "已安装";
        activeValue.ForeColor = active ? AppVisuals.Accent : AppVisuals.Ink;
        descriptionValue.Text = string.IsNullOrWhiteSpace(manifest.Description)
            ? "未填写说明"
            : manifest.Description;
        jsonBox.Text = item.Info.RawManifestJson;
        UpdateCommandState();
    }

    private async void ImportFromDialog()
    {
        if (themeOperationBusy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "导入主题包",
            Filter = "ZIP 主题包 (*.zip)|*.zip|旧版主题包 (*.mtp)|*.mtp",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            await ImportPackageAsync(path);
        }
    }

    private async Task ImportPackageAsync(string path)
    {
        if (themeOperationBusy) return;
        SetThemeBusy(true, "正在导入主题…");
        try
        {
            ThemeItem imported;
            try
            {
                imported = await Task.Run(() => library.Import(path, false));
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
                imported = await Task.Run(() => library.Import(path, true));
            }

            RefreshThemes(imported.Key);
            ShowStatus($"已导入“{imported.DisplayName}”。");
        }
        catch (Exception exception)
        {
            ShowError($"导入失败：{exception.Message}");
        }
        finally { SetThemeBusy(false); }
    }

    private async void ExportSelected()
    {
        if (themeOperationBusy) return;
        var item = SelectedTheme;
        if (item?.Info == null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 ZIP 主题包",
            Filter = "ZIP 主题包 (*.zip)|*.zip",
            FileName = item.Info.Manifest.Id + ThemePackage.Extension,
            AddExtension = true,
            DefaultExt = "zip"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SetThemeBusy(true, "正在导出 ZIP…");
            var destination = dialog.FileName;
            await Task.Run(() => library.Export(item, destination));
            ShowStatus($"已导出到 {dialog.FileName}");
        }
        catch (Exception exception)
        {
            ShowError($"导出失败：{exception.Message}");
        }
        finally { SetThemeBusy(false); }
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
        if (item == null || item.IsVirtual)
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
                "按安装清单卸载 ThemeLoader、桌面快捷方式和管理器？"
                + Environment.NewLine
                + "自定义主题、修改过的文件和原有 BepInEx 会保留。",
                "卸载 ThemeLoader",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Stop) != DialogResult.Yes)
        {
            return;
        }

        try
        {
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
        if (!themeOperationBusy && eventArgs.Data?.GetDataPresent(DataFormats.FileDrop) == true
            && eventArgs.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.All(ThemePackage.IsPackagePath))
        {
            eventArgs.Effect = DragDropEffects.Copy;
        }
    }

    private async void OnDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }
        foreach (var file in files)
        {
            await ImportPackageAsync(file);
        }
    }

    private void UpdateCommandState()
    {
        var item = SelectedTheme;
        var valid = !themeOperationBusy && item?.IsValid == true;
        emptyHint.Visible = item is null;
        if (metadataPanel is not null) metadataPanel.Visible = item?.Info is not null;
        manifestButton.Enabled = valid;
        exportButton.Enabled = valid;
        activateButton.Enabled = valid && !string.Equals(item?.Key, library.GetActiveTheme(), StringComparison.OrdinalIgnoreCase);
        activateButton.Text = valid && !activateButton.Enabled ? "正在使用" : "应用主题";
        removeButton.Enabled = valid && item?.IsVirtual == false;
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
            controlBackgroundButton.Enabled = controlModeToggle.Checked;
            controlTopButton.Enabled = controlModeToggle.Checked;
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
            controlBackgroundButton.Enabled = controlModeToggle.Checked;
            controlTopButton.Enabled = controlModeToggle.Checked;
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

    private void SetControlTopImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "设置控制模式顶图",
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
            var target = library.SetControlTopImage(dialog.FileName);
            ShowStatus($"控制模式顶图已更新：{target}");
        }
        catch (Exception exception)
        {
            ShowError($"无法设置控制模式顶图：{exception.Message}");
        }
    }

    private ThemeItem? SelectedTheme => themeList.SelectedItem as ThemeItem;

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
            AutoEllipsis = true,
            UseMnemonic = false
        };
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
