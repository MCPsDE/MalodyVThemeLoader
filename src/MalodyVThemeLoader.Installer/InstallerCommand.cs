namespace MalodyVThemeLoader.Installer;

internal static class InstallerCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable(
                    "MALODY_THEMELOADER_TEST_MODE") != "integration")
            {
                Console.Error.WriteLine(
                    "Interactive agreement is required. Command mode is reserved for integration tests.");
                return 3;
            }

            var authorization =
                MalodyVThemeLoader.Shared.DeviceAuthorization.CheckCurrentDevice();
            if (!authorization.Authorized)
            {
                Console.Error.WriteLine(
                    $"{authorization.Error} Device code: {authorization.DeviceCode}");
                return 3;
            }

            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: --install <game-dir> | --uninstall <game-dir> | --verify <game-dir>");
                return 2;
            }

            var progress = Environment.GetEnvironmentVariable(
                "MALODY_THEMELOADER_QUIET") == "1"
                ? null
                : new Progress<string>(Console.WriteLine);
            var service = new InstallService();
            var result = args[0].ToLowerInvariant() switch
            {
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
}
