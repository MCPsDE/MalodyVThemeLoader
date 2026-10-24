using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor.U2D;

public static class ProbeBuild
{
    public static void Run()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 640; PlayerSettings.defaultScreenHeight = 480;
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
        Directory.CreateDirectory("Assets/ResultRanks"); Directory.CreateDirectory("Assets/Resources");
        for (var level = 0; level < 5; level++) for (var rank = 1; rank <= 5; rank++)
        {
            var texture = new Texture2D(4, 4);
            File.WriteAllBytes($"Assets/ResultRanks/rank_score_{level}{rank}.png", texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles("Assets/ResultRanks", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        const string atlasPath = "Assets/Resources/ResultRankProbe.spriteatlas";
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null) { atlas = new SpriteAtlas(); AssetDatabase.CreateAsset(atlas, atlasPath); }
        atlas.Remove(atlas.GetPackables()); atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/ResultRanks") });
        atlas.SetIncludeInBuild(true); AssetDatabase.SaveAssets();
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, BuildTarget.StandaloneWindows64);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Probe.unity");
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "player"); Directory.CreateDirectory(directory);
        var result = BuildPipeline.BuildPlayer(new[] { "Assets/Probe.unity" }, Path.Combine(directory, "InspectorProbe.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
