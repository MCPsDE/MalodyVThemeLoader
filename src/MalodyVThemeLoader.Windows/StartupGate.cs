using System.Diagnostics;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.Windows;

public static class StartupGate
{
    public static bool EnsureAuthorized(string productName)
    {
        var authorization = DeviceAuthorization.CheckCurrentDevice();
        if (authorization.Authorized)
        {
            return true;
        }

        MessageBox.Show(
            $"{authorization.Error}{Environment.NewLine}{Environment.NewLine}"
            + $"设备码：{authorization.DeviceCode}{Environment.NewLine}{Environment.NewLine}"
            + "程序将关闭并删除当前可执行文件。请将设备码交给项目所有者申请授权。",
            productName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Stop);
        SelfDelete.ScheduleCurrentExecutable();
        return false;
    }
}

public static class SelfDelete
{
    public static void ScheduleCurrentExecutable()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)
            || !File.Exists(executable)
            || !Path.GetFileNameWithoutExtension(executable).StartsWith(
                "MalodyVTheme",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetFullPath(executable),
                Path.GetFullPath(Environment.ProcessPath!),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var command = $"/d /c ping 127.0.0.1 -n 3 > nul & "
                      + $"del /f /q \"{executable.Replace("\"", "\"\"")}\"";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
        }
    }
}
