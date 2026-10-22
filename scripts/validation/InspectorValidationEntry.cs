using System;
using System.IO;
using UnityEditor;

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
    private static void Check()
    {
        var result = Path.Combine(Output, "result.json");
        if (File.Exists(result)) { var text = File.ReadAllText(result); UnityEngine.Debug.Log(text); EditorApplication.Exit(text.Contains("\"passed\": true") ? 0 : 1); }
        else if (EditorApplication.timeSinceStartup > deadline) { UnityEngine.Debug.LogError("Inspector validation timed out."); EditorApplication.Exit(1); }
    }
}
