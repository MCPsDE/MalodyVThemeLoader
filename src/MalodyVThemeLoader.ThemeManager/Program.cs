using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.ThemeManager;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (!StartupGate.EnsureAuthorized("主题管理器"))
        {
            return 3;
        }

        var gameRoot = ResolveGameRoot(args);
        if (!GameRootValidator.IsInstalledGameRoot(gameRoot, out var error))
        {
            MessageBox.Show(
                error,
                "主题管理器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        using (var agreement = new AgreementDialog(
                   "打开主题管理器",
                   minimumReadSeconds: 0,
                   requireScroll: false))
        {
            if (agreement.ShowDialog() != DialogResult.OK)
            {
                return 1;
            }
        }

        try
        {
            ConsentReceipt.Write(gameRoot, "theme-manager");
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法写入条款凭据：{exception.Message}",
                "主题管理器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        Application.Run(new ThemeManagerForm(gameRoot));
        return 0;
    }

    private static string ResolveGameRoot(string[] args)
    {
        if (args.Length == 2
            && string.Equals(args[0], "--game", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(args[1]);
        }

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar);
        var parent = Directory.GetParent(baseDirectory);
        return string.Equals(
            Path.GetFileName(baseDirectory),
            "ThemeLoader",
            StringComparison.OrdinalIgnoreCase)
            && parent != null
                ? parent.FullName
                : baseDirectory;
    }
}
