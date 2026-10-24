using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

public static class PresentationBuild
{
    public static void Run()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 640; PlayerSettings.defaultScreenHeight = 480;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = Rect("CanvasForegroundDPI", null); canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var panel = Rect("LeftPanel", canvas);
        var entrance = Rect("EntranceIcon", panel); entrance.sizeDelta = new Vector2(274, 81);
        var animator = entrance.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/entranceIcon.controller");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var mask = Rect("mask", entrance); mask.anchorMin = Vector2.zero; mask.anchorMax = new Vector2(0, 1); mask.sizeDelta = Vector2.zero;
        mask.gameObject.AddComponent<RectMask2D>();
        var tip = Rect("tip", mask); tip.anchorMin = Vector2.zero; tip.anchorMax = Vector2.one; tip.sizeDelta = Vector2.zero;
        tip.gameObject.AddComponent<Image>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/SceneSong.unity");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "player"); Directory.CreateDirectory(directory);
        var build = BuildPipeline.BuildPlayer(new[] { "Assets/SceneSong.unity" }, Path.Combine(directory, "PresentationProbe.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        EditorApplication.Exit(build.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }
}
