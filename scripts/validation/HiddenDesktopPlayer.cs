using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Unity suppresses normal screen presentation for SW_HIDE windows. Give the
// validation player a normal window on a private, never-activated desktop;
// neither the user's desktop nor the running game is brought to foreground.
public static class HiddenDesktopPlayer
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string reserved, desktop, title;
        public int x, y, width, height, columns, rows, fill, flags;
        public short show, reservedBytes;
        public IntPtr reservedPointer, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr process, thread; public int pid, tid; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr security);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessW(string executable, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr handle, uint code);
    public static int Run(string executable, string log)
    {
        var name = "ThemeLoaderValidation_" + Guid.NewGuid().ToString("N");
        var desktop = CreateDesktopW(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        ProcessInfo process = default(ProcessInfo);
        try
        {
            var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), desktop = "winsta0\\" + name, flags = 1, show = 4 };
            var command = new StringBuilder("\"" + executable + "\" -screen-width 640 -screen-height 480 -logFile \"" + log + "\"");
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, false, 0x400, IntPtr.Zero, System.IO.Path.GetDirectoryName(executable), ref startup, out process)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var timer = Stopwatch.StartNew();
            while (WaitForSingleObject(process.process, 1000) == 258)
                if (timer.Elapsed.TotalSeconds > 120) { TerminateProcess(process.process, 1); throw new TimeoutException("Isolated overlay player timed out."); }
            if (!GetExitCodeProcess(process.process, out var code)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return (int)code;
        }
        finally { if (process.thread != IntPtr.Zero) CloseHandle(process.thread); if (process.process != IntPtr.Zero) CloseHandle(process.process); CloseDesktop(desktop); }
    }
}
