using System.Drawing.Drawing2D;
using System.Reflection;

namespace MalodyVThemeLoader.Windows;

public static class AppVisuals
{
    private const string IconResourceName =
        "MalodyVThemeLoader.Windows.Assets.mtp.ico";
    private const string LogoResourceName =
        "MalodyVThemeLoader.Windows.Assets.mtp.png";

    private static readonly Lazy<string> UiFontFamily = new(() =>
        ResolveFontFamily(
            "Microsoft YaHei UI",
            "Segoe UI Variable Text",
            "Segoe UI"));
    private static readonly Lazy<string> MonoFontFamily = new(() =>
        ResolveFontFamily("Cascadia Mono", "Consolas", "Courier New"));

    // Fluent 2 light theme roles. These are deliberately neutral and blue;
    // green/teal is reserved for theme artwork and never acts as product chrome.
    public static readonly Color Ink = Color.FromArgb(36, 36, 36);
    public static readonly Color MutedInk = Color.FromArgb(97, 97, 97);
    public static readonly Color Canvas = Color.FromArgb(245, 245, 245);
    public static readonly Color Surface = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceAlt = Color.FromArgb(250, 250, 250);
    public static readonly Color SurfaceRaised = Color.FromArgb(255, 255, 255);
    public static readonly Color Line = Color.FromArgb(225, 225, 225);
    public static readonly Color Accent = Color.FromArgb(15, 108, 189);
    public static readonly Color AccentHover = Color.FromArgb(17, 94, 163);
    public static readonly Color AccentPressed = Color.FromArgb(0, 84, 147);
    public static readonly Color AccentSoft = Color.FromArgb(229, 241, 251);
    public static readonly Color Danger = Color.FromArgb(196, 49, 36);
    public static readonly Color DangerHover = Color.FromArgb(164, 38, 28);
    public static readonly Color DangerPressed = Color.FromArgb(139, 31, 23);
    public static readonly Color Sidebar = Color.FromArgb(248, 248, 248);
    public static readonly Color SidebarMuted = Color.FromArgb(97, 97, 97);
    public static readonly Color SidebarSoft = Color.FromArgb(243, 243, 243);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);

    public static Font UiFont(float size = 10F, FontStyle style = FontStyle.Regular)
    {
        return new Font(UiFontFamily.Value, size, style, GraphicsUnit.Point);
    }

    public static Font MonoFont(float size = 9F, FontStyle style = FontStyle.Regular)
    {
        return new Font(MonoFontFamily.Value, size, style, GraphicsUnit.Point);
    }

    public static Icon CreateAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(IconResourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public static Image CreateLogoImage()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(LogoResourceName);
            if (stream != null)
            {
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
        }
        catch
        {
        }

        var fallback = new Bitmap(96, 96);
        using var graphics = Graphics.FromImage(fallback);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(Accent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillEllipse(brush, 12, 12, 72, 72);
        return fallback;
    }

    public static Button CreateButton(
        string text,
        bool primary = false,
        bool danger = false)
    {
        var button = new ActionButton
        {
            Primary = primary,
            Danger = danger,
            AutoSize = false,
            Height = 36,
            Text = text,
            Font = UiFont(10F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = primary
                ? Accent
                : danger
                    ? Danger
                    : Surface,
            ForeColor = primary || danger ? Color.White : Ink
        };
        button.AccessibleName = text.Replace("&", "");
        button.FlatAppearance.BorderSize = primary || danger ? 0 : 1;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : danger
                ? DangerHover
                : SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = primary
            ? AccentPressed
            : danger
                ? DangerPressed
                : Color.FromArgb(237, 237, 237);
        return button;
    }

    public static TableLayoutPanel Stack(int padding = 0)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
            Padding = new Padding(padding), BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    public static Label TextBlock(string text, float size = 10F, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top,
        Font = UiFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = bold ? Ink : MutedInk, Margin = new Padding(0, 0, 0, 12),
        UseMnemonic = false
    };

    public static void AddRow(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
    }

    public static Label CreateLabel(
        string text,
        float size,
        Color color,
        FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            ForeColor = color,
            Font = UiFont(size, style)
        };
    }

    private static string ResolveFontFamily(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            try
            {
                using var font = new Font(candidate, 9F);
                if (string.Equals(
                        font.FontFamily.Name,
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return FontFamily.GenericSansSerif.Name;
    }
}

public sealed class Separator : Control
{
    public Separator()
    {
        Height = 1;
        Dock = DockStyle.Top;
        BackColor = AppVisuals.Line;
        SetStyle(ControlStyles.Selectable, false);
    }
}
