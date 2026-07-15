using System.Text;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.Windows;

namespace MalodyVThemeLoader.Authorizer;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var label = args.Length > 0
                ? string.Join(" ", args).Trim()
                : Environment.MachineName;
            var deviceCode = DeviceAuthorization.GetDeviceCode();
            var authorizationValue =
                DeviceAuthorization.CreateAuthorizationValue(deviceCode);
            var entry = DeviceAuthorization.CreateAllowListEntry(
                label, authorizationValue);
            var outputPath = Path.Combine(
                AppContext.BaseDirectory, "authorization-value.txt");

            var content =
                $"""
                ThemeLoader authorization enrollment
                GeneratedAt: {DateTimeOffset.Now:O}
                Label: {label}
                DeviceCode: {deviceCode}
                AuthorizationValue: {authorizationValue}

                Add the following object to src/MalodyVThemeLoader.Shared/allowed-devices.json:
                {JsonSerializer.Serialize(entry, JsonDefaults.Options)}
                """;
            File.WriteAllText(outputPath, content, new UTF8Encoding(false));

            Console.WriteLine($"授权材料已写入：{outputPath}");
            Console.WriteLine("授权器将在退出后删除自身，工程和生成脚本不会被删除。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"生成失败：{exception.Message}");
            return 1;
        }
        finally
        {
            SelfDelete.ScheduleCurrentExecutable();
        }
    }
}
