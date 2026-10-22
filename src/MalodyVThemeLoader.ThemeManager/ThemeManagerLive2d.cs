using System.Diagnostics;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeManagerForm
{
    private ComboBox live2dTheme = null!;
    private ComboBox live2dIdle = null!;
    private ComboBox live2dTap = null!;
    private NumberField live2dScale = null!;
    private NumberField live2dX = null!;
    private NumberField live2dY = null!;
    private Button live2dApply = null!;
    private Button live2dRestore = null!;
    private Label live2dModelState = null!;
    private bool refreshingLive2d;
    private bool loadingLive2dSelection;
    private bool live2dDirty;
    private Live2dBinding? live2dEditingBinding;
    private Button live2dEditor = null!;

    private Control BuildLive2dPage()
    {
        var page = new TableLayoutPanel
        {
            Padding = new Padding(32, 24, 32, 20), ColumnCount = 1, RowCount = 2,
            Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var stack = AppVisuals.Stack();
        scroll.Layout += (_, _) =>
        {
            // AutoSize tables can retain the pre-clamp width after a DPI/size
            // change. Constrain measurement to the real scroll viewport.
            var width = Math.Max(1, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            if (stack.MaximumSize.Width == width) return;
            stack.MinimumSize = new Size(width, 0);
            stack.MaximumSize = new Size(width, 0);
        };
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("Live2D 工作台", 22F, true));
        live2dTheme = Choice("编辑主题");
        live2dTheme.DisplayMember = "DisplayName";
        live2dTheme.SelectedIndexChanged += (_, _) =>
        {
            if (refreshingLive2d || live2dTheme.SelectedItem is not ThemeItem item) return;
            searchBox.Clear();
            for (var i = 0; i < themeList.Items.Count; i++)
                if (themeList.Items[i] is ThemeItem theme && theme.Key == item.Key) { themeList.SelectedIndex = i; break; }
        };
        AppVisuals.AddRow(stack, Field("编辑主题", live2dTheme));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 8, 0, 0) };
        live2dImportDirectory = AppVisuals.CreateButton("导入模型目录");
        live2dImportArchive = AppVisuals.CreateButton("导入模型 ZIP");
        live2dImportAnimation = AppVisuals.CreateButton("导入动画");
        live2dImportTexture = AppVisuals.CreateButton("替换纹理");
        foreach (var button in new[] { live2dImportDirectory, live2dImportArchive, live2dImportAnimation, live2dImportTexture })
        {
            button.AutoSize = true; button.MinimumSize = new Size(120, 36); button.Margin = new Padding(0, 0, 8, 8);
            actions.Controls.Add(button);
        }
        live2dImportDirectory.Click += (_, _) => ImportLive2dDirectory();
        live2dImportArchive.Click += (_, _) => ImportLive2dArchive();
        live2dImportAnimation.Click += (_, _) => ImportLive2dAnimations();
        live2dImportTexture.Click += (_, _) => ImportLive2dTexture();
        AppVisuals.AddRow(stack, actions);
        var lists = new TableLayoutPanel { Dock = DockStyle.Top, Height = 154, ColumnCount = 2, Margin = new Padding(0, 4, 0, 8) };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        live2dModels = ModelList("模型列表"); live2dModels.FormattingEnabled = true;
        live2dModels.Format += (_, e) =>
        {
            if (e.ListItem is string path) e.Value = path.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(path).Replace(".model3.json", "", StringComparison.OrdinalIgnoreCase) : path + "（旧版）";
        };
        live2dModels.SelectedIndexChanged += (_, _) => RefreshLive2dAnimations();
        live2dAnimations = ModelList("当前模型的动画文件");
        lists.Controls.Add(ListField("选择模型", live2dModels), 0, 0);
        lists.Controls.Add(ListField("模型动画", live2dAnimations), 1, 0);
        AppVisuals.AddRow(stack, lists);
        live2dModelState = AppVisuals.TextBlock("导入模型后，选择动画并应用。", 9F);
        AppVisuals.AddRow(stack, live2dModelState);
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("替换游戏当前显示的看板角色", 11F, true));
        live2dIdle = Choice("待机动画"); live2dTap = Choice("点击动画");
        var motionFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Margin = new Padding(0, 4, 0, 0) };
        motionFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); motionFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        motionFields.Controls.Add(Field("待机动画 · 循环", live2dIdle), 0, 0);
        motionFields.Controls.Add(Field("点击动画 · 播放后回到待机", live2dTap), 1, 0);
        AppVisuals.AddRow(stack, motionFields);
        live2dEditor = AppVisuals.CreateButton("预览与绑定动画"); live2dEditor.AutoSize = true; live2dEditor.MinimumSize = new Size(180, 38);
        live2dEditor.Margin = new Padding(0, 8, 0, 8); live2dEditor.Click += (_, _) => EditLive2dAnimations();
        live2dEditor.Enter += (_, _) => scroll.ScrollControlIntoView(live2dEditor);
        AppVisuals.AddRow(stack, live2dEditor);
        live2dScale = Adjustment("模型大小（百分比）", 10, 300, 100);
        live2dX = Adjustment("水平位置（百分比）", -200, 200, 0);
        live2dY = Adjustment("垂直位置（百分比）", -200, 200, 0);
        var fitFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, Margin = new Padding(0, 8, 0, 8) };
        foreach (var _ in new[] { 0, 1, 2 }) fitFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        fitFields.Controls.Add(Field("大小 %", live2dScale), 0, 0);
        fitFields.Controls.Add(Field("水平位置 %", live2dX), 1, 0);
        fitFields.Controls.Add(Field("垂直位置 %", live2dY), 2, 0);
        AppVisuals.AddRow(stack, fitFields);
        scroll.Controls.Add(stack); page.Controls.Add(scroll, 0, 0);

        var footer = AppVisuals.Stack(); footer.Dock = DockStyle.Fill; footer.AutoSize = false; footer.Margin = new Padding(0, 12, 0, 0);
        var applyRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false, Margin = Padding.Empty };
        live2dApply = AppVisuals.CreateButton("保存并应用模型", primary: true);
        live2dRestore = AppVisuals.CreateButton("恢复游戏模型");
        foreach (var button in new[] { live2dApply, live2dRestore })
        {
            button.AutoSize = true; button.MinimumSize = new Size(150, 38); button.Margin = new Padding(0, 0, 8, 8);
            applyRow.Controls.Add(button);
        }
        live2dApply.Click += (_, _) => ApplyLive2dModel();
        live2dRestore.Click += (_, _) => RestoreLive2dModel();
        AppVisuals.AddRow(footer, applyRow);
        live2dState = AppVisuals.TextBlock("请先选择一个主题。", 9F);
        live2dState.AccessibleName = "模型应用状态";
        AppVisuals.AddRow(footer, live2dState); page.Controls.Add(footer, 0, 1);
        foreach (var combo in new[] { live2dIdle, live2dTap }) combo.SelectedIndexChanged += (_, _) => MarkLive2dDirty();
        foreach (var number in new[] { live2dScale, live2dX, live2dY }) number.ValueChanged += (_, _) => MarkLive2dDirty();
        foreach (var editor in new Control[] { live2dTheme, live2dIdle, live2dTap, live2dScale, live2dX, live2dY })
            editor.Enter += (_, _) => scroll.ScrollControlIntoView(editor);
        var timer = new System.Windows.Forms.Timer { Interval = 1500 };
        timer.Tick += (_, _) => { if (page.Visible && !live2dDirty) RefreshLive2dStatus(); };
        timer.Start(); FormClosed += (_, _) => timer.Dispose();
        return page;
    }

    private static ComboBox Choice(string name) => new ChoiceBox
    {
        Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat, BackColor = AppVisuals.Surface, ForeColor = AppVisuals.Ink,
        Font = AppVisuals.UiFont(10F), AccessibleName = name, IntegralHeight = false, DropDownHeight = 240
    };

    private static NumberField Adjustment(string name, int minimum, int maximum, int value) => new NumberField
    {
        Dock = DockStyle.Top, Minimum = minimum, Maximum = maximum, Value = value,
        BackColor = AppVisuals.Surface, ForeColor = AppVisuals.Ink,
        AccessibleName = name, Font = AppVisuals.UiFont(10F)
    };

    private static ListBox ModelList(string name) => new()
    {
        Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None,
        BackColor = AppVisuals.Surface, ForeColor = AppVisuals.Ink, Font = AppVisuals.UiFont(10F), AccessibleName = name
    };

    private static Control Field(string label, Control control)
    {
        var field = AppVisuals.Stack(); field.Margin = new Padding(0, 0, 12, 4);
        var heading = AppVisuals.TextBlock(label, 9F); heading.Margin = new Padding(0, 0, 0, 4);
        AppVisuals.AddRow(field, heading); AppVisuals.AddRow(field, control); return field;
    }

    private static Control ListField(string label, Control list)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12), BackColor = AppVisuals.Surface, Margin = new Padding(0, 0, 12, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(AppVisuals.TextBlock(label, 10F, true), 0, 0); panel.Controls.Add(list, 0, 1); return panel;
    }

    private void RefreshLive2d()
    {
        if (live2dModels is null) return;
        var selected = live2dModels.SelectedItem as string;
        var item = SelectedTheme;
        refreshingLive2d = true;
        try
        {
            live2dTheme.Items.Clear();
            foreach (var theme in library.GetThemes().Where(theme => theme.IsValid && !theme.IsVirtual))
            {
                var index = live2dTheme.Items.Add(theme); if (theme.Key == item?.Key) live2dTheme.SelectedIndex = index;
            }
            live2dModels.Items.Clear(); live2dAnimations?.Items.Clear();
            var editable = item is { IsValid: true, IsVirtual: false };
            live2dImportDirectory!.Enabled = editable; live2dImportArchive!.Enabled = editable;
            live2dImportTexture!.Enabled = editable; live2dImportAnimation!.Enabled = false;
            live2dApply.Enabled = false; live2dRestore.Enabled = false; live2dEditor.Enabled = false;
            if (!editable) { live2dState!.Text = "请先选择一个主题。"; return; }
            var binding = Live2dWorkspace.GetBinding(item!);
            foreach (var model in Live2dWorkspace.GetModels(item!)) live2dModels.Items.Add(model);
            var choice = binding?.Model ?? selected;
            var indexToSelect = choice == null ? -1 : live2dModels.Items.IndexOf(choice);
            if (live2dModels.Items.Count > 0) live2dModels.SelectedIndex = Math.Max(0, indexToSelect);
            live2dRestore.Enabled = binding != null;
            live2dDirty = false; RefreshLive2dStatus();
        }
        catch (Exception e) { live2dState!.Text = "读取模型失败：" + e.Message; }
        finally { refreshingLive2d = false; }
    }

    private void RefreshLive2dAnimations()
    {
        if (live2dAnimations is null) return;
        var draft = !refreshingLive2d && live2dDirty ? live2dEditingBinding : null;
        loadingLive2dSelection = true;
        try
        {
            live2dAnimations.Items.Clear(); live2dIdle.Items.Clear(); live2dTap.Items.Clear();
            live2dApply.Enabled = false; live2dImportAnimation!.Enabled = false; live2dEditor.Enabled = false;
            live2dEditingBinding = null;
            foreach (var control in new Control[] { live2dIdle, live2dTap, live2dScale, live2dX, live2dY }) control.Enabled = false;
            if (SelectedTheme is not { } item || live2dModels?.SelectedItem is not string model) return;
            foreach (var animation in Live2dWorkspace.GetAnimationPaths(item, model)) live2dAnimations.Items.Add(animation);
            live2dImportAnimation.Enabled = true;
            if (!model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            {
                live2dModelState.Text = "旧版模型 · 可替换原有纹理和动画文件。"; return;
            }
            var document = Live2dWorkspace.ReadModel(item, model);
            var motions = Live2dWorkspace.GetMotionChoices(item, model);
            live2dIdle.Items.Add(new Live2dMotionReference { Group = "不播放动画" });
            live2dTap.Items.Add(new Live2dMotionReference { Group = "不播放动画" });
            foreach (var motion in motions) { live2dIdle.Items.Add(motion); live2dTap.Items.Add(motion); }
            var binding = draft?.Model == model ? draft : Live2dWorkspace.GetBinding(item);
            var matching = binding?.Model == model;
            live2dEditingBinding = matching ? JsonSerializer.Deserialize<Live2dBinding>(JsonSerializer.Serialize(binding, JsonDefaults.Options), JsonDefaults.Options)
                : new Live2dBinding { Model = model };
            var idle = matching ? binding!.IdleMotion : "";
            var tap = matching ? binding!.TapMotion : "";
            SelectMotion(live2dIdle, idle); SelectMotion(live2dTap, tap);
            live2dScale.Value = matching ? Math.Clamp((decimal)binding!.Scale * 100, 10, 300) : 100;
            live2dX.Value = matching ? Math.Clamp((decimal)binding!.OffsetX * 100, -200, 200) : 0;
            live2dY.Value = matching ? Math.Clamp((decimal)binding!.OffsetY * 100, -200, 200) : 0;
            foreach (var control in new Control[] { live2dIdle, live2dTap, live2dScale, live2dX, live2dY }) control.Enabled = true;
            live2dApply.Enabled = true; live2dEditor.Enabled = true;
            live2dModelState.Text = $"{document.Name} · {document.Textures.Count} 张纹理 · {motions.Count} 个动画";
        }
        catch (Exception e) { live2dModelState.Text = e.Message; }
        finally { loadingLive2dSelection = false; }
        if (!refreshingLive2d) MarkLive2dDirty();
    }

    private static void SelectMotion(ComboBox combo, string? file)
    {
        var index = combo.Items.Cast<Live2dMotionReference>().ToList().FindIndex(motion => motion.File == file);
        combo.SelectedIndex = Math.Max(0, index);
    }

    private void MarkLive2dDirty()
    {
        if (refreshingLive2d || loadingLive2dSelection || !live2dApply.Enabled) return;
        if (live2dModels?.SelectedItem is string model) live2dEditingBinding = CurrentLive2dBinding(model);
        live2dDirty = true; live2dState!.Text = "尚未应用 · 确认动画后，点击“保存并应用模型”。";
    }

    private void ApplyLive2dModel()
    {
        if (SelectedTheme is not { } item || live2dModels?.SelectedItem is not string model) return;
        try
        {
            Live2dWorkspace.SaveBinding(item, CurrentLive2dBinding(model));
            library.SetRealtimeMode(true); library.Activate(item); RefreshThemes(item.Key); RefreshRealtimeMode();
            live2dDirty = false; RefreshLive2dStatus(); ShowStatus($"已保存 {Path.GetFileName(model)}，并启用主题“{item.DisplayName}”。");
        }
        catch (Exception e) { ShowError("应用模型失败：" + e.Message); }
    }

    private Live2dBinding CurrentLive2dBinding(string model)
    {
        var binding = live2dEditingBinding ?? new Live2dBinding { Model = model };
        binding.Model = model; binding.IdleMotion = (live2dIdle.SelectedItem as Live2dMotionReference)?.File ?? "";
        binding.TapMotion = (live2dTap.SelectedItem as Live2dMotionReference)?.File ?? "";
        binding.Scale = (float)live2dScale.Value / 100; binding.OffsetX = (float)live2dX.Value / 100; binding.OffsetY = (float)live2dY.Value / 100;
        return binding;
    }

    private void EditLive2dAnimations()
    {
        if (SelectedTheme is not { } item || live2dModels?.SelectedItem is not string model) return;
        try
        {
            var binding = JsonSerializer.Deserialize<Live2dBinding>(JsonSerializer.Serialize(CurrentLive2dBinding(model), JsonDefaults.Options), JsonDefaults.Options)!;
            var root = PathRules.GetSafeCombinedPath(Live2dWorkspace.EnsureEditableDirectory(item), item.Info!.Manifest.Live2dRoot);
            using var editor = new Live2dAnimationEditor(gameRoot, root, binding, Live2dWorkspace.GetMotionChoices(item, model));
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            live2dEditingBinding = editor.Binding; loadingLive2dSelection = true;
            SelectMotion(live2dIdle, editor.Binding.IdleMotion); SelectMotion(live2dTap, editor.Binding.TapMotion);
            loadingLive2dSelection = false; MarkLive2dDirty(); ShowStatus("动画绑定已保留，点击“保存并应用模型”使其生效。");
        }
        catch (Exception e) { ShowError("无法打开模型预览：" + e.Message); }
        finally { loadingLive2dSelection = false; }
    }

    private void RestoreLive2dModel()
    {
        if (SelectedTheme is not { } item) return;
        try
        {
            Live2dWorkspace.RemoveBinding(item); live2dDirty = false; RefreshLive2d();
            ShowStatus("已移除模型替换，原模型将在实时重载后恢复。");
        }
        catch (Exception e) { ShowError("恢复模型失败：" + e.Message); }
    }

    private void RefreshLive2dStatus()
    {
        if (live2dState == null || SelectedTheme is not { IsValid: true, IsVirtual: false } item) return;
        try
        {
            var binding = Live2dWorkspace.GetBinding(item);
            if (binding == null) { live2dState.Text = "选择模型和动画，然后点击“保存并应用模型”。"; return; }
            var plugin = Path.Combine(gameRoot, "BepInEx", "plugins", "MalodyVThemeLoader", "MalodyVThemeLoader.Plugin.dll");
            var required = binding.ClickRegions.Count > 0 || binding.ResultMotions.Values.Any(value => value.Length > 0)
                || binding.MotionAudio.Count > 0 ? new Version(2, 4, 0) : new Version(2, 3, 0);
            if (!File.Exists(plugin) || new Version(FileVersionInfo.GetVersionInfo(plugin).FileVersion ?? "0.0") < required)
            { live2dState.Text = $"配置已保存 · 请安装 {required.ToString(3)} 或更新版加载器，再重新启动游戏一次。"; return; }
            var path = Path.Combine(gameRoot, "ThemeLoader", "live2d-status.json");
            var bindingTime = File.GetLastWriteTimeUtc(Path.Combine(Live2dWorkspace.EnsureEditableDirectory(item), item.Info!.Manifest.Live2dRoot, Live2dModelDocument.BindingFile));
            var status = File.Exists(path) ? JsonSerializer.Deserialize<Live2dLoadStatus>(File.ReadAllText(path), JsonDefaults.Options) : null;
            if (status is not null && status.Theme == item.Info!.Manifest.Id && status.Model == binding.Model
                && status.UpdatedAt.UtcDateTime >= bindingTime && Version.TryParse(status.PluginVersion, out var runningVersion) && runningVersion < required)
            { live2dState.Text = $"配置已保存 · 请重新启动游戏，加载 {required.ToString(3)} 或更新版插件。"; return; }
            live2dState.Text = status is not null && status.Theme == item.Info!.Manifest.Id && status.Model == binding.Model
                && status.UpdatedAt.UtcDateTime >= bindingTime
                ? status.State == "error" ? "加载失败 · " + status.Message : status.Message
                : "配置已保存 · 等待游戏加载，请进入显示看板角色的界面。";
        }
        catch (IOException) { /* Game may be atomically writing the status. Retry next tick. */ }
        catch (Exception e) { live2dState.Text = "读取应用状态失败：" + e.Message; }
    }

    private void ImportLive2dDirectory()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) return;
        using var dialog = new FolderBrowserDialog { Description = "选择包含 .model3.json、moc3 和纹理的模型目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var model = Live2dWorkspace.ImportDirectory(item, dialog.SelectedPath); RefreshThemes(item.Key); SelectImportedModel(model); ShowStatus("模型和动画已导入，请选择后保存并应用。"); }
        catch (Exception e) { ShowError("导入模型失败：" + e.Message); }
    }

    private void ImportLive2dArchive()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) return;
        using var dialog = new OpenFileDialog { Title = "导入模型 ZIP", Filter = "ZIP 文件 (*.zip)|*.zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var model = Live2dWorkspace.ImportArchive(item, dialog.FileName); RefreshThemes(item.Key); SelectImportedModel(model); ShowStatus("模型和动画已导入，请选择后保存并应用。"); }
        catch (Exception e) { ShowError("导入模型失败：" + e.Message); }
    }

    private void ImportLive2dAnimations()
    {
        if (SelectedTheme is not { } item || live2dModels?.SelectedItem is not string model) return;
        using var dialog = new OpenFileDialog { Title = "添加或替换模型动画", Filter = model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase)
            ? "Cubism 动画 (*.motion3.json)|*.motion3.json" : "旧版动画 (*.mtn;*.mtn.bytes)|*.mtn;*.mtn.bytes", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Live2dWorkspace.ImportAnimations(item, model, dialog.FileNames); RefreshLive2dAnimations(); ShowStatus("动画已导入，请在待机或点击动画中选择并保存。"); }
        catch (Exception e) { ShowError("导入动画失败：" + e.Message); }
    }

    private void SelectImportedModel(string model)
    {
        if (live2dModels != null && live2dModels.Items.Contains(model)) live2dModels.SelectedItem = model;
    }

    private void ImportLive2dTexture()
    {
        if (SelectedTheme is not { } item || item.IsVirtual) return;
        using var dialog = new OpenFileDialog { Title = "替换纹理", Filter = "静态图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|GIF 首帧 (*.gif)|*.gif", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var choices = Live2dWorkspace.GetTexturePaths(item);
        if (choices.Count == 0) { ShowError("当前主题没有可替换的纹理。"); return; }
        using var picker = new Form { Text = "选择要替换的纹理", ClientSize = new Size(560, 150), Padding = new Padding(16), StartPosition = FormStartPosition.CenterParent, Font = AppVisuals.UiFont() };
        var combo = Choice("要替换的纹理"); combo.DataSource = choices.ToList();
        var ok = AppVisuals.CreateButton("替换", primary: true); ok.Dock = DockStyle.Bottom; ok.Height = 36; ok.DialogResult = DialogResult.OK;
        picker.Controls.Add(combo); picker.Controls.Add(ok); picker.AcceptButton = ok; AppAppearance.Initialize(picker);
        if (picker.ShowDialog(this) != DialogResult.OK || combo.SelectedItem is not string target) return;
        try { Live2dWorkspace.ImportImageOrGif(item, dialog.FileName, target); ShowStatus("纹理已替换。"); }
        catch (Exception e) { ShowError("替换纹理失败：" + e.Message); }
    }
}
