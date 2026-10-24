using UnityEngine;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal static class BuiltInOverlayVisibility
{
    internal static readonly string[] EntranceNames = { "EntranceIcon", "EntranceIcon (1)", "EntranceIcon (2)", "EntranceIcon (3)", "EntranceIcon (4)", "EntranceIcon (5)" };
    private static readonly Dictionary<string, string[]> EntranceTips = EntranceNames.ToDictionary(name => name,
        name => new[] { "CanvasForegroundDPI", "LeftPanel", name, "mask", "tip" }, StringComparer.Ordinal);
    private static readonly string[] ResultMask = { "Canvas", "bgmask" };
    private static readonly string[] ResultBottom = { "Canvas", "bottom" };
    private static readonly string[] ResultBottomMp = { "Canvas", "bottom_mp" };
    private static readonly string[] ResultBottomWatch = { "Canvas", "bottom_watch" };
    private static readonly string[] ResultArtwork = { "Canvas", "Image" };
    private static readonly string[] ResultCamera = { "Main Camera" };

    internal static bool NormalizeResultBackground(Image image)
    {
        if (image == null || image.gameObject.scene.name != "SceneResult" || image.name != "Image"
            || !MatchesUniquePath(image.transform, ResultArtwork)) return false;
        var color = image.color;
        if (color.a == 1f) return false;
        // The scene's .447 tint mixes the camera's blue clear color into even
        // an opaque replacement PNG. Keep the artwork's own alpha and RGB.
        color.a = 1; image.color = color; return true;
    }

    internal static bool ClearResultCamera(Camera camera)
    {
        if (camera == null || camera.gameObject.scene.name != "SceneResult" || camera.name != "Main Camera"
            || camera.clearFlags != CameraClearFlags.SolidColor || !MatchesUniquePath(camera.transform, ResultCamera)) return false;
        if (camera.backgroundColor == Color.clear) return false;
        camera.backgroundColor = Color.clear;
        return true;
    }

    internal static bool Hide(Image image)
    {
        if (image == null) return false;
        var scene = image.gameObject.scene;
        if (!scene.IsValid()) return false;
        var path = (scene.name, image.name) switch
        {
            ("SceneSong", "tip") => EntrancePath(image.transform),
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

    private static string[]? EntrancePath(Transform tip)
    {
        var entrance = tip.parent?.parent;
        return entrance != null && EntranceTips.TryGetValue(entrance.name, out var path) ? path : null;
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
