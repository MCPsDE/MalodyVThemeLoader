namespace MalodyVThemeLoader.Installer;

internal static class InstallerCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: --check <game-dir> | --install <game-dir> | --uninstall <game-dir> | --verify <game-dir>");
                return 2;
            }

            var progress = Environment.GetEnvironmentVariable(
                "MALODY_THEMELOADER_QUIET") == "1"
                ? null
                : new Progress<string>(Console.WriteLine);
            var service = new InstallService();
            var result = args[0].ToLowerInvariant() switch
            {
                "--check" => Check(args[1]),
                "--install" => service.InstallOrRepair(args[1], progress),
                "--uninstall" => service.Uninstall(args[1], progress),
                "--verify" => service.Verify(args[1]),
                _ => OperationResult.Fail($"Unknown command: {args[0]}")
            };

            var writer = result.Success ? Console.Out : Console.Error;
            writer.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static OperationResult Check(string gameRoot)
    {
        if (!GameLocator.IsGameDirectory(gameRoot, out var error)) return OperationResult.Fail(error);
        var environment = BepInExEnvironment.Inspect(gameRoot);
        return environment.Present && !environment.Compatible
            ? OperationResult.Fail(environment.Message)
            : OperationResult.Ok(environment.Message);
    }
}
