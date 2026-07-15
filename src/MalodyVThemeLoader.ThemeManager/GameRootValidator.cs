namespace MalodyVThemeLoader.ThemeManager;

internal static class GameRootValidator
{
    public static bool IsInstalledGameRoot(string gameRoot, out string error)
    {
        if (!Directory.Exists(gameRoot)
            || !File.Exists(Path.Combine(gameRoot, "Malody V.exe"))
            || !File.Exists(Path.Combine(
                gameRoot, "ThemeLoader", "install-manifest.json")))
        {
            error =
                "未找到有效的 ThemeLoader 安装。请从安装后的桌面快捷方式启动管理器。";
            return false;
        }

        error = "";
        return true;
    }
}
