using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.Installer;

internal sealed class InstallerForm : Form
{
    private readonly TextBox pathBox = new();
    private readonly Label pathState = new();
    private readonly Button browseButton;
    private readonly Button installButton;
    private readonly Button uninstallButton;
    private readonly TextBox statusBox = new();
    private readonly Label operationState = new();
    private readonly ProgressBar progressBar = new();
    private readonly InstallService service = new();

    public InstallerForm()
    {
        Text = ProductInfo.Name;
        ClientSize = new Size(930, 610);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();

        var sidebar = BuildSidebar();
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppVisuals.Canvas,
            Padding = new Padding(34, 30, 34, 26)
        };

        var title = AppVisuals.CreateLabel(
            "安装与维护", 18.5F, AppVisuals.Ink, FontStyle.Bold);
        title.Location = new Point(34, 28);
        var subtitle = AppVisuals.CreateLabel(
            "选择游戏目录。安装、修复和卸载都会按清单处理。",
            9.5F,
            AppVisuals.MutedInk);
        subtitle.Location = new Point(36, 67);

        var pathLabel = AppVisuals.CreateLabel(
            "游戏目录", 9F, AppVisuals.Ink, FontStyle.Bold);
        pathLabel.Location = new Point(34, 112);

        pathBox.Location = new Point(34, 137);
        pathBox.Size = new Size(500, 29);
        pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pathBox.BorderStyle = BorderStyle.FixedSingle;
        pathBox.Font = AppVisuals.UiFont(9.5F);
        pathBox.Text = GameLocator.FindGameDirectory() ?? "";
        pathBox.TextChanged += (_, _) => UpdatePathState();

        browseButton = AppVisuals.CreateButton("选择…");
        browseButton.Size = new Size(88, 32);
        browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browseButton.Location = new Point(548, 135);
        browseButton.Click += BrowseButtonOnClick;

        pathState.AutoSize = true;
        pathState.Location = new Point(35, 176);
        pathState.Font = AppVisuals.UiFont(8.5F, FontStyle.Bold);

        installButton = AppVisuals.CreateButton("安装 / 修复", primary: true);
        installButton.Location = new Point(34, 214);
        installButton.Size = new Size(138, 40);
        installButton.Click += async (_, _) => await RunOperationAsync(
            () => service.InstallOrRepair(pathBox.Text, CreateProgress()));

        uninstallButton = AppVisuals.CreateButton("卸载", danger: true);
        uninstallButton.Location = new Point(184, 214);
        uninstallButton.Size = new Size(104, 40);
        uninstallButton.Click += async (_, _) =>
        {
            if (MessageBox.Show(
                    this,
                    "确认按安装清单卸载？被修改或不再归安装器所有的文件会保留。",
                    ProductInfo.Name,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            await RunOperationAsync(
                () => service.Uninstall(pathBox.Text, CreateProgress()));
        };

        var logLabel = AppVisuals.CreateLabel(
            "操作记录", 9F, AppVisuals.Ink, FontStyle.Bold);
        logLabel.Location = new Point(34, 286);

        statusBox.Location = new Point(34, 312);
        statusBox.Size = new Size(602, 204);
        statusBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;
        statusBox.Multiline = true;
        statusBox.ReadOnly = true;
        statusBox.ScrollBars = ScrollBars.Vertical;
        statusBox.BorderStyle = BorderStyle.FixedSingle;
        statusBox.BackColor = AppVisuals.Surface;
        statusBox.ForeColor = AppVisuals.Ink;
        statusBox.Font = AppVisuals.MonoFont(8.5F);

        operationState.AutoSize = true;
        operationState.Location = new Point(34, 536);
        operationState.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        operationState.ForeColor = AppVisuals.MutedInk;
        operationState.Text = "就绪";

        progressBar.Location = new Point(34, 565);
        progressBar.Size = new Size(602, 4);
        progressBar.Anchor = AnchorStyles.Bottom
                             | AnchorStyles.Left | AnchorStyles.Right;
        progressBar.Style = ProgressBarStyle.Continuous;

        content.Controls.AddRange(new Control[]
        {
            title,
            subtitle,
            pathLabel,
            pathBox,
            browseButton,
            pathState,
            installButton,
            uninstallButton,
            logLabel,
            statusBox,
            operationState,
            progressBar
        });

        Controls.Add(content);
        Controls.Add(sidebar);
        content.Resize += (_, _) =>
        {
            browseButton.Left = content.ClientSize.Width - 122;
            pathBox.Width = Math.Max(260, browseButton.Left - 46);
            statusBox.Width = content.ClientSize.Width - 68;
            progressBar.Width = content.ClientSize.Width - 68;
        };
        UpdatePathState();
    }

    private Control BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 254,
            BackColor = AppVisuals.Sidebar,
            Padding = new Padding(26, 28, 24, 24)
        };
        var logo = new PictureBox
        {
            Image = AppVisuals.CreateLogoImage(),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Location = new Point(26, 30),
            Size = new Size(58, 58)
        };
        var product = AppVisuals.CreateLabel(
            "ThemeLoader", 20F, Color.White, FontStyle.Bold);
        product.Location = new Point(25, 108);
        var version = AppVisuals.CreateLabel(
            $"Theme Pack Installer · {ProductInfo.Version}",
            9F,
            AppVisuals.SidebarMuted);
        version.Location = new Point(27, 154);

