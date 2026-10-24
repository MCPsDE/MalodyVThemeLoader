using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

// Keep the game's Graphic and event ordering. Extending its raycast padding
// does not move the RectTransform, its layout, or the displayed character.
internal sealed class Live2dClickSurface : IDisposable
{
    private readonly Graphic graphic;
    private readonly Transform model;
    private readonly Bounds bounds;
    private readonly Vector4 originalPadding;
    private Vector4 appliedPadding;
    private bool changed;

    public Live2dClickSurface(Graphic graphic, Transform model, Bounds bounds)
    {
        this.graphic = graphic; this.model = model; this.bounds = bounds;
        originalPadding = graphic.raycastPadding;
    }

    public void Refresh()
    {
        if (graphic == null || model == null || graphic.canvas == null) { Restore(); return; }
        var canvas = graphic.canvas;
        var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var modelCamera = Camera.main;
        if (modelCamera == null) modelCamera = eventCamera;
        if (modelCamera == null) { Restore(); return; }
        var rect = graphic.rectTransform;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var corner = 0; corner < 8; corner++)
        {
            var local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
            var screen = modelCamera.WorldToScreenPoint(model.TransformPoint(local));
            if (screen.z <= modelCamera.nearClipPlane || !float.IsFinite(screen.x) || !float.IsFinite(screen.y)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(screen.x, screen.y), eventCamera, out var point)
                || !float.IsFinite(point.x) || !float.IsFinite(point.y)) { Restore(); return; }
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        var area = rect.rect;
        // Negative padding expands an edge. Preserve any existing extension
        // and leave the original (possibly deliberately inset) region intact.
        appliedPadding = new Vector4(Mathf.Min(originalPadding.x, min.x - area.xMin),
            Mathf.Min(originalPadding.y, min.y - area.yMin),
            Mathf.Min(originalPadding.z, area.xMax - max.x),
            Mathf.Min(originalPadding.w, area.yMax - max.y));
        graphic.raycastPadding = appliedPadding;
        changed = true;
    }

    // The normalized coordinates are shared with the manager's preview. Use
    // the camera rendering the model, also when its UI receiver is an overlay.
    public static bool TryModelPoint(Transform model, Bounds bounds, Camera? fallbackCamera,
        Vector2 screen, out Vector2 point)
    {
        point = default;
        var camera = Camera.main;
        if (camera == null) camera = fallbackCamera;
        if (camera == null || model == null || bounds.size.x <= .001f || bounds.size.y <= .001f) return false;
        var plane = new Plane(model.forward, model.position);
        var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
        if (!plane.Raycast(ray, out var distance)) return false;
        var local = model.InverseTransformPoint(ray.GetPoint(distance));
        point = new Vector2((local.x - bounds.min.x) / bounds.size.x, (bounds.max.y - local.y) / bounds.size.y);
        return float.IsFinite(point.x) && float.IsFinite(point.y);
    }

    private void Restore()
    {
        if (changed && graphic != null && graphic.raycastPadding == appliedPadding)
            graphic.raycastPadding = originalPadding;
        changed = false;
    }

    public void Dispose() => Restore();
}
