using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class Live2dAnimationEditor : Form
{
    public Live2dBinding Binding { get; }
    private readonly Live2dPreviewSurface preview;
    private readonly ListBox rules = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly TextBox regionName = new() { Dock = DockStyle.Top, MaxLength = 60 };
    private readonly ChoiceBox assignedMotion = new() { Dock = DockStyle.Top, DropDownHeight = 250, IntegralHeight = false };
    private readonly ChoiceBox browseMotion = new() { Dock = DockStyle.Fill, DropDownHeight = 250, IntegralHeight = false };
    private readonly Label state = AppVisuals.TextBlock("先试播动作，再选择触发项绑定。", 9F);
    private readonly Button delete = AppVisuals.CreateButton("删除区域");
    private bool selecting;
    private readonly string live2dRoot;
    private readonly Label audioName = AppVisuals.TextBlock("未绑定音频", 9F);
    private readonly Button audioImport = AppVisuals.CreateButton("添加音频");
    private readonly Button audioRemove = AppVisuals.CreateButton("移除音频");
    private static readonly string[] BasicRules = { "待机 · 循环", "点击未绑定区域", "M1 结算", "M2 结算", "M3 结算", "M4 结算", "M5 结算" };

    public Live2dAnimationEditor(string gameRoot, string root, Live2dBinding binding, IReadOnlyList<Live2dMotionReference> motions)
    {
        Binding = binding;
        live2dRoot = root;
        Text = "动画与点击区域 · " + Path.GetFileName(binding.Model).Replace(".model3.json", "", StringComparison.OrdinalIgnoreCase);
        Font = AppVisuals.UiFont(); BackColor = AppVisuals.Canvas; ForeColor = AppVisuals.Ink;
        ClientSize = new Size(1140, 760); MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        preview = new Live2dPreviewSurface(gameRoot, root, binding) { Dock = DockStyle.Fill, Font = AppVisuals.UiFont(9F), Margin = Padding.Empty };
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(24), Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        var viewer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 20, 0) };
        viewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        viewer.RowStyles.Add(new RowStyle(SizeType.AutoSize)); viewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); viewer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        viewer.Controls.Add(AppVisuals.TextBlock("模型预览", 16F, true), 0, 0); viewer.Controls.Add(preview, 0, 1);
        var playback = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        playback.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); playback.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76)); playback.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        var play = AppVisuals.CreateButton("试播"); play.Dock = DockStyle.Fill;
        var reset = AppVisuals.CreateButton("重置"); reset.Dock = DockStyle.Fill;
        playback.Controls.Add(browseMotion, 0, 0); playback.Controls.Add(play, 1, 0); playback.Controls.Add(reset, 2, 0); viewer.Controls.Add(playback, 0, 2);
        browseMotion.AccessibleName = "选择要试播的动画"; assignedMotion.AccessibleName = "此触发项的动画";
        regionName.AccessibleName = "点击区域名称";
        play.Click += (_, _) => TryPlay((browseMotion.SelectedItem as Live2dMotionReference)?.File ?? "");
        reset.Click += (_, _) => { preview.CancelRegion(); TryPlay(""); state.Text = "已恢复默认姿态。"; };
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize)); editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 296));
        editor.Controls.Add(AppVisuals.TextBlock("触发与动画", 16F, true), 0, 0);
        rules.BackColor = AppVisuals.Surface; rules.ForeColor = AppVisuals.Ink; rules.AccessibleName = "待机、点击区域与 M1 到 M5 结算触发项";
        editor.Controls.Add(rules, 0, 1);
        var regionActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        var add = AppVisuals.CreateButton("框选区域"); add.Width = 112; delete.Width = 112;
        add.Click += (_, _) => { preview.BeginRegion(); state.Text = "在左侧拖动框选；Esc 取消。重叠区域以后添加的为准。"; };
        delete.Click += (_, _) => DeleteRegion(); regionActions.Controls.Add(add); regionActions.Controls.Add(delete); editor.Controls.Add(regionActions, 0, 2);
        var fields = AppVisuals.Stack(); fields.Dock = DockStyle.Fill; fields.AutoSize = false; fields.Margin = new Padding(0, 12, 0, 0);
        AppVisuals.AddRow(fields, AppVisuals.TextBlock("区域名称", 9F)); AppVisuals.AddRow(fields, regionName);
        var motionLabel = AppVisuals.TextBlock("播放动画", 9F); motionLabel.Margin = new Padding(0, 12, 0, 4); AppVisuals.AddRow(fields, motionLabel); AppVisuals.AddRow(fields, assignedMotion);
        audioName.AutoEllipsis = true; audioName.AutoSize = false; audioName.Height = 24; audioName.Dock = DockStyle.Top; audioName.Margin = new Padding(0, 8, 0, 0);
        AppVisuals.AddRow(fields, audioName);
        var audioActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Margin = Padding.Empty };
        audioImport.Width = 112; audioRemove.Width = 112;
        audioImport.Click += (_, _) => ImportAudio(); audioRemove.Click += (_, _) => RemoveAudio();
        audioActions.Controls.Add(audioImport); audioActions.Controls.Add(audioRemove); AppVisuals.AddRow(fields, audioActions);
        var audition = AppVisuals.CreateButton("试播绑定动画"); audition.AutoSize = true; audition.MinimumSize = new Size(160, 36); audition.Margin = new Padding(0, 12, 0, 0);
        audition.Click += (_, _) => TryPlay((assignedMotion.SelectedItem as Live2dMotionReference)?.File ?? ""); AppVisuals.AddRow(fields, audition); editor.Controls.Add(fields, 0, 3);
        shell.Controls.Add(viewer, 0, 0); shell.Controls.Add(editor, 1, 0);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 16, 0, 0), Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        state.Dock = DockStyle.Fill; state.AutoSize = false; state.TextAlign = ContentAlignment.MiddleLeft;
        var save = AppVisuals.CreateButton("完成绑定", primary: true); save.Dock = DockStyle.Fill; save.Click += (_, _) => Complete();
        footer.Controls.Add(state, 0, 0); footer.Controls.Add(save, 1, 0); shell.Controls.Add(footer, 0, 1); shell.SetColumnSpan(footer, 2);
        Controls.Add(shell);
        assignedMotion.Items.Add(new Live2dMotionReference { Group = "不绑定动画" });
        foreach (var motion in motions) { browseMotion.Items.Add(motion); assignedMotion.Items.Add(motion); }
        if (browseMotion.Items.Count > 0) browseMotion.SelectedIndex = 0;
        rules.SelectedIndexChanged += (_, _) => SelectRule();
        assignedMotion.SelectedIndexChanged += (_, _) => AssignMotion();
        regionName.TextChanged += (_, _) => RenameRegion();
        preview.RegionCreated += region => { region.Name = "区域 " + (Binding.ClickRegions.Count + 1); Binding.ClickRegions.Add(region); RefreshRules(BasicRules.Length + Binding.ClickRegions.Count - 1); };
        preview.RegionSelected += index => rules.SelectedIndex = BasicRules.Length + index;
        preview.PreviewError += message => state.Text = "预览失败：" + message;
        Shown += (_, _) => { preview.RenderFrame(); }; RefreshRules(0); AppAppearance.Initialize(this);
    }

    private void RefreshRules(int selected)
    {
        selecting = true; rules.Items.Clear(); foreach (var name in BasicRules) rules.Items.Add(name);
        foreach (var region in Binding.ClickRegions) rules.Items.Add("点击 · " + region.Name);
        selecting = false; rules.SelectedIndex = Math.Clamp(selected, 0, rules.Items.Count - 1);
    }

    private void SelectRule()
    {
        if (selecting || rules.SelectedIndex < 0) return;
        selecting = true;
        var index = rules.SelectedIndex; var regionIndex = index - BasicRules.Length;
        preview.SelectedRegion = regionIndex; regionName.Enabled = regionIndex >= 0; delete.Enabled = regionIndex >= 0;
        regionName.Text = regionIndex >= 0 ? Binding.ClickRegions[regionIndex].Name : "";
        var file = index == 0 ? Binding.IdleMotion : index == 1 ? Binding.TapMotion : index < BasicRules.Length
            ? Binding.ResultMotions.GetValueOrDefault(Live2dEvents.ResultKeys[index - 2], "") : Binding.ClickRegions[regionIndex].Motion;
        assignedMotion.SelectedIndex = Math.Max(0, assignedMotion.Items.Cast<Live2dMotionReference>().ToList().FindIndex(item => item.File == file));
        state.Text = index == 0 ? "模型显示时循环播放。" : index < 2 ? "点击未设置区域的位置时播放。" : index < BasicRules.Length
            ? "结算显示 " + Live2dEvents.ResultKeys[index - 2] + " 时播放一次，然后回到待机。" : "可修改名称和动画，或删除后重新框选。";
        selecting = false; preview.Invalidate();
        RefreshAudio();
    }

    private void AssignMotion()
    {
        if (selecting || assignedMotion.SelectedItem is not Live2dMotionReference motion || rules.SelectedIndex < 0) return;
        var index = rules.SelectedIndex;
        if (index == 0) Binding.IdleMotion = motion.File;
        else if (index == 1) Binding.TapMotion = motion.File;
        else if (index < BasicRules.Length) Binding.ResultMotions[Live2dEvents.ResultKeys[index - 2]] = motion.File;
        else Binding.ClickRegions[index - BasicRules.Length].Motion = motion.File;
        state.Text = "已设置，点击“完成绑定”返回工作台。";
        RefreshAudio();
    }

    private void RefreshAudio()
    {
        var file = (assignedMotion.SelectedItem as Live2dMotionReference)?.File ?? "";
        var audio = Binding.MotionAudio.GetValueOrDefault(file, "");
        audioImport.Enabled = file.Length > 0; audioRemove.Enabled = audio.Length > 0;
        audioName.Text = audio.Length == 0 ? "未绑定音频 · OGG / WAV" : "音频：" + Path.GetFileName(audio);
    }

    private void ImportAudio()
    {
        var file = (assignedMotion.SelectedItem as Live2dMotionReference)?.File ?? "";
        if (file.Length == 0) return;
        using var dialog = new OpenFileDialog { Title = "为此动画添加音频", Filter = "音频 (*.ogg;*.wav)|*.ogg;*.wav", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Binding.MotionAudio[file] = Live2dWorkspace.ImportAudio(live2dRoot, dialog.FileName); RefreshAudio(); state.Text = "音频已绑定到此动画，试播可同时试听。"; }
        catch (Exception e) { state.Text = "音频导入失败：" + e.Message; }
    }

    private void RemoveAudio()
    {
        var file = (assignedMotion.SelectedItem as Live2dMotionReference)?.File ?? "";
        Binding.MotionAudio.Remove(file); RefreshAudio();
    }

    private void RenameRegion()
    {
        if (selecting || rules.SelectedIndex < BasicRules.Length) return;
        var index = rules.SelectedIndex - BasicRules.Length; Binding.ClickRegions[index].Name = regionName.Text;
        selecting = true; rules.Items[rules.SelectedIndex] = "点击 · " + regionName.Text; selecting = false; preview.Invalidate();
    }

    private void DeleteRegion()
    {
        if (rules.SelectedIndex < BasicRules.Length) return;
        Binding.ClickRegions.RemoveAt(rules.SelectedIndex - BasicRules.Length); RefreshRules(1); preview.Invalidate();
    }

    private void TryPlay(string file)
    {
        try { preview.Play(file, rules.SelectedIndex == 0 && file == Binding.IdleMotion); }
        catch (Exception e) { state.Text = "无法试播：" + e.Message; }
    }

    private void Complete()
    {
        try { Binding.Validate(); DialogResult = DialogResult.OK; Close(); }
        catch (Exception e) { state.Text = e.Message; }
    }
}
