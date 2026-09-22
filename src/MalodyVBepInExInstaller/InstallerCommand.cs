namespace MalodyVBepInExInstaller;

internal static class InstallerCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "用法：--install <游戏目录> | --verify <游戏目录> | --uninstall <游戏目录>");
            return 2;
        }

        var progress = Environment.GetEnvironmentVariable("MALODY_BEPINEX_QUIET") == "1"
            ? null
            : new Progress<string>(Console.WriteLine);
        var service = new InstallService();
        var result = args[0].ToLowerInvariant() switch
        {
            "--install" => service.InstallOrRepair(args[1], progress),
            "--verify" => service.Verify(args[1]),
            "--uninstall" => service.Uninstall(args[1], progress),
            _ => OperationResult.Fail($"未知命令：{args[0]}")
        };

        (result.Success ? Console.Out : Console.Error).WriteLine(result.Message);
        return result.Success ? 0 : 1;
    }
}
