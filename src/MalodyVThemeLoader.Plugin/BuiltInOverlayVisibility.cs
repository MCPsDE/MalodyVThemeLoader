using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal static class BuiltInOverlayVisibility
{
    private static readonly string[] EntranceTip = { "CanvasForegroundDPI", "LeftPanel", "EntranceIcon", "mask", "tip" };
    private static readonly string[] ResultMask = { "Canvas", "bgmask" };
    private static readonly string[] ResultBottom = { "Canvas", "bottom" };
    private static readonly string[] ResultBottomMp = { "Canvas", "bottom_mp" };
    private static readonly string[] ResultBottomWatch = { "Canvas", "bottom_watch" };

    internal static bool Hide(Image image)
    {
        if (image == null) return false;
        var scene = image.gameObject.scene;
        if (!scene.IsValid()) return false;
        var path = (scene.name, image.name) switch
        {
            ("SceneSong", "tip") => EntranceTip,
            ("SceneResult", "bgmask") => ResultMask,
            ("SceneResult", "bottom") => ResultBottom,
            ("SceneResult", "bottom_mp") => ResultBottomMp,
            ("SceneResult", "bottom_watch") => ResultBottomWatch,
            _ => null
        };
        if (path == null || !MatchesUniquePath(image.transform, path)) return false;

        var color = image.color;
        if (color.a == 0f) return false;
        // Only the original Graphic becomes transparent. Child backgrounds,
        // text, RectMask2D geometry and pointer handling remain independent.
        color.a = 0f;
        image.color = color;
        return true;
    }

    private static bool MatchesUniquePath(Transform target, string[] path)
    {
        Transform? current = target;
        for (var index = path.Length - 1; index >= 0; index--)
        {
            if (current == null || current.name != path[index]) return false;
            var parent = current.parent;
            var matches = 0;
            if (parent != null)
            {
                for (var sibling = 0; sibling < parent.childCount; sibling++)
                    if (parent.GetChild(sibling).name == path[index]) matches++;
            }
            else
            {
                foreach (var root in target.gameObject.scene.GetRootGameObjects())
                    if (root.name == path[index]) matches++;
            }
            if (matches != 1) return false;
            current = parent;
        }
        return current == null;
    }
}
