using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

public static class ProbeBuild
{
    public static void Run()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 640; PlayerSettings.defaultScreenHeight = 480;
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Probe.unity");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "player"); Directory.CreateDirectory(directory);
        var result = BuildPipeline.BuildPlayer(new[] { "Assets/Probe.unity" }, Path.Combine(directory, "InspectorProbe.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
