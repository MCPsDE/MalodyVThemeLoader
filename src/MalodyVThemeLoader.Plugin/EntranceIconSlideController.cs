using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

internal static class EntranceIconSlideController
{
    private const string CanvasName = "CanvasForegroundDPI";
    private const string PanelName = "LeftPanel";
    private const string MaskName = "mask";

    private static readonly List<Binding> bindings = new();

    internal static void Bind(Scene scene)
    {
        bindings.Clear();
        if (!scene.IsValid() || !string.Equals(scene.name, "SceneSong", StringComparison.Ordinal)) return;

        Transform? canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, CanvasName, StringComparison.Ordinal)) continue;
            if (canvas != null) return;
            canvas = root.transform;
        }
        var panel = canvas == null ? null : UniqueChild(canvas, PanelName);
        if (panel == null) return;
        foreach (var name in BuiltInOverlayVisibility.EntranceNames)
        {
            var entrance = UniqueChild(panel, name);
            // Transform.Find returns a base CLR wrapper in IL2CPP. Managed
            // `as` / pattern casts cannot discover its native RectTransform.
            var mask = entrance == null ? null : UniqueChild(entrance, MaskName)?.GetComponent<RectTransform>();
            var animator = entrance?.GetComponent<Animator>();
            if (mask == null || animator == null) continue;
            var parent = mask.parent?.GetComponent<RectTransform>();
            if (parent == null) continue;

            var progress = Mathf.Clamp01(mask.anchorMax.x);
            var current = new Binding(animator, mask, progress);
            bindings.Add(current);
            Apply(current, parent.rect.width);
        }
    }

    private static Transform? UniqueChild(Transform parent, string name)
    {
        Transform? found = null;
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name != name) continue;
            if (found != null) return null;
            found = child;
        }
        return found;
    }

    internal static void Tick()
    {
        for (var index = bindings.Count - 1; index >= 0; index--)
        {
            var current = bindings[index];
            if (current.Animator == null || current.Mask == null) { bindings.RemoveAt(index); continue; }
            Tick(current);
        }
    }

    private static void Tick(Binding current)
    {
        var parent = current.Mask.parent?.GetComponent<RectTransform>();
        if (parent == null) return;

        if (current.Animator.isActiveAndEnabled)
        {
            var state = current.Animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("hover"))
            {
                current.Progress = state.normalizedTime >= 1f
                    ? 1f
                    : Mathf.Clamp01(current.Mask.anchorMax.x);
            }
            else if (state.IsName("leave"))
            {
                current.Progress = state.normalizedTime >= 1f
                    ? 0f
                    : Mathf.Clamp01(current.Mask.anchorMax.x);
            }
        }

        Apply(current, parent.rect.width);
    }

    private static void Apply(Binding target, float width)
    {
        if (width <= 0f || float.IsNaN(width) || float.IsInfinity(width)) return;

        var mask = target.Mask;
        var anchorMin = mask.anchorMin;
        var anchorMax = mask.anchorMax;
        var size = mask.sizeDelta;
        mask.anchorMin = new Vector2(0f, anchorMin.y);
        mask.anchorMax = new Vector2(1f, anchorMax.y);
        mask.sizeDelta = new Vector2(0f, size.y);

        var position = mask.anchoredPosition;
        position.x = -width * (1f - target.Progress);
        mask.anchoredPosition = position;
    }

    private sealed class Binding
    {
        internal Binding(Animator animator, RectTransform mask, float progress)
        {
            Animator = animator;
            Mask = mask;
            Progress = progress;
        }

        internal Animator Animator { get; }
        internal RectTransform Mask { get; }
        internal float Progress { get; set; }
    }
}
