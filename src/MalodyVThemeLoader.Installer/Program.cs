using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            return InstallerCommand.Run(args);
        }

        ApplicationConfiguration.Initialize();
        if (!StartupGate.EnsureAuthorized(ProductInfo.Name))
        {
            return 3;
        }

        using (var agreement = new AgreementDialog(
                   "安装或维护 ThemeLoader",
                   minimumReadSeconds: 5,
                   requireScroll: true))
        {
            if (agreement.ShowDialog() != DialogResult.OK)
            {
                return 1;
            }
        }

        Application.Run(new InstallerForm());
        return 0;
    }
}
