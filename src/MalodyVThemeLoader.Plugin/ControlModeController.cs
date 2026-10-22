using BepInEx;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MalodyVThemeLoader.Shared;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ControlModeController : MonoBehaviour
{
    private static ControlModeController? instance;
    internal static bool TryMapInspectorPoint(Vector2 point, out Vector2 mapped)
    {
        mapped = point; var current = instance;
        if (current == null || !current.controlMode || current.displayCamera == null || current.screenQuad == null || !current.screenQuad.activeInHierarchy) return true;
        var transform = current.screenQuad.transform;
        var ray = current.displayCamera.ScreenPointToRay(new Vector3(point.x, point.y, 0));
        var plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out var distance)) return false;
        var local = transform.InverseTransformPoint(ray.GetPoint(distance));
        if (Math.Abs(local.x) > .5f || Math.Abs(local.y) > .5f) return false;
        // The captured-frame quad is rotated 180 degrees around X to present
        // the screen capture upright. Undo that flip for source UI coordinates.
        mapped = new Vector2((local.x + .5f) * Screen.width, (.5f - local.y) * Screen.height); return true;
    }
    private const int ProjectionLayer = 31;
    private const float SettingsPollInterval = 0.5f;
    private const float ProjectionDistance = 8f;
    private const float BackgroundDistance = 80f;
    private const float CameraMoveSpeed = 5.5f;
    private const float MousePixelSensitivity = 0.12f;
    private const float LegacyMouseSensitivity = 2.4f;
    private const float RollStep = 7.5f;

    private string loaderRoot = "";
    private bool controlMode;
    private bool controlInputActive;
    private bool inputApiResolved;
    private bool projectionWarningLogged;
    private bool overlayWarningLogged;
    private float nextSettingsPoll;
    private MethodInfo? getKeyMethod;
    private MethodInfo? getAxisMethod;
    private PropertyInfo? mouseScrollDeltaProperty;
    private Coroutine? captureCoroutine;

    private Vector3 cameraPosition = new(0f, 0f, -ProjectionDistance);
    private Quaternion cameraRotation = Quaternion.identity;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLockMode;
    private NativePoint savedCursorPosition;
    private bool hasSavedCursorPosition;

    private GameObject? projectionRoot;
    private GameObject? capturedFrameRoot;
    private GameObject? screenQuad;
    private GameObject? backgroundQuad;
    private GameObject? topOverlayRoot;
    private RawImage? topOverlayImage;
    private Camera? displayCamera;
    private RenderTexture? captureTexture;
    private Material? screenMaterial;
    private Material? backgroundMaterial;
    private Texture2D? backgroundTexture;
    private string loadedBackgroundPath = "";
    private DateTime loadedBackgroundWriteTimeUtc = DateTime.MinValue;
    private Texture2D? topTexture;
    private string loadedTopPath = "";
    private DateTime loadedTopWriteTimeUtc = DateTime.MinValue;
    private int captureWidth;
    private int captureHeight;

    private GameObject? overlayRoot;
    private Text? overlayText;

    public ControlModeController(IntPtr pointer) : base(pointer)
    {
    }

    public void Initialize()
    {
        instance = this;
        loaderRoot = Path.Combine(Paths.GameRootPath, "ThemeLoader");
        controlMode = ReadControlMode();
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        captureCoroutine = StartCoroutine(
            CaptureAndPresentLoop().WrapToIl2Cpp());
        Plugin.Log.LogInfo(controlMode
            ? "Full-frame control mode installed and enabled."
            : "Full-frame control mode installed.");
    }

    private void Update()
    {
        PollControlModeSetting();
        var ctrlPressed = controlMode
                          && Application.isFocused
                          && (GetKey(KeyCode.LeftControl)
                              || GetKey(KeyCode.RightControl));
        SetControlInputActive(ctrlPressed);
        if (!ctrlPressed)
        {
            HideOverlay();
            return;
        }

        var input = ReadControlInput();
        var rollDelta = Math.Abs(input.Scroll) > 0.001f
            ? input.Scroll * RollStep
            : 0f;
        if (Math.Abs(input.PitchDegrees) > 0.0001f
            || Math.Abs(input.YawDegrees) > 0.0001f
            || Math.Abs(rollDelta) > 0.0001f)
        {
            cameraRotation *= Quaternion.Euler(
                input.PitchDegrees,
                input.YawDegrees,
                rollDelta);
        }

        var movement = cameraRotation
                       * new Vector3(input.Horizontal, 0f, input.Forward);
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        cameraPosition += movement * CameraMoveSpeed * Time.unscaledDeltaTime;
    }

    private IEnumerator CaptureAndPresentLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            if (!controlMode)
            {
                continue;
            }

            try
            {
                EnsureProjectionObjects();
                EnsureCaptureTexture();
                LoadBackgroundTextureIfChanged();
                LoadTopTextureIfChanged();
                ApplyProjectionLayout();
                UpdateOverlay(controlInputActive);

                ScreenCapture.CaptureScreenshotIntoRenderTexture(captureTexture!);
                displayCamera!.Render();
                projectionWarningLogged = false;
            }
            catch (Exception exception)
            {
                DisableProjectionPresentation();
                if (!projectionWarningLogged)
                {
                    projectionWarningLogged = true;
                    Plugin.Log.LogWarning(
                        $"Full-frame control mode is unavailable; "
                        + $"the original screen remains active: {exception.Message}");
                }
            }
        }
    }

    private void PollControlModeSetting()
    {
        var now = Time.realtimeSinceStartup;
        if (now < nextSettingsPoll)
        {
            return;
        }

        nextSettingsPoll = now + SettingsPollInterval;
        var enabled = ReadControlMode();
        if (enabled == controlMode)
        {
            return;
        }

        controlMode = enabled;
        if (!enabled)
        {
            SetControlInputActive(false);
            DisableProjectionPresentation();
            HideOverlay();
        }

        Plugin.Log.LogInfo(enabled
            ? "Full-frame control mode enabled."
            : "Full-frame control mode disabled.");
    }

    private bool ReadControlMode()
    {
        try
        {
            return ThemeRuntime.ReadSettings(loaderRoot).ControlMode;
        }
        catch
        {
            return false;
        }
    }

    private ControlInput ReadControlInput()
    {
        var mouseLook = ReadMouseLook();
        return new ControlInput(
            Axis(KeyCode.D, KeyCode.A),
            Axis(KeyCode.W, KeyCode.S),
            mouseLook.y,
            mouseLook.x,
            ReadScroll());
    }

    private Vector2 ReadMouseLook()
    {
        if (TryReadCenteredMouseDelta(out var pixelDelta))
        {
            return pixelDelta * MousePixelSensitivity;
        }

        return new Vector2(
            ReadAxis("Mouse X") * LegacyMouseSensitivity,
            -ReadAxis("Mouse Y") * LegacyMouseSensitivity);
    }

    private static bool TryReadCenteredMouseDelta(out Vector2 delta)
    {
        delta = Vector2.zero;
        if (!TryGetForegroundClientCenter(out var center)
            || !GetCursorPos(out var current))
        {
            return false;
        }

        var deltaX = Math.Clamp(current.X - center.X, -500, 500);
        var deltaY = Math.Clamp(current.Y - center.Y, -500, 500);
        SetCursorPos(center.X, center.Y);
        delta = new Vector2(deltaX, deltaY);
        return true;
    }

    private static bool TryGetForegroundClientCenter(out NativePoint center)
    {
        center = default;
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero
            || !GetClientRect(window, out var clientRect)
            || clientRect.Right <= clientRect.Left
            || clientRect.Bottom <= clientRect.Top)
        {
            return false;
        }

        center = new NativePoint
        {
            X = clientRect.Left + (clientRect.Right - clientRect.Left) / 2,
            Y = clientRect.Top + (clientRect.Bottom - clientRect.Top) / 2
        };
        return ClientToScreen(window, ref center);
    }

    private static void CenterForegroundCursor()
    {
        if (TryGetForegroundClientCenter(out var center))
        {
            SetCursorPos(center.X, center.Y);
        }
    }

    private void EnsureInputApi()
    {
        if (inputApiResolved)
        {
            return;
        }

        inputApiResolved = true;
        var inputType = ResolveInputType();
        if (inputType == null)
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        getKeyMethod = inputType.GetMethod(
            "GetKey",
            flags,
            null,
            new[] { typeof(KeyCode) },
            null);
        getAxisMethod = inputType.GetMethod(
            "GetAxis",
            flags,
            null,
            new[] { typeof(string) },
            null);
        mouseScrollDeltaProperty = inputType.GetProperty("mouseScrollDelta", flags);
    }

    private static Type? ResolveInputType()
    {
        foreach (var assemblyName in new[]
                 {
                     "UnityEngine.InputLegacyModule",
                     "UnityEngine.CoreModule",
                     "UnityEngine"
                 })
        {
            var type = Type.GetType($"UnityEngine.Input, {assemblyName}");
            if (type != null)
            {
                return type;
            }
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEngine.Input"))
            .FirstOrDefault(type => type != null);
    }

    private bool GetKey(KeyCode key)
    {
        EnsureInputApi();
        return TryGetUnityKey(key) || TryGetWindowsKey(key);
    }

    private bool TryGetUnityKey(KeyCode key)
    {
        try
        {
            return getKeyMethod?.Invoke(null, new object[] { key }) is true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetWindowsKey(KeyCode key)
    {
        var virtualKey = ToVirtualKey(key);
        if (virtualKey == 0)
        {
            return false;
        }

        try
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static int ToVirtualKey(KeyCode key)
    {
        return key switch
        {
            KeyCode.A => 0x41,
            KeyCode.D => 0x44,
            KeyCode.S => 0x53,
            KeyCode.W => 0x57,
            KeyCode.LeftControl => 0xA2,
            KeyCode.RightControl => 0xA3,
            _ => 0
        };
    }

    private float Axis(KeyCode positive, KeyCode negative)
    {
        var value = 0f;
        if (GetKey(positive))
        {
            value += 1f;
        }

        if (GetKey(negative))
        {
            value -= 1f;
        }

        return value;
    }

    private float ReadAxis(string axisName)
    {
        EnsureInputApi();
        try
        {
            return getAxisMethod?.Invoke(null, new object[] { axisName })
                is float value
                ? value
                : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    private float ReadScroll()
    {
        EnsureInputApi();
        try
        {
            if (mouseScrollDeltaProperty?.GetValue(null) is Vector2 delta)
            {
                return delta.y;
            }

            return ReadAxis("Mouse ScrollWheel") * 10f;
        }
        catch
        {
            return 0f;
        }
    }

    private void SetControlInputActive(bool active)
    {
        if (active == controlInputActive)
        {
            return;
        }

        controlInputActive = active;
        try
        {
            if (active)
            {
                savedCursorVisible = Cursor.visible;
                savedCursorLockMode = Cursor.lockState;
                hasSavedCursorPosition = GetCursorPos(out savedCursorPosition);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
                CenterForegroundCursor();
            }
            else
            {
                Cursor.lockState = savedCursorLockMode;
                Cursor.visible = savedCursorVisible;
                if (hasSavedCursorPosition)
                {
                    SetCursorPos(savedCursorPosition.X, savedCursorPosition.Y);
                }

                hasSavedCursorPosition = false;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogDebug(
                $"Control mode cursor state was not changed: {exception.Message}");
        }
    }

    private void EnsureProjectionObjects()
    {
        if (projectionRoot != null)
        {
            projectionRoot.SetActive(true);
            return;
        }

        var shader = Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("UI/Default");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "No compatible texture shader was found.");
        }

        screenMaterial = new Material(shader)
        {
            name = "MalodyVThemeLoader Captured Frame Material"
        };
        backgroundMaterial = new Material(shader)
        {
            name = "MalodyVThemeLoader Background Material"
        };

        projectionRoot = new GameObject("ThemeLoader.FullFrameProjection");
        UnityEngine.Object.DontDestroyOnLoad(projectionRoot);
        projectionRoot.hideFlags = HideFlags.HideAndDontSave;

        var displayCameraObject = new GameObject("Projection Display Camera");
        displayCameraObject.transform.SetParent(projectionRoot.transform, false);
        displayCameraObject.layer = ProjectionLayer;
        displayCamera = displayCameraObject.AddComponent<Camera>();
        displayCamera.enabled = false;
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = Color.black;
        displayCamera.cullingMask = 1 << ProjectionLayer;
        displayCamera.nearClipPlane = 0.05f;
        displayCamera.farClipPlane = 100f;
        displayCamera.fieldOfView = 60f;
        displayCamera.allowHDR = false;
        displayCamera.allowMSAA = false;
        displayCamera.useOcclusionCulling = false;

        capturedFrameRoot = new GameObject("Captured Original Screen");
        capturedFrameRoot.transform.SetParent(projectionRoot.transform, false);
        capturedFrameRoot.layer = ProjectionLayer;
        capturedFrameRoot.transform.localPosition = Vector3.zero;

        screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screenQuad.name = "Captured Frame Surface";
        screenQuad.transform.SetParent(capturedFrameRoot.transform, false);
        screenQuad.layer = ProjectionLayer;
        screenQuad.GetComponent<MeshRenderer>().sharedMaterial = screenMaterial;

        backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundQuad.name = "Control Mode Background";
        backgroundQuad.transform.SetParent(displayCamera.transform, false);
        backgroundQuad.layer = ProjectionLayer;
        backgroundQuad.GetComponent<MeshRenderer>().sharedMaterial =
            backgroundMaterial;

        EnsureTopOverlay();
        LoadBackgroundTextureIfChanged();
        LoadTopTextureIfChanged();
    }

    private void EnsureCaptureTexture()
    {
        var width = Math.Max(Screen.width, 1);
        var height = Math.Max(Screen.height, 1);
        if (captureTexture != null
            && width == captureWidth
            && height == captureHeight)
        {
            return;
        }

        if (screenMaterial != null)
        {
            screenMaterial.mainTexture = null;
        }

        if (captureTexture != null)
        {
            captureTexture.Release();
            UnityEngine.Object.Destroy(captureTexture);
        }

        captureWidth = width;
        captureHeight = height;
        captureTexture = new RenderTexture(
            captureWidth,
            captureHeight,
            0,
            RenderTextureFormat.ARGB32)
        {
            name = "MalodyVThemeLoader Full Screen Capture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        captureTexture.Create();
        screenMaterial!.mainTexture = captureTexture;
    }

    private void ApplyProjectionLayout()
    {
        if (displayCamera == null
            || capturedFrameRoot == null
            || screenQuad == null
            || backgroundQuad == null)
        {
            return;
        }

        var aspect = captureHeight == 0
            ? 16f / 9f
            : (float)captureWidth / captureHeight;
        displayCamera.aspect = aspect;
        displayCamera.transform.localPosition = cameraPosition;
        displayCamera.transform.localRotation = cameraRotation;

        capturedFrameRoot.transform.localPosition = Vector3.zero;
        capturedFrameRoot.transform.localRotation = Quaternion.identity;
        var frameHeight =
            Mathf.Tan(displayCamera.fieldOfView * Mathf.Deg2Rad * 0.5f)
            * ProjectionDistance
            * 2f;
        screenQuad.transform.localPosition = Vector3.zero;
        screenQuad.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        screenQuad.transform.localScale =
            new Vector3(frameHeight * aspect, frameHeight, 1f);

        var backgroundHeight =
            Mathf.Tan(displayCamera.fieldOfView * Mathf.Deg2Rad * 0.5f)
            * BackgroundDistance
            * 2f;
        backgroundQuad.transform.localPosition =
            new Vector3(0f, 0f, BackgroundDistance);
        backgroundQuad.transform.localRotation = Quaternion.identity;
        backgroundQuad.transform.localScale =
            new Vector3(backgroundHeight * aspect, backgroundHeight, 1f);
        backgroundQuad.SetActive(backgroundTexture != null);
        if (topOverlayRoot != null)
        {
            topOverlayRoot.SetActive(topTexture != null);
        }
    }

    private void EnsureTopOverlay()
    {
        if (topOverlayRoot != null
            || displayCamera == null
            || projectionRoot == null)
        {
            return;
        }

        topOverlayRoot = new GameObject("MalodyVThemeLoader Control Top Image");
        topOverlayRoot.transform.SetParent(projectionRoot.transform, false);
        topOverlayRoot.layer = ProjectionLayer;
        topOverlayRoot.hideFlags = HideFlags.HideAndDontSave;
        var canvas = topOverlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = displayCamera;
        canvas.planeDistance = 0.3f;
        canvas.sortingOrder = short.MaxValue - 1;
        var scaler = topOverlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var imageObject = new GameObject("Top Image");
        imageObject.transform.SetParent(topOverlayRoot.transform, false);
        imageObject.layer = ProjectionLayer;
        topOverlayImage = imageObject.AddComponent<RawImage>();
        topOverlayImage.raycastTarget = false;
        topOverlayImage.color = Color.white;
        var rect = topOverlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        topOverlayRoot.SetActive(false);
    }

    private void LoadTopTextureIfChanged()
    {
        var path = ResolveTopImagePath();
        var writeTimeUtc = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path)
            : DateTime.MinValue;
        if (string.Equals(
                path,
                loadedTopPath,
                StringComparison.OrdinalIgnoreCase)
            && writeTimeUtc == loadedTopWriteTimeUtc)
        {
            return;
        }

        loadedTopPath = path;
        loadedTopWriteTimeUtc = writeTimeUtc;
        if (topTexture != null)
        {
            UnityEngine.Object.Destroy(topTexture);
            topTexture = null;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (topOverlayImage != null)
            {
                topOverlayImage.texture = null;
            }

            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "MalodyVThemeLoader Control Top Image",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            topTexture = texture;
            if (topOverlayImage != null)
            {
                topOverlayImage.texture = texture;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Control mode top image was not loaded: {exception.Message}");
            loadedTopPath = "";
            loadedTopWriteTimeUtc = DateTime.MinValue;
        }
    }

    private string ResolveTopImagePath()
    {
        string? configured;
        try
        {
            configured = ThemeRuntime.ReadSettings(loaderRoot).ControlTopImage;
        }
        catch
        {
            configured = null;
        }

        var relative = string.IsNullOrWhiteSpace(configured)
            ? "control-top.png"
            : configured.Trim();
        try
        {
            return PathRules.GetSafeCombinedPath(loaderRoot, relative);
        }
        catch
        {
            return "";
        }
    }

    private void LoadBackgroundTextureIfChanged()
    {
        var path = ResolveBackgroundImagePath();
        var writeTimeUtc = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path)
            : DateTime.MinValue;
        if (string.Equals(
                path,
                loadedBackgroundPath,
                StringComparison.OrdinalIgnoreCase)
            && writeTimeUtc == loadedBackgroundWriteTimeUtc)
        {
            return;
        }

        loadedBackgroundPath = path;
        loadedBackgroundWriteTimeUtc = writeTimeUtc;
        if (backgroundTexture != null)
        {
            UnityEngine.Object.Destroy(backgroundTexture);
            backgroundTexture = null;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (backgroundMaterial != null)
            {
                backgroundMaterial.mainTexture = null;
            }

            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "MalodyVThemeLoader Control Background",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            backgroundTexture = texture;
            if (backgroundMaterial != null)
            {
                backgroundMaterial.mainTexture = texture;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Control mode background image was not loaded: {exception.Message}");
            loadedBackgroundPath = "";
            loadedBackgroundWriteTimeUtc = DateTime.MinValue;
        }
    }

    private string ResolveBackgroundImagePath()
    {
        string? configured;
        try
        {
            configured = ThemeRuntime.ReadSettings(loaderRoot).ControlBackgroundImage;
        }
        catch
        {
            configured = null;
        }

        var relative = string.IsNullOrWhiteSpace(configured)
            ? "control-background.png"
            : configured.Trim();
        try
        {
            return PathRules.GetSafeCombinedPath(loaderRoot, relative);
        }
        catch
        {
            return "";
        }
    }

    private void DisableProjectionPresentation()
    {
        if (projectionRoot != null)
        {
            projectionRoot.SetActive(false);
        }
    }

    private void UpdateOverlay(bool visible)
    {
        if (!visible)
        {
            HideOverlay();
            return;
        }

        try
        {
            EnsureOverlay();
            if (overlayRoot == null || overlayText == null)
            {
                return;
            }

            overlayRoot.SetActive(true);
            var euler = cameraRotation.eulerAngles;
            overlayText.text =
                "控制模式相机\n"
                + $"Position: x {cameraPosition.x:F2}, y {cameraPosition.y:F2}, "
                + $"z {cameraPosition.z:F2}\n"
                + $"View: pitch {ToSignedDegrees(euler.x):F1}, "
                + $"yaw {ToSignedDegrees(euler.y):F1}, "
                + $"roll {ToSignedDegrees(euler.z):F1}\n"
                + "Ctrl + WASD 移动 | Ctrl + 鼠标观察 | Ctrl + 滚轮滚转";
            overlayWarningLogged = false;
        }
        catch (Exception exception)
        {
            HideOverlay();
            if (!overlayWarningLogged)
            {
                overlayWarningLogged = true;
                Plugin.Log.LogWarning(
                    $"Control mode overlay was not displayed: {exception.Message}");
            }
        }
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null || displayCamera == null || projectionRoot == null)
        {
            return;
        }

        overlayRoot = new GameObject("MalodyVThemeLoader Control Overlay");
        overlayRoot.transform.SetParent(projectionRoot.transform, false);
        overlayRoot.layer = ProjectionLayer;
        overlayRoot.hideFlags = HideFlags.HideAndDontSave;
        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = displayCamera;
        canvas.planeDistance = 0.2f;
        canvas.sortingOrder = short.MaxValue;
        var scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textObject = new GameObject("Camera Parameters");
        textObject.transform.SetParent(overlayRoot.transform, false);
        textObject.layer = ProjectionLayer;
        overlayText = textObject.AddComponent<Text>();
        overlayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        overlayText.fontSize = 18;
        overlayText.alignment = TextAnchor.UpperLeft;
        overlayText.color = new Color(1f, 1f, 1f, 0.95f);
        overlayText.raycastTarget = false;
        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        var rect = overlayText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(18f, -18f);
        rect.sizeDelta = new Vector2(800f, 145f);
        overlayRoot.SetActive(false);
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SetControlInputActive(false);
        }
    }

    private void OnDestroy()
    {
        SetControlInputActive(false);
        if (captureCoroutine != null)
        {
            StopCoroutine(captureCoroutine);
            captureCoroutine = null;
        }

        if (projectionRoot != null)
        {
            UnityEngine.Object.Destroy(projectionRoot);
            projectionRoot = null;
        }

        capturedFrameRoot = null;
        screenQuad = null;
        backgroundQuad = null;
        topOverlayRoot = null;
        topOverlayImage = null;
        displayCamera = null;
        overlayRoot = null;
        overlayText = null;

        if (captureTexture != null)
        {
            captureTexture.Release();
            UnityEngine.Object.Destroy(captureTexture);
            captureTexture = null;
        }

        if (backgroundTexture != null)
        {
            UnityEngine.Object.Destroy(backgroundTexture);
            backgroundTexture = null;
        }

        if (topTexture != null)
        {
            UnityEngine.Object.Destroy(topTexture);
            topTexture = null;
        }

        if (screenMaterial != null)
        {
            UnityEngine.Object.Destroy(screenMaterial);
            screenMaterial = null;
        }

        if (backgroundMaterial != null)
        {
            UnityEngine.Object.Destroy(backgroundMaterial);
            backgroundMaterial = null;
        }
    }

    private static float ToSignedDegrees(float value)
    {
        return value > 180f ? value - 360f : value;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    private readonly record struct ControlInput(
        float Horizontal,
        float Forward,
        float PitchDegrees,
        float YawDegrees,
        float Scroll);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
