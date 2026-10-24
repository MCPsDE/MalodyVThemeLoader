using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TextOpeningBuild
{
    public static void Run()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);
        PlayerSettings.runInBackground = true; PlayerSettings.defaultScreenWidth = 800; PlayerSettings.defaultScreenHeight = 600;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        Directory.CreateDirectory("Assets/Resources");
        const string source = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        if (!File.Exists("Assets/Resources/ProbeFont.asset") && !AssetDatabase.CopyAsset(source, "Assets/Resources/ProbeFont.asset"))
            throw new System.InvalidOperationException("TMP fixture font was not imported.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Probe.unity");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "player"); Directory.CreateDirectory(directory);
        var result = BuildPipeline.BuildPlayer(new[] { "Assets/Probe.unity" }, Path.Combine(directory, "TextOpeningProbe.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
