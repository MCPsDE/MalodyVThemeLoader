using System.Runtime.InteropServices;
using BepInEx;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal sealed class SceneInspectorController : MonoBehaviour
{
    private readonly string runtimeId = Guid.NewGuid().ToString("N");
    private string loaderRoot = "";
    private InspectorControl control = new();
    private List<SceneNode> nodes = new();
    private long request;
    private long revision;
    private int selected;
    private string selectionOrigin = "";
    private float nextPoll;
    private float nextSnapshot;
    private bool middleWasDown;
    private Vector2 lastPick;
    private float lastPickTime;
    private int cycle;
    private GameObject? overlay;
    private Mesh? mesh;
    private Material? material;
    private CanvasRenderer? canvasRenderer;
    private readonly List<Mesh> meshBatches = new();
    private readonly List<CanvasRenderer> rendererBatches = new();
    private Font? font;
    private readonly List<Text> labels = new();
    private bool failed;
    private bool snapshotTruncated;
    internal static bool IsInspecting { get; private set; }
    public SceneInspectorController(IntPtr pointer) : base(pointer) { }
    public void Initialize()
    {
        loaderRoot = Path.Combine(Paths.GameRootPath, "ThemeLoader");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
    }
    private void Update()
    {
        try
        {
            var now = Time.realtimeSinceStartup;
            if (now >= nextPoll)
            {
                nextPoll = now + .25f;
                var next = InspectorFiles.Read<InspectorControl>(loaderRoot, InspectorFiles.ControlName) ?? new InspectorControl();
                if (next.Client != control.Client || next.IsLive && !control.IsLive) { failed = false; request = 0; selected = 0; revision = 0; }
                control = next;
            }
            IsInspecting = !failed && control.IsLive;
            if (!IsInspecting)
            {
                if (overlay != null) overlay.SetActive(false);
                middleWasDown = false; return;
            }
            if (now >= nextSnapshot)
            {
                nextSnapshot = now + .5f;
                nodes = SceneNodeLocator.Collect(ThemeRuntime.Current?.Assets, out snapshotTruncated);
                if (control.Runtime == runtimeId && control.SelectRequest != request)
                {
                    request = control.SelectRequest;
                    if (nodes.Any(n => n.Info.Id == control.SelectedId)) { selected = control.SelectedId; selectionOrigin = "editor"; revision++; }
                }
                if (!nodes.Any(n => n.Info.Id == selected)) selected = 0;
                foreach (var node in nodes)
                {
                    node.Info.HasBounds = SceneNodeLocator.Corners(node) != null;
                    node.Info.DecorationStatus = ThemeRuntime.Current?.Decorations.Status(node.Info.Scene, node.Info.Address) ?? "";
                }
                Publish(snapshotTruncated);
            }
            var down = Application.isFocused && (GetAsyncKeyState(4) & 0x8000) != 0;
            if (down && !middleWasDown && TryMouse(out var position)) Pick(position);
            middleWasDown = down;
        }
        catch (Exception e)
        {
            failed = true; IsInspecting = false; if (overlay != null) overlay.SetActive(false);
            Plugin.Log.LogWarning("Scene inspector stopped: " + e.Message);
        }
    }
    private void Publish(bool truncated)
    {
        InspectorFiles.Write(loaderRoot, InspectorFiles.SnapshotName, new InspectorSnapshot
        {
            Runtime = runtimeId, Client = control.Client, UpdatedUtc = DateTime.UtcNow, PluginVersion = MyPluginInfo.PLUGIN_VERSION,
            Theme = ThemeRuntime.Current?.Theme.ActiveKey ?? "", SelectedId = selected, SelectionRevision = revision, SelectionOrigin = selectionOrigin,
            Nodes = nodes.Select(n => n.Info).ToList(), Truncated = truncated
        });
    }
    private void Pick(Vector2 position)
    {
        if (!ControlModeController.TryMapInspectorPoint(position, out position)) return;
        var hits = nodes.Where(n => n.Transform != null && SceneNodeLocator.Hit(n, position))
            .OrderByDescending(n => CanvasOrder(n.Transform)).ThenByDescending(n => GraphicDepth(n.Transform))
            .ThenByDescending(n => n.Info.Address.Count(c => c == '/')).ThenByDescending(n => n.Transform.GetSiblingIndex()).ToList();
        if (hits.Count == 0) return;
        cycle = (position - lastPick).sqrMagnitude < 64 && Time.realtimeSinceStartup - lastPickTime < 1.5f ? cycle + 1 : 0;
        lastPick = position; lastPickTime = Time.realtimeSinceStartup;
        selected = hits[cycle % hits.Count].Info.Id; selectionOrigin = "game"; revision++; Publish(snapshotTruncated);
    }
    private static int CanvasOrder(Transform t)
    {
        var c = t.GetComponentInParent<Canvas>();
        while (c != null && !c.overrideSorting)
        { var parent = c.transform.parent; var inherited = parent != null ? parent.GetComponentInParent<Canvas>() : null; if (inherited == null) break; c = inherited; }
        return c != null ? c.sortingOrder : int.MinValue;
    }
    private static int GraphicDepth(Transform t) { var g = t.GetComponent<Graphic>(); return g != null ? g.depth : -1; }
    private static bool TryMouse(out Vector2 point)
    {
        point = Vector2.zero; var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetCursorPos(out var native) || !ScreenToClient(window, ref native) || !GetClientRect(window, out var client)
            || client.Right <= 0 || client.Bottom <= 0) return false;
        point = new Vector2(native.X * (float)Screen.width / client.Right, Screen.height - native.Y * (float)Screen.height / client.Bottom);
        return point.x >= 0 && point.y >= 0 && point.x <= Screen.width && point.y <= Screen.height;
    }
    private void LateUpdate()
    {
        if (!IsInspecting) return;
        try { Draw(); }
        catch (Exception e) { failed = true; IsInspecting = false; if (overlay != null) overlay.SetActive(false); Plugin.Log.LogWarning("Inspector drawing stopped: " + e.Message); }
    }
    private void EnsureOverlay()
    {
        if (overlay != null) { overlay.SetActive(true); return; }
        overlay = new GameObject(SceneNodeLocator.OwnPrefix + "Inspector"); UnityEngine.Object.DontDestroyOnLoad(overlay);
        var canvas = overlay.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
        overlay.AddComponent<GraphicRaycaster>();
        var fenceObject = new GameObject(SceneNodeLocator.OwnPrefix + "MiddleClickFence"); var fenceRect = fenceObject.AddComponent<RectTransform>(); fenceRect.SetParent(overlay.transform, false);
        fenceRect.anchorMin = Vector2.zero; fenceRect.anchorMax = Vector2.one; fenceRect.offsetMin = fenceRect.offsetMax = Vector2.zero;
        var fence = fenceObject.AddComponent<InspectorInputFence>(); fence.color = Color.clear; fence.raycastTarget = true; fence.canvasRenderer.cullTransparentMesh = false;
        var drawing = new GameObject(SceneNodeLocator.OwnPrefix + "Bounds"); var rect = drawing.AddComponent<RectTransform>(); rect.SetParent(overlay.transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero;
        canvasRenderer = drawing.AddComponent<CanvasRenderer>();
        material = new Material(Shader.Find("UI/Default")); canvasRenderer.SetMaterial(material, Texture2D.whiteTexture);
        // CanvasRenderer consumes 16-bit UI indices. Larger meshes must be
        // split; using UInt32 produces corrupt triangles in Unity 2022.3.
        mesh = new Mesh { name = SceneNodeLocator.OwnPrefix + "Bounds", indexFormat = IndexFormat.UInt16 };
        meshBatches.Add(mesh); rendererBatches.Add(canvasRenderer);
        font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 13);
    }
    private void Draw()
    {
        EnsureOverlay();
        var vertices = new List<Vector3>(); var colors = new List<Color32>(); var indices = new List<int>(); var used = 0;
        foreach (var node in nodes)
        {
            if (node.Transform == null) continue;
            var points = SceneNodeLocator.Corners(node); if (points == null) continue;
            var clip = ClipFor(node); if (clip.width <= 0 || clip.height <= 0) continue;
            var left = points.Min(p => p.x); var right = points.Max(p => p.x); var bottom = points.Min(p => p.y); var top = points.Max(p => p.y);
            if (right < clip.xMin || left > clip.xMax || top < clip.yMin || bottom > clip.yMax) continue;
            var isSelected = node.Info.Id == selected;
            Color32 color = isSelected ? new Color32(255, 204, 64, 255) : node.Info.Component == "Image" ? new Color32(95, 170, 255, 200)
                : node.Info.Component == "RawImage" ? new Color32(200, 150, 255, 200) : new Color32(200, 200, 200, 150);
            if (control.ShowFrames || isSelected)
                for (var i = 0; i < 4; i++) Segment(points[i], points[(i + 1) % 4], clip, color, isSelected ? 2 : 1, vertices, colors, indices);
            if (!control.ShowNames && !isSelected) continue;
            if (used == labels.Count)
            {
                var item = new GameObject(SceneNodeLocator.OwnPrefix + "Name"); item.transform.SetParent(overlay!.transform, false);
                var text = item.AddComponent<Text>(); text.font = font; text.fontSize = 13; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow; text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.zero;
                text.rectTransform.pivot = new Vector2(0, 1); labels.Add(text);
            }
            var label = labels[used++]; label.gameObject.SetActive(true); label.text = node.Info.Name; label.color = color;
            label.rectTransform.anchoredPosition = new Vector2(Math.Clamp(left, clip.xMin, Math.Max(clip.xMin, clip.xMax - 80)), Math.Clamp(top, clip.yMin + 16, clip.yMax));
            label.rectTransform.sizeDelta = new Vector2(500, 20);
        }
        for (var i = used; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
        const int batchSize = 60000;
        var required = Math.Max(1, (vertices.Count + batchSize - 1) / batchSize);
        while (meshBatches.Count < required)
        {
            var go = new GameObject(SceneNodeLocator.OwnPrefix + "BoundsBatch"); var rect = go.AddComponent<RectTransform>(); rect.SetParent(overlay!.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            var renderer = go.AddComponent<CanvasRenderer>(); renderer.SetMaterial(material, Texture2D.whiteTexture); rendererBatches.Add(renderer);
            meshBatches.Add(new Mesh { name = SceneNodeLocator.OwnPrefix + "Bounds", indexFormat = IndexFormat.UInt16 });
        }
        for (var i = 0; i < meshBatches.Count; i++)
        {
            var batch = meshBatches[i]; batch.Clear(); var start = i * batchSize; var count = Math.Min(batchSize, vertices.Count - start);
            if (count > 0)
            {
                batch.vertices = vertices.GetRange(start, count).ToArray(); batch.colors32 = colors.GetRange(start, count).ToArray();
                batch.uv = Enumerable.Repeat(new Vector2(.5f, .5f), count).ToArray();
                batch.triangles = indices.GetRange(start / 4 * 6, count / 4 * 6).Select(index => index - start).ToArray(); batch.RecalculateBounds();
            }
            rendererBatches[i].SetMesh(batch);
        }
    }
    private static Rect ClipFor(SceneNode node)
    {
        var clip = new Rect(0, 0, Screen.width, Screen.height);
        foreach (var mask in node.Transform.GetComponentsInParent<RectMask2D>())
        {
            if (!mask.isActiveAndEnabled) continue;
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            mask.rectTransform.GetWorldCorners(corners); var camera = SceneNodeLocator.CameraFor(mask.rectTransform);
            var points = Enumerable.Range(0, 4).Select(i => RectTransformUtility.WorldToScreenPoint(camera, corners[i])).ToArray();
            clip = Rect.MinMaxRect(Math.Max(clip.xMin, points.Min(p => p.x)), Math.Max(clip.yMin, points.Min(p => p.y)),
                Math.Min(clip.xMax, points.Max(p => p.x)), Math.Min(clip.yMax, points.Max(p => p.y)));
        }
        return clip;
    }
    private static void Segment(Vector2 a, Vector2 b, Rect clip, Color32 color, float width, List<Vector3> vertices, List<Color32> colors, List<int> indices)
    {
        var delta = b - a; var enter = 0f; var exit = 1f;
        bool Bound(float p, float q)
        {
            if (Math.Abs(p) < .00001f) return q >= 0;
            var r = q / p; if (p < 0) { if (r > exit) return false; enter = Math.Max(enter, r); }
            else { if (r < enter) return false; exit = Math.Min(exit, r); } return true;
        }
        if (!Bound(-delta.x, a.x - clip.xMin) || !Bound(delta.x, clip.xMax - a.x) || !Bound(-delta.y, a.y - clip.yMin) || !Bound(delta.y, clip.yMax - a.y)) return;
        b = a + delta * exit; a += delta * enter; var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        var first = vertices.Count; vertices.Add(a - normal); vertices.Add(a + normal); vertices.Add(b + normal); vertices.Add(b - normal);
        for (var i = 0; i < 4; i++) colors.Add(color);
        indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
    }
    private void OnDestroy()
    {
        IsInspecting = false;
        if (overlay != null) UnityEngine.Object.Destroy(overlay); foreach (var batch in meshBatches) UnityEngine.Object.Destroy(batch);
        if (material != null) UnityEngine.Object.Destroy(material); if (font != null) UnityEngine.Object.Destroy(font);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
}
