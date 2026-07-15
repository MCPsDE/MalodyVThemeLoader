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
    private const int ProjectionLayer = 31;
    private const float SettingsPollInterval = 0.5f;
    private const float ProjectionDistance = 8f;
    private const float BackgroundDistance = 80f;
    private const float CameraMoveSpeed = 5.5f;
    private const float MouseLookSensitivity = 2.4f;
    private const float RollStep = 7.5f;
    private const float MaxPitchDegrees = 85f;
    private const float MaxLateralDistance = 20f;
    private const float MaxVerticalDistance = 14f;
    private const float MaxBackoff = 24f;
    private const float NearPlaneMargin = 0.08f;

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

    private Vector3 cameraPosition;
    private float pitchDegrees;
    private float yawDegrees;
    private float rollDegrees;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLockMode;

    private GameObject? projectionRoot;
    private GameObject? capturedFrameRoot;
    private GameObject? screenQuad;
    private GameObject? backgroundQuad;
    private Camera? displayCamera;
    private RenderTexture? captureTexture;
    private Material? screenMaterial;
    private Material? backgroundMaterial;
    private Texture2D? backgroundTexture;
    private string loadedBackgroundPath = "";
    private DateTime loadedBackgroundWriteTimeUtc = DateTime.MinValue;
    private int captureWidth;
    private int captureHeight;

    private GameObject? overlayRoot;
    private Text? overlayText;

    public ControlModeController(IntPtr pointer) : base(pointer)
    {
    }

    public void Initialize()
    {
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
        pitchDegrees = Math.Clamp(
            pitchDegrees - input.MouseY * MouseLookSensitivity,
            -MaxPitchDegrees,
            MaxPitchDegrees);
        yawDegrees = WrapDegrees(
            yawDegrees + input.MouseX * MouseLookSensitivity);
        if (Math.Abs(input.Scroll) > 0.001f)
        {
            rollDegrees = WrapDegrees(rollDegrees + input.Scroll * RollStep);
        }

        var cameraRotation = Quaternion.Euler(
            pitchDegrees,
            yawDegrees,
            rollDegrees);
        var movement = cameraRotation
                       * new Vector3(input.Horizontal, 0f, input.Forward);
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        cameraPosition += movement * CameraMoveSpeed * Time.unscaledDeltaTime;
        cameraPosition.x = Math.Clamp(
            cameraPosition.x,
            -MaxLateralDistance,
            MaxLateralDistance);
        cameraPosition.y = Math.Clamp(
            cameraPosition.y,
            -MaxVerticalDistance,
            MaxVerticalDistance);
        cameraPosition.z = Math.Clamp(
            cameraPosition.z,
            -MaxBackoff,
            ProjectionDistance - NearPlaneMargin);
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
        return new ControlInput(
            Axis(KeyCode.D, KeyCode.A),
            Axis(KeyCode.W, KeyCode.S),
            ReadAxis("Mouse X"),
            ReadAxis("Mouse Y"),
            ReadScroll());
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
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = savedCursorLockMode;
                Cursor.visible = savedCursorVisible;
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

        projectionRoot = new GameObject("MalodyVThemeLoader Full Frame Projection");
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
        capturedFrameRoot.transform.localPosition =
            new Vector3(0f, 0f, ProjectionDistance);

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

        LoadBackgroundTextureIfChanged();
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
        displayCamera.transform.localRotation = Quaternion.Euler(
            pitchDegrees,
            yawDegrees,
            rollDegrees);

        capturedFrameRoot.transform.localPosition =
            new Vector3(0f, 0f, ProjectionDistance);
        capturedFrameRoot.transform.localRotation = Quaternion.identity;
        var frameHeight =
            Mathf.Tan(displayCamera.fieldOfView * Mathf.Deg2Rad * 0.5f)
            * ProjectionDistance
            * 2f;
        screenQuad.transform.localPosition = Vector3.zero;
        screenQuad.transform.localRotation = Quaternion.identity;
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
            overlayText.text =
                "控制模式相机\n"
                + $"Position: x {cameraPosition.x:F2}, y {cameraPosition.y:F2}, "
                + $"z {cameraPosition.z:F2}\n"
                + $"View: pitch {pitchDegrees:F1}, yaw {yawDegrees:F1}, "
                + $"roll {rollDegrees:F1}\n"
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

    private static float WrapDegrees(float value)
    {
        while (value > 180f)
        {
            value -= 360f;
        }

        while (value < -180f)
        {
            value += 360f;
        }

        return value;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private readonly record struct ControlInput(
        float Horizontal,
        float Forward,
        float MouseX,
        float MouseY,
        float Scroll);
}
