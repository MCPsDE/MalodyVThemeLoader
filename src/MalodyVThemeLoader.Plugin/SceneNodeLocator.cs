using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MalodyVThemeLoader.Plugin;

internal sealed record SceneNode(Transform Transform, InspectorNode Info);

internal static class SceneNodeLocator
{
    private static readonly Il2CppStructArray<Vector3> cornerBuffer = new(4);
    internal const string OwnPrefix = "ThemeLoader.";
    internal static List<SceneNode> Collect(ThemeAssetStore? assets, out bool truncated)
    {
        var result = new List<SceneNode>(); var limit = false;
        var roots = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t != null && t.parent == null && t.gameObject.scene.IsValid()
            && SceneThemeApplier.IsAllowedScene(t.gameObject.scene.name) && !IsOwn(t)).ToArray();
        void Visit(Transform t, int parent, string address, string hierarchy, int depth)
        {
            if (depth > 96 || result.Count >= 20000) { limit = true; return; }
            if (IsOwn(t)) return;
            var rect = t.GetComponent<RectTransform>(); var canvas = t.GetComponentInParent<Canvas>();
            var info = new InspectorNode { Id = t.GetInstanceID(), ParentId = parent, Name = t.name, Scene = t.gameObject.scene.name,
                Address = address, Hierarchy = hierarchy, Active = t.gameObject.activeInHierarchy,
                CanDecorate = rect != null && canvas != null, Width = rect != null ? rect.rect.width : 0, Height = rect != null ? rect.rect.height : 0 };
            var image = t.GetComponent<Image>(); var raw = t.GetComponent<RawImage>(); var renderer = t.GetComponent<SpriteRenderer>();
            ImageCatalogEntry? entry = null;
            if (image != null)
            {
                info.Component = "Image"; var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                if (sprite != null) entry = assets?.Identify(sprite.name, sprite, null, info.Scene, hierarchy, info.Component);
            }
            else if (raw != null)
            {
                info.Component = "RawImage"; if (raw.texture != null) entry = assets?.Identify(raw.texture.name, null, raw.texture, info.Scene, hierarchy, info.Component);
            }
            else if (renderer != null)
            {
                info.Component = "SpriteRenderer"; if (renderer.sprite != null) entry = assets?.Identify(renderer.sprite.name, renderer.sprite, null, info.Scene, hierarchy, info.Component);
            }
            else info.Component = t.GetComponent<Graphic>() != null ? "Text / Graphic" : rect != null ? "RectTransform" : "Transform";
            info.LogicalId = entry?.LogicalId ?? "";
            result.Add(new SceneNode(t, info));
            var children = new List<Transform>();
            for (var i = 0; i < t.childCount; i++) { var c = t.GetChild(i); if (!IsOwn(c)) children.Add(c); }
            var counts = children.GroupBy(c => c.name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in children)
            {
                occurrences.TryGetValue(c.name, out var index); occurrences[c.name] = index + 1;
                Visit(c, info.Id, address + "/" + SceneNodeAddress.Segment(c.name, index, counts[c.name]), hierarchy + "/" + c.name, depth + 1);
            }
        }
        foreach (var group in roots.GroupBy(t => t.gameObject.scene.handle))
        {
            var names = group.GroupBy(t => t.name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.OrderBy(t => t.GetSiblingIndex()).ToList(), StringComparer.Ordinal);
            foreach (var t in group.OrderBy(t => t.GetSiblingIndex()))
                Visit(t, 0, SceneNodeAddress.Segment(t.name, names[t.name].IndexOf(t), names[t.name].Count), t.name, 0);
        }
        truncated = limit; return result;
    }
    internal static bool IsOwnName(string name) => name.StartsWith(OwnPrefix, StringComparison.Ordinal) || name == "BepInEx_Manager";
    internal static bool IsOwn(Transform t) => IsOwnName(t.name);
    internal static string AddressOf(Transform target) => new SceneAddressIndex().AddressOf(target);

    internal static Camera? CameraFor(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        return canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera ?? Camera.main;
    }
    internal static Vector2[]? Corners(SceneNode node)
    {
        if (node.Transform == null || !node.Transform.gameObject.activeInHierarchy || !SceneThemeApplier.IsAllowedScene(node.Transform.gameObject.scene.name)) return null;
        var rect = node.Transform.GetComponent<RectTransform>();
        if (rect == null) return null;
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null || !canvas.isActiveAndEnabled || rect.rect.width <= 0 || rect.rect.height <= 0) return null;
        var camera = CameraFor(rect);
        var world = cornerBuffer; rect.GetWorldCorners(world);
        var points = new Vector2[4];
        for (var i = 0; i < 4; i++) points[i] = RectTransformUtility.WorldToScreenPoint(camera, world[i]);
        if (points.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y))) return null;
        // Debug bounds describe active layout even without a visible image.
        // Picking applies the additional visibility filters below.
        return points;
    }
    internal static bool Hit(SceneNode node, Vector2 position)
    {
        var rect = node.Transform.GetComponent<RectTransform>();
        if (rect == null || Corners(node) == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, position, CameraFor(rect))) return false;
        foreach (var cg in node.Transform.GetComponentsInParent<CanvasGroup>()) if (cg.alpha <= .001f) return false;
        var graphic = node.Transform.GetComponent<Graphic>();
        var decoration = node.Transform.childCount > 0 ? node.Transform.GetChild(0) : null;
        var decorated = decoration != null && decoration.name == OwnPrefix + "Decoration" && decoration.gameObject.activeInHierarchy;
        if (!decorated && graphic != null && (!graphic.enabled || graphic.color.a <= .001f)) return false;
        foreach (var mask in node.Transform.GetComponentsInParent<RectMask2D>())
            if (mask.isActiveAndEnabled && !mask.IsRaycastLocationValid(position, CameraFor(mask.rectTransform))) return false;
        foreach (var mask in node.Transform.GetComponentsInParent<Mask>())
            if (mask.isActiveAndEnabled && !mask.IsRaycastLocationValid(position, CameraFor(mask.rectTransform))) return false;
        return true;
    }
}
