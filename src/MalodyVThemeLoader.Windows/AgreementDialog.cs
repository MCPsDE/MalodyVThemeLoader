using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.Windows;

public sealed class AgreementDialog : Form
{
    private const int RequiredPasses = 3;

    private readonly Label passLabel = new();
    private readonly Label instructionLabel = new();
    private readonly RichTextBox termsBox = new();
    private readonly Button nextButton;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly int minimumReadSeconds;
    private readonly bool requireScroll;
    private int currentPass = 1;
    private int secondsRemaining;
    private bool reachedBottom;

    public AgreementDialog(
        string context,
        int minimumReadSeconds = 60,
        bool requireScroll = true)
    {
        this.minimumReadSeconds = Math.Max(0, minimumReadSeconds);
        this.requireScroll = requireScroll;

        Text = LegalTerms.Title;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 650);
        MinimumSize = new Size(720, 580);
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            Padding = new Padding(28, 22, 28, 18),
            BackColor = AppVisuals.Sidebar
        };
        var title = AppVisuals.CreateLabel(
            LegalTerms.Title, 15.5F, Color.White, FontStyle.Bold);
        title.Location = new Point(28, 20);
        var subtitle = AppVisuals.CreateLabel(
            $"{context}前会显示三遍条款。",
            9F,
            AppVisuals.SidebarMuted);
        subtitle.Location = new Point(30, 58);
        passLabel.AutoSize = true;
        passLabel.Font = AppVisuals.UiFont(10F, FontStyle.Bold);
        passLabel.ForeColor = Color.White;
        passLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        passLabel.Location = new Point(704, 28);
        header.Controls.AddRange(new Control[] { title, subtitle, passLabel });

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 96,
            Padding = new Padding(28, 12, 28, 20),
            BackColor = AppVisuals.Surface
        };
        footer.Controls.Add(new Separator());
        instructionLabel.AutoSize = true;
        instructionLabel.Location = new Point(30, 36);
        instructionLabel.ForeColor = AppVisuals.MutedInk;

        var cancelButton = AppVisuals.CreateButton("退出");
        cancelButton.Size = new Size(92, 36);
        cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        cancelButton.Location = new Point(590, 46);
        cancelButton.DialogResult = DialogResult.Cancel;

        nextButton = AppVisuals.CreateButton("继续", primary: true);
        nextButton.Size = new Size(92, 36);
        nextButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        nextButton.Location = new Point(694, 46);
        nextButton.Enabled = false;
        nextButton.Click += NextButtonOnClick;
        footer.Controls.AddRange(new Control[]
        {
            instructionLabel, cancelButton, nextButton
        });

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 18),
            BackColor = AppVisuals.Canvas
        };
        termsBox.Dock = DockStyle.Fill;
        termsBox.ReadOnly = true;
        termsBox.BorderStyle = BorderStyle.FixedSingle;
        termsBox.BackColor = AppVisuals.Surface;
        termsBox.ForeColor = AppVisuals.Ink;
        termsBox.Font = AppVisuals.UiFont(10F);
        termsBox.Text = LegalTerms.Text;
        termsBox.DetectUrls = false;
        termsBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        termsBox.VScroll += (_, _) => CheckScrollPosition();
        body.Controls.Add(termsBox);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
        AcceptButton = nextButton;
        CancelButton = cancelButton;

        timer.Interval = 1000;
        timer.Tick += (_, _) =>
        {
            if (secondsRemaining > 0)
            {
                secondsRemaining--;
            }
            UpdateState();
        };
        Shown += (_, _) => StartPass();
        FormClosed += (_, _) => timer.Dispose();
    }

    private void StartPass()
    {
        timer.Stop();
        secondsRemaining = minimumReadSeconds;
        reachedBottom = !requireScroll;
        nextButton.Enabled = false;
        termsBox.SelectionStart = 0;
        termsBox.ScrollToCaret();
        passLabel.Text = $"{currentPass} / {RequiredPasses}";
        nextButton.Text = currentPass == RequiredPasses ? "继续" : "下一遍";
        UpdateState();
        if (minimumReadSeconds > 0)
        {
            timer.Start();
        }
    }

    private void CheckScrollPosition()
    {
        var point = new Point(
            Math.Max(1, termsBox.ClientSize.Width - 8),
            Math.Max(1, termsBox.ClientSize.Height - 8));
        var lastVisible = termsBox.GetCharIndexFromPosition(point);
        if (lastVisible >= termsBox.TextLength - 3)
        {
            reachedBottom = true;
        }
        UpdateState();
    }

    private void UpdateState()
    {
        if (secondsRemaining > 0)
        {
            instructionLabel.Text = $"请阅读本遍条款，{secondsRemaining} 秒后可确认。";
        }
        else if (requireScroll && !reachedBottom)
        {
            instructionLabel.Text = "请将条款滚动到最末尾。";
        }
        else
        {
            instructionLabel.Text = currentPass == RequiredPasses
                ? "本遍已完成，可以继续。"
                : "本遍已完成，可以进入下一遍。";
            nextButton.Enabled = true;
        }
    }

    private void NextButtonOnClick(object? sender, EventArgs eventArgs)
    {
        if ((requireScroll && !reachedBottom) || secondsRemaining > 0)
        {
            return;
        }

        if (currentPass < RequiredPasses)
        {
            currentPass++;
            StartPass();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
