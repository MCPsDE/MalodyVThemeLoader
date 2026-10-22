using System.Runtime.InteropServices;
using SkiaSharp;

namespace MalodyVThemeLoader.ThemeManager;

// A never-shown WGL window supplies a private offscreen rendering context.
// The editor receives a bitmap, so ordinary WinForms overlays and input retain
// their native behavior; the game process/window is never used for preview.
internal sealed class PreviewGpuContext : IDisposable
{
    private readonly NativeWindow window = new();
    private nint dc;
    private nint renderingContext;
    private GRGlInterface? gl;
    public GRContext Context { get; private set; } = null!;

    public PreviewGpuContext()
    {
        try
        {
            window.CreateHandle(new CreateParams { Caption = "ThemeLoader preview context", Width = 1, Height = 1, Style = unchecked((int)0x80000000) });
            dc = GetDC(window.Handle);
            var descriptor = new PixelFormatDescriptor { Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(), Version = 1,
                Flags = 0x00000004 | 0x00000020, PixelType = 0, ColorBits = 32, AlphaBits = 8, DepthBits = 24, StencilBits = 8 };
            var format = ChoosePixelFormat(dc, ref descriptor);
            if (format == 0 || !SetPixelFormat(dc, format, ref descriptor)) throw new InvalidOperationException("No OpenGL pixel format.");
            renderingContext = wglCreateContext(dc);
            if (renderingContext == 0 || !wglMakeCurrent(dc, renderingContext)) throw new InvalidOperationException("OpenGL context creation failed.");
            gl = GRGlInterface.Create();
            Context = GRContext.CreateGl(gl) ?? throw new InvalidOperationException("GPU preview is unavailable.");
        }
        catch { Dispose(); throw; }
    }

    public void MakeCurrent()
    {
        if (!wglMakeCurrent(dc, renderingContext)) throw new InvalidOperationException("模型预览的绘图环境不可用。");
    }

    public void Dispose()
    {
        if (renderingContext != 0) wglMakeCurrent(dc, renderingContext);
        Context?.Dispose(); gl?.Dispose(); gl = null;
        if (renderingContext != 0) { wglMakeCurrent(0, 0); wglDeleteContext(renderingContext); renderingContext = 0; }
        if (dc != 0) { ReleaseDC(window.Handle, dc); dc = 0; }
        window.DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
    {
        public ushort Size, Version;
        public uint Flags;
        public byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift, AlphaBits, AlphaShift;
        public byte AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits, DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
        public uint LayerMask, VisibleMask, DamageMask;
    }
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern int ChoosePixelFormat(nint dc, ref PixelFormatDescriptor descriptor);
    [DllImport("gdi32.dll")] private static extern bool SetPixelFormat(nint dc, int format, ref PixelFormatDescriptor descriptor);
    [DllImport("opengl32.dll")] private static extern nint wglCreateContext(nint dc);
    [DllImport("opengl32.dll")] private static extern bool wglMakeCurrent(nint dc, nint context);
    [DllImport("opengl32.dll")] private static extern bool wglDeleteContext(nint context);
}