        var divider = new Panel
        {
            Location = new Point(26, 188),
            Size = new Size(202, 1),
            BackColor = Color.FromArgb(67, 72, 79)
        };

        var authorization = DeviceAuthorization.CheckCurrentDevice();
        var authLabel = AppVisuals.CreateLabel(
            authorization.Authorized
                ? $"已授权 · {authorization.Label}"
                : "设备未授权",
            8.5F,
            authorization.Authorized
                ? AppVisuals.Success
                : Color.FromArgb(247, 153, 158),
            FontStyle.Bold);
        authLabel.Location = new Point(27, 536);
        authLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        var legal = AppVisuals.CreateLabel(
            $"条款 {LegalTerms.Version}",
            8F,
            AppVisuals.SidebarMuted);
        legal.Location = new Point(27, 563);
        legal.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        panel.Controls.AddRange(new Control[]
        {
            logo, product, version, divider, authLabel, legal
        });
        return panel;
    }

    private void BrowseButtonOnClick(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择游戏安装目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(pathBox.Text) ? pathBox.Text : ""
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            pathBox.Text = dialog.SelectedPath;
        }
    }

    private async Task RunOperationAsync(Func<OperationResult> operation)
    {
        SetBusy(true);
        statusBox.Clear();
        operationState.Text = "正在执行…";

        try
        {
            var result = await Task.Run(operation);
            AppendStatus(result.Message);
            operationState.Text = result.Success ? "操作完成" : "操作失败";
            operationState.ForeColor =
                result.Success ? AppVisuals.Accent : AppVisuals.Danger;
            MessageBox.Show(
                this,
                result.Message,
                ProductInfo.Name,
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            UpdatePathState();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private IProgress<string> CreateProgress()
    {
        return new Progress<string>(AppendStatus);
    }

    private void AppendStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendStatus(message));
            return;
        }

        statusBox.AppendText(
            $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        statusBox.SelectionStart = statusBox.TextLength;
        statusBox.ScrollToCaret();
    }

    private void UpdatePathState()
    {
        var valid = GameLocator.IsGameDirectory(pathBox.Text, out var error);
        pathState.Text = valid ? "已识别目标目录" : error;
        pathState.ForeColor = valid ? AppVisuals.Accent : AppVisuals.Danger;
        installButton.Enabled = valid && !UseWaitCursor;
        uninstallButton.Enabled = Directory.Exists(pathBox.Text) && !UseWaitCursor;
    }

    private void SetBusy(bool busy)
    {
        pathBox.Enabled = !busy;
        browseButton.Enabled = !busy;
        installButton.Enabled = !busy;
        uninstallButton.Enabled = !busy;
        UseWaitCursor = busy;
        progressBar.Style = busy
            ? ProgressBarStyle.Marquee
            : ProgressBarStyle.Continuous;
        if (!busy)
        {
            progressBar.Value = 0;
            UpdatePathState();
        }
    }
}
