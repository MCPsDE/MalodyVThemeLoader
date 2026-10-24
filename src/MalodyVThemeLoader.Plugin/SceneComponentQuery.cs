using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

// Query live presentation scenes directly. A loaded editor note scene can
// contain tens of thousands of components and must never enter this scan.
internal static class SceneComponentQuery
{
    // Keep saved editor rules readable; exclude note pools at the runtime
    // discovery boundary instead of rejecting the entire theme manifest.
    internal static bool IsAllowedScene(string sceneName) => ThemeResources.IsAllowedScene(sceneName)
        && !sceneName.StartsWith("EditMode", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<T> Find<T>(string? sceneName = null) where T : Component
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded || !IsAllowedScene(scene.name)
                || sceneName != null && !string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null || SceneNodeLocator.IsOwn(root.transform)) continue;
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    if (component != null) yield return component;
            }
        }
    }
}
