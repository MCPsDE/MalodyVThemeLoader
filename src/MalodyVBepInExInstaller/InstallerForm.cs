namespace MalodyVBepInExInstaller;

internal sealed class InstallerForm : Form
{
    private readonly TextBox pathBox = new();
    private readonly Label pathState = new();
    private readonly TextBox statusBox = new();
    private readonly Button installButton = new();
    private readonly Button uninstallButton = new();
    private readonly InstallService service = new();

    public InstallerForm()
    {
        Text = ProductInfo.Name;
        ClientSize = new Size(720, 390);
        MinimumSize = new Size(640, 350);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(28, 24),
            Text = "为 Malody V 安装 BepInEx"
        };
        var subtitle = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(30, 58),
            Text = $"BepInEx {ProductInfo.BepInExVersion} · Unity {ProductInfo.UnityVersion} · Windows x64"
        };
        var label = new Label
        {
            AutoSize = true,
            Location = new Point(30, 102),
            Text = "Malody V 游戏目录"
        };
        pathBox.Location = new Point(30, 126);
        pathBox.Size = new Size(540, 27);
        pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pathBox.Text = GameLocator.FindGameDirectory() ?? "";
        pathBox.TextChanged += (_, _) => UpdatePathState();

        var browse = new Button
        {
            Location = new Point(580, 124),
            Size = new Size(100, 31),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = "选择…"
        };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择 Malody V 游戏目录",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(pathBox.Text) ? pathBox.Text : ""
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                pathBox.Text = dialog.SelectedPath;
            }
        };

        pathState.AutoSize = true;
        pathState.Location = new Point(31, 161);

        installButton.Location = new Point(30, 192);
        installButton.Size = new Size(130, 36);
        installButton.Text = "安装 / 修复";
        installButton.Click += async (_, _) =>
            await RunAsync(() => service.InstallOrRepair(
                pathBox.Text, new Progress<string>(AppendStatus)));

        uninstallButton.Location = new Point(170, 192);
        uninstallButton.Size = new Size(100, 36);
        uninstallButton.Text = "卸载";
        uninstallButton.Click += async (_, _) =>
        {
            if (MessageBox.Show(
                    this,
                    "按本安装器清单卸载 BepInEx？其他插件和被修改的文件会保留。",
                    ProductInfo.Name,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            await RunAsync(() => service.Uninstall(
                pathBox.Text, new Progress<string>(AppendStatus)));
        };

        statusBox.Location = new Point(30, 250);
        statusBox.Size = new Size(650, 105);
        statusBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;
        statusBox.Multiline = true;
        statusBox.ReadOnly = true;
        statusBox.ScrollBars = ScrollBars.Vertical;

        Controls.AddRange(new Control[]
        {
            title, subtitle, label, pathBox, browse, pathState,
            installButton, uninstallButton, statusBox
        });
        UpdatePathState();
    }

    private void UpdatePathState()
    {
        var valid = GameLocator.IsGameDirectory(pathBox.Text, out var error);
        pathState.Text = valid ? "已找到有效的 Malody V IL2CPP 目录。" : error;
        pathState.ForeColor = valid ? Color.DarkGreen : Color.DarkRed;
        installButton.Enabled = valid;
        uninstallButton.Enabled = Directory.Exists(pathBox.Text);
    }

    private async Task RunAsync(Func<OperationResult> operation)
    {
        SetBusy(true);
        statusBox.Clear();
        try
        {
            var result = await Task.Run(operation);
            AppendStatus(result.Message);
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
        pathBox.Enabled = !busy;
        installButton.Enabled = !busy;
        uninstallButton.Enabled = !busy;
    }
}
