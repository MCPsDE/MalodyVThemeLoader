using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

// Keep the graphic native. Inject only the small MonoBehaviour filter, rather
// than a managed subclass of Unity's serialized UI Image hierarchy.
internal sealed class InspectorInputFence : MonoBehaviour
{
    private int releaseFrame = int.MinValue;
    public InspectorInputFence(IntPtr pointer) : base(pointer) { }
    internal bool CaptureForFrame(bool down, int frame)
    {
        if (down) releaseFrame = frame + 2;
        return down || frame <= releaseFrame;
    }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!SceneInspectorController.IsInspecting || !SceneThemeApplier.IsAllowedScene(SceneManager.GetActiveScene().name)) return false;
        return CaptureForFrame((GetAsyncKeyState(4) & 0x8000) != 0, Time.frameCount);
    }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
