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
            "HarmonyOS Sans SC",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "Microsoft YaHei UI",
            "Segoe UI Variable Text",
            "Segoe UI"));
    private static readonly Lazy<string> MonoFontFamily = new(() =>
        ResolveFontFamily("Cascadia Mono", "Consolas", "Courier New"));

    public static readonly Color Ink = Color.FromArgb(25, 27, 31);
    public static readonly Color MutedInk = Color.FromArgb(102, 108, 117);
    public static readonly Color Canvas = Color.FromArgb(246, 247, 249);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(240, 243, 246);
    public static readonly Color Line = Color.FromArgb(219, 224, 230);
    public static readonly Color Accent = Color.FromArgb(36, 109, 132);
    public static readonly Color AccentHover = Color.FromArgb(24, 86, 107);
    public static readonly Color AccentSoft = Color.FromArgb(226, 242, 246);
    public static readonly Color Danger = Color.FromArgb(184, 63, 74);
    public static readonly Color Sidebar = Color.FromArgb(27, 32, 39);
    public static readonly Color SidebarMuted = Color.FromArgb(183, 191, 201);
    public static readonly Color SidebarSoft = Color.FromArgb(42, 49, 59);
    public static readonly Color Success = Color.FromArgb(91, 176, 151);

    public static Font UiFont(float size = 9F, FontStyle style = FontStyle.Regular)
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
        var button = new Button
        {
            AutoSize = false,
            Height = 36,
            Text = text,
            Font = UiFont(9.25F, FontStyle.Bold),
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
        button.FlatAppearance.BorderSize = primary || danger ? 0 : 1;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : danger
                ? Color.FromArgb(148, 41, 48)
                : SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(13, 72, 87)
            : danger
                ? Color.FromArgb(132, 36, 43)
                : Color.FromArgb(232, 237, 242);
        return button;
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
