using System.Diagnostics;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed class OpeningEditorPane : UserControl
{
    private readonly ThemeLibrary library;
    private readonly Action<string> report;
    private readonly Label file = AppVisuals.TextBlock("使用游戏原版开屏", 10);
    private readonly Label status = AppVisuals.TextBlock("保存后，下次启动游戏时播放。", 9);
    private readonly ResourcePreviewSurface preview = new() { Dock = DockStyle.Top, Height = 230, EmptyText = "导入图片或 WebM 开屏动画" };
    private readonly Button import = AppVisuals.CreateButton("导入开屏…");
    private readonly Button audition = AppVisuals.CreateButton("预览文件");
    private readonly Button save = AppVisuals.CreateButton("保存开屏", primary: true);
    private readonly Button restore = AppVisuals.CreateButton("恢复原版开屏");
    private readonly ChoiceBox fit = new() { Dock = DockStyle.Top };
    private readonly NumberField duration = new() { Minimum = 1, Maximum = 30, DecimalPlaces = 1, Increment = .5m, Value = 4, Dock = DockStyle.Top };
    private readonly ColorField background = new();
    private readonly ToolTip tips = new();
    private ThemeResourceWorkspace? workspace;
    private ThemeItem? theme;
    private ThemeOpening draft = new();
    private string? pending;
    private bool loading;
    public bool Dirty { get; private set; }
    public bool IsBusy { get; private set; }
    public event Action? Applied;

    internal OpeningEditorPane(ThemeLibrary library, Action<string> report)
    {
        this.library = library; this.report = report; BackColor = AppVisuals.Canvas; ForeColor = AppVisuals.Ink; Font = AppVisuals.UiFont();
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = AppVisuals.Stack(); var heading = AppVisuals.TextBlock("开屏动画", 18, true); AppVisuals.AddRow(stack, heading);
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("导入图片或 VP8 WebM，为当前主题设置开屏。", 10));
        file.AutoSize = false; file.Height = 32; file.AutoEllipsis = true; AppVisuals.AddRow(stack, file);
        AppVisuals.AddRow(stack, new ButtonRow(import, audition)); AppVisuals.AddRow(stack, preview);
        fit.Items.AddRange(new object[] { "完整显示", "裁切填满", "拉伸填满" }); fit.SelectedIndex = 0;
        var fields = new FieldGrid(); var left = AppVisuals.Stack(); var right = AppVisuals.Stack();
        Field(left, "显示方式", fit); Field(left, "背景颜色", background); Field(right, "最长播放时长 · 秒", duration);
        AppVisuals.AddRow(right, AppVisuals.TextBlock("视频播放结束后自动关闭。", 9)); fields.AddField(left); fields.AddField(right); AppVisuals.AddRow(stack, fields);
        AppVisuals.AddRow(stack, new ButtonRow(save, restore) { Margin = new Padding(0, 20, 0, 12) }); AppVisuals.AddRow(stack, status);
        scroll.Controls.Add(stack); Controls.Add(scroll); background.Value = "#000000";
        import.Click += async (_, _) => await Pick(); audition.Click += (_, _) => Preview(); save.Click += async (_, _) => await SaveAsync(); restore.Click += (_, _) => Restore();
        fit.SelectedIndexChanged += (_, _) => Changed(); duration.ValueChanged += (_, _) => Changed(); background.ValueChanged += (_, _) => Changed(); UpdateEnabled();
    }
    private static void Field(TableLayoutPanel stack, string label, Control control)
    {
        var title = AppVisuals.TextBlock(label, 9); title.Margin = new Padding(0, 12, 0, 5); AppVisuals.AddRow(stack, title);
        control.AccessibleName = label; control.Dock = DockStyle.Top; AppVisuals.AddRow(stack, control);
    }
    public bool ConfirmLeave() => !IsBusy && (!Dirty || MessageBox.Show(FindForm(), "开屏修改尚未保存，是否放弃？", "开屏动画", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
    public void SetWorkspace(ThemeResourceWorkspace? value, ThemeItem? item)
    {
        if ((Dirty || IsBusy) && workspace?.Root == value?.Root) return;
        workspace = value; theme = item; LoadSelection();
    }
    private void LoadSelection()
    {
        loading = true;
        try
        {
            draft = workspace?.Rules.Opening is { } rule ? JsonSerializer.Deserialize<ThemeOpening>(JsonSerializer.Serialize(rule))! : new ThemeOpening();
            pending = null; Dirty = false; fit.SelectedIndex = draft.Fit == "cover" ? 1 : draft.Fit == "stretch" ? 2 : 0; duration.Value = (decimal)draft.Duration; background.Value = draft.Background;
            file.Text = draft.File.Length == 0 ? "使用游戏原版开屏" : draft.DisplayName.Length > 0 ? draft.DisplayName : Path.GetFileName(draft.File);
            tips.SetToolTip(file, draft.File); SetPreview(workspace != null && draft.File.Length > 0 ? workspace.NodeFile(draft.File) : null);
            status.Text = workspace == null ? "先选择一个主题，再导入开屏。" : "保存后，下次启动游戏时播放。";
        }
        catch (Exception e) { preview.SetImage(null); status.Text = "读取开屏失败：" + e.Message; }
        finally { loading = false; UpdateEnabled(); }
    }
    private void SetPreview(string? path)
    {
        var video = path != null && Path.GetExtension(path).Equals(".webm", StringComparison.OrdinalIgnoreCase);
        preview.EmptyText = video ? "WebM 开屏动画 · 点击“预览文件”播放" : "导入图片或 WebM 开屏动画";
        preview.SetImage(path != null && !video ? ThemeResourceWorkspace.ReadBitmap(path) : null);
    }
    private void Changed() { if (loading) return; Dirty = true; status.Text = "开屏修改尚未保存"; UpdateEnabled(); }
    private void UpdateEnabled()
    {
        var edit = workspace != null && theme != null && !IsBusy;
        import.Enabled = fit.Enabled = duration.Enabled = background.Enabled = edit;
        audition.Enabled = edit && (pending != null || draft.File.Length > 0); save.Enabled = edit && Dirty && (pending != null || draft.File.Length > 0);
        restore.Enabled = edit && workspace?.Rules.Opening != null;
    }
    internal async Task ImportAsync(string path)
    {
        if (workspace == null || IsBusy) return; IsBusy = true; UpdateEnabled();
        try
        {
            var video = Path.GetExtension(path).Equals(".webm", StringComparison.OrdinalIgnoreCase);
            if (video) await Task.Run(() => WebmVideo.Inspect(path));
            else await Task.Run(() => { using var image = ThemeResourceWorkspace.ReadBitmap(path); if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 16 * 1024 * 1024) throw new InvalidDataException("开屏图片最多为 8192 像素、1600 万像素。"); });
            SetPreview(path); pending = path; file.Text = Path.GetFileName(path) + " · 待保存"; tips.SetToolTip(file, path); Changed();
        }
        catch (Exception e) { status.Text = "导入失败：" + e.Message; }
        finally { IsBusy = false; UpdateEnabled(); }
    }
    private async Task Pick()
    {
        using var dialog = new OpenFileDialog { Title = "导入主题开屏", Filter = "开屏图片或 VP8 WebM|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.webm|WebM 动画（VP8）|*.webm|静态图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp", CheckFileExists = true };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await ImportAsync(dialog.FileName);
    }
    private void Preview()
    {
        if (workspace == null || IsBusy) return;
        try { var path = pending ?? workspace.NodeFile(draft.File); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { status.Text = "无法预览：" + e.Message + "。请为此格式设置默认播放器。"; }
    }
    public async Task SaveAsync()
    {
        if (!Dirty || IsBusy || workspace == null || theme == null) return;
        IsBusy = true; UpdateEnabled();
        try
        {
            var next = JsonSerializer.Deserialize<ThemeOpening>(JsonSerializer.Serialize(draft))!;
            next.Fit = fit.SelectedIndex == 1 ? "cover" : fit.SelectedIndex == 2 ? "stretch" : "contain";
            next.Duration = (float)duration.Value; next.Background = background.Value; TextColor.Validate(next.Background);
            await Task.Run(() => workspace.SaveOpening(next, pending)); library.Activate(theme); LoadSelection();
            status.Text = "开屏已保存 · 重启游戏后生效"; report(status.Text); Applied?.Invoke();
        }
        catch (Exception e) { status.Text = "保存失败：" + e.Message; report(status.Text); }
        finally { IsBusy = false; UpdateEnabled(); }
    }
    private void Restore()
    {
        if (workspace == null || theme == null || !ConfirmLeave()) return;
        try { workspace.SaveOpening(null, null); library.Activate(theme); LoadSelection(); status.Text = "已恢复原版开屏 · 下次启动生效"; Applied?.Invoke(); }
        catch (Exception e) { status.Text = "恢复失败：" + e.Message; }
    }
    protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
}
