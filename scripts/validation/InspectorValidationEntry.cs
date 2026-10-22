using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

[InitializeOnLoad]
public static class InspectorValidationEntry
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MALODY_INSPECTOR_OUTPUT");
    private static double deadline;
    static InspectorValidationEntry()
    {
        if (string.IsNullOrEmpty(Output)) return;
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update += Check;
    }
    public static void Run() { EditorApplication.EnterPlaymode(); }
    public static void BuildPlayer()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
        PlayerSettings.defaultScreenWidth = 640; PlayerSettings.defaultScreenHeight = 480; PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/OverlayValidation.unity");
        var directory = Path.Combine(Output, "player"); Directory.CreateDirectory(directory);
        var build = BuildPipeline.BuildPlayer(new[] { "Assets/OverlayValidation.unity" }, Path.Combine(directory, "InspectorOverlayValidation.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        EditorApplication.Exit(build.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    private static void Check()
    {
        var result = Path.Combine(Output, "result.json");
        if (File.Exists(result)) { var text = File.ReadAllText(result); UnityEngine.Debug.Log(text); EditorApplication.Exit(text.Contains("\"passed\": true") ? 0 : 1); }
        else if (EditorApplication.timeSinceStartup > deadline) { UnityEngine.Debug.LogError("Inspector validation timed out."); EditorApplication.Exit(1); }
    }
}
