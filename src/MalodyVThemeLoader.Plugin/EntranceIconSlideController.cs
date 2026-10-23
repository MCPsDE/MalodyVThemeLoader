using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

internal static class EntranceIconSlideController
{
    private const string CanvasName = "CanvasForegroundDPI";
    private const string PanelName = "LeftPanel";
    private const string EntranceName = "EntranceIcon";
    private const string MaskName = "mask";

    private static Binding? binding;

    internal static void Bind(Scene scene)
    {
        binding = null;
        if (!scene.IsValid() || !string.Equals(scene.name, "SceneSong", StringComparison.Ordinal)) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, CanvasName, StringComparison.Ordinal)) continue;

            var entrance = root.transform.Find(PanelName + "/" + EntranceName);
            var mask = entrance?.Find(MaskName) as RectTransform;
            var animator = entrance?.GetComponent<Animator>();
            if (mask == null || animator == null || mask.parent is not RectTransform parent) return;

            var progress = Mathf.Clamp01(mask.anchorMax.x);
            binding = new Binding(animator, mask, progress);
            Apply(binding, parent.rect.width);
            return;
        }
    }

    internal static void Tick()
    {
        var current = binding;
        if (current == null || current.Animator == null || current.Mask == null) return;
        if (current.Mask.parent is not RectTransform parent) return;

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
