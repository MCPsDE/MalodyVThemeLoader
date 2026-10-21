using MalodyVThemeLoader.Windows;

namespace MalodyVBepInExInstaller;

internal sealed class InstallerForm : Form
{
    private readonly TextBox pathBox = new();
    private readonly Label pathState = AppVisuals.TextBlock("");
    private readonly Label operationState = AppVisuals.TextBlock("准备就绪。");
    private readonly TextBox statusBox = new();
    private readonly CheckBox configurationManagerCheckBox = new();
    private readonly Button installButton = AppVisuals.CreateButton("安装 / 修复 BepInEx", primary: true);
    private readonly Button uninstallButton = AppVisuals.CreateButton("卸载 BepInEx");
    private readonly Button browseButton = AppVisuals.CreateButton("浏览…");
    private readonly InstallService service = new();
    private bool busy;

    public InstallerForm()
    {
        Text = "Malody V · BepInEx 安装";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 680);
        MinimumSize = new Size(740, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(32) };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = AppVisuals.Stack();
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("安装 BepInEx",22F,true));
        AppVisuals.AddRow(stack,AppVisuals.TextBlock($"适用于 Malody V · BepInEx {ProductInfo.BepInExVersion} · Windows x64"));
        var label = AppVisuals.TextBlock("游戏目录",11F,true);
        label.Margin = new Padding(0,20,0,12);
        AppVisuals.AddRow(stack,label);
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, Margin = new Padding(0,0,0,8) };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        pathBox.Dock = DockStyle.Fill; pathBox.Margin = new Padding(0,6,12,0);
        pathBox.AccessibleName = "Malody V 游戏目录";
        pathBox.TextChanged += (_,_) => UpdatePathState();
        browseButton.Dock = DockStyle.Fill; browseButton.Margin = Padding.Empty;
        browseButton.Click += (_,_) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "选择包含 Malody V.exe 的目录", UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(pathBox.Text) ? pathBox.Text : "" };
            if (dialog.ShowDialog(this) == DialogResult.OK) pathBox.Text = dialog.SelectedPath;
        };
        pathRow.Controls.Add(pathBox,0,0); pathRow.Controls.Add(browseButton,1,0);
        AppVisuals.AddRow(stack,pathRow); AppVisuals.AddRow(stack,pathState);
        configurationManagerCheckBox.Text = "附带官方 ConfigurationManager（英文界面）";
        configurationManagerCheckBox.AutoSize = true; configurationManagerCheckBox.Checked = true;
        configurationManagerCheckBox.Margin = new Padding(0,12,0,12);
        AppVisuals.AddRow(stack,configurationManagerCheckBox);
        AppVisuals.AddRow(stack,AppVisuals.TextBlock("重新安装会重建 BepInEx 运行时、配置与缓存；保留 plugins 中的现有插件。"));
        var logTitle = AppVisuals.TextBlock("安装记录",11F,true); logTitle.Margin = new Padding(0,16,0,12);
        AppVisuals.AddRow(stack,logTitle);
        statusBox.Dock = DockStyle.Top; statusBox.Height = 100;
        statusBox.Multiline = statusBox.ReadOnly = true; statusBox.ScrollBars = ScrollBars.Vertical;
        statusBox.BorderStyle = BorderStyle.None; statusBox.BackColor = AppVisuals.Surface;
        statusBox.Font = AppVisuals.MonoFont(9F); statusBox.ForeColor = AppVisuals.MutedInk;
        statusBox.Text = "选择游戏目录后即可安装。";
        AppVisuals.AddRow(stack,statusBox);
        scroll.Controls.Add(stack); shell.Controls.Add(scroll,0,0);
        var footer = AppVisuals.Stack();
        AppVisuals.AddRow(footer,operationState);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Margin = Padding.Empty, WrapContents = false };
        installButton.AutoSize = uninstallButton.AutoSize = true;
        installButton.MinimumSize = new Size(200,44); uninstallButton.MinimumSize = new Size(150,44);
        installButton.Margin = new Padding(0,0,12,0);
        actions.Controls.Add(installButton); actions.Controls.Add(uninstallButton);
        AppVisuals.AddRow(footer,actions); shell.Controls.Add(footer,0,1); Controls.Add(shell);
        installButton.Click += async (_,_) =>
        {
            if (MessageBox.Show(this,"重建运行时、配置与缓存？已有 plugins 将保留。","安装 BepInEx",MessageBoxButtons.YesNo,MessageBoxIcon.Question) != DialogResult.Yes) return;
            var root = pathBox.Text;
            var includeManager = configurationManagerCheckBox.Checked;
            var progress = new Progress<string>(AppendStatus);
            await RunAsync(() => service.InstallOrRepair(root,progress,includeManager), "正在安装 BepInEx，请稍候…");
        };
        uninstallButton.Click += async (_,_) =>
        {
            if (MessageBox.Show(this,"按安装清单卸载 BepInEx？现有插件和修改过的文件将保留。","卸载 BepInEx",MessageBoxButtons.YesNo,MessageBoxIcon.Question) != DialogResult.Yes) return;
            var root = pathBox.Text;
            var progress = new Progress<string>(AppendStatus);
            await RunAsync(() => service.Uninstall(root,progress), "正在卸载 BepInEx，请稍候…");
        };
        pathBox.Text = GameLocator.FindGameDirectory() ?? "";
        UpdatePathState();
        FormClosing += (_,e) => { if(busy) e.Cancel = true; };
    }

    private void UpdatePathState()
    {
        var valid = GameLocator.IsGameDirectory(pathBox.Text, out var error);
        pathState.Text = valid ? "已找到有效的 Malody V IL2CPP 目录。" : error;
        pathState.ForeColor = valid ? Color.DarkGreen : Color.DarkRed;
        installButton.Enabled = valid && !busy;
        uninstallButton.Enabled = !busy && File.Exists(Path.Combine(pathBox.Text, ProductInfo.ManifestRelativePath));
    }

    private async Task RunAsync(Func<OperationResult> operation, string busyMessage)
    {
        SetBusy(true);
        operationState.Text = busyMessage;
        operationState.ForeColor = AppVisuals.MutedInk;
        statusBox.Clear();
        try
        {
            var result = await Task.Run(operation);
            AppendStatus(result.Message);
            operationState.Text = result.Message;
            operationState.ForeColor = result.Success ? AppVisuals.Accent : AppVisuals.Danger;
            UpdatePathState();
        }
        catch (Exception exception)
        {
            operationState.Text = "操作失败：" + exception.Message;
            operationState.ForeColor = AppVisuals.Danger;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AppendStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendStatus(message));
            return;
        }

        statusBox.AppendText(message + Environment.NewLine);
    }

    private void SetBusy(bool busy)
    {
        this.busy = busy;
        UseWaitCursor = busy;
        browseButton.Enabled = configurationManagerCheckBox.Enabled = !busy;
        pathBox.Enabled = !busy;
        installButton.Enabled = !busy;
        uninstallButton.Enabled = !busy;
        if (!busy) UpdatePathState();
    }
}
