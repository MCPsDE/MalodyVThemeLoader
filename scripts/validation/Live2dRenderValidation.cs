using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using MalodyVThemeLoader.Plugin;
using UnityEditor;
using UnityEngine;

public static class Live2dRenderValidation
{
    [Serializable] private class References { public string Moc; public string[] Textures; }
    [Serializable] private class ModelFile { public References FileReferences; }
    [Serializable] private class CurveValue { public string target; public string id; public float value; }
    [Serializable] private class Pose { public CurveValue[] values; }
    [Serializable] private class PoseSet { public Pose[] poses; }

    private static void ApplyPose(CubismVisual visual, Pose pose)
    {
        var parameters = visual.Model.Parameters.ToDictionary(p => p.Id);
        var parts = visual.Model.Parts.ToDictionary(p => p.Id);
        foreach (var parameter in parameters.Values) parameter.Value = parameter.DefaultValue;
        foreach (var part in parts.Values) part.Opacity = 1;
        var controller = visual.Model.GetComponent<CubismRenderController>();
        controller.Opacity = 1;
        foreach (var value in pose.values)
            if (value.target == "Parameter" && parameters.TryGetValue(value.id, out var parameter))
                parameter.Value = Mathf.Clamp(value.value, parameter.MinimumValue, parameter.MaximumValue);
            else if (value.target == "PartOpacity" && parts.TryGetValue(value.id, out var part)) part.Opacity = Mathf.Clamp01(value.value);
            else if (value.target == "Model" && value.id == "Opacity") controller.Opacity = Mathf.Clamp01(value.value);
    }

    private static void Refresh(CubismVisual visual)
    {
        CubismVisualFactory.Show(visual);
        visual.Model.GetComponent<CubismRenderController>().OnLateUpdate();
        var mask = visual.Model.GetComponent<CubismMaskController>();
        if (mask != null) mask.OnLateUpdate();
        foreach (var buffer in Resources.FindObjectsOfTypeAll<CubismMaskCommandBuffer>()) buffer.SendMessage("LateUpdate");
    }

    public static void Run()
    {
        try
        {
            var source = Environment.GetEnvironmentVariable("MALODY_LIVE2D_RENDER_SAMPLE");
            var output = Environment.GetEnvironmentVariable("MALODY_LIVE2D_RENDER_OUTPUT");
            var modelFile = Directory.GetFiles(source, "*.model3.json")[0];
            var poses = JsonUtility.FromJson<PoseSet>(File.ReadAllText(Path.Combine(output, "poses.json"))).poses;
            var data = JsonUtility.FromJson<ModelFile>(File.ReadAllText(modelFile));
            var textures = new Texture2D[data.FileReferences.Textures.Length];
            for (var i = 0; i < textures.Length; i++)
            {
                textures[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!textures[i].LoadImage(File.ReadAllBytes(Path.Combine(source, data.FileReferences.Textures[i]))))
                    throw new Exception("Texture decode failed.");
            }
            using (var visual = CubismVisualFactory.Create(File.ReadAllBytes(Path.Combine(source, data.FileReferences.Moc)), textures))
            {
                var parent = new GameObject("ValidationParent");
                CubismVisualFactory.Fit(visual, parent.transform, new Bounds(Vector3.zero, new Vector3(3.4f, 4.5f, 0)), 0, 1, 0, 0);
                ApplyPose(visual, poses[0]); Refresh(visual);
                var camera = new GameObject("ValidationCamera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 2.6f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
                var target = new RenderTexture(720, 900, 24);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var bitmap = new Texture2D(720, 900, TextureFormat.RGB24, false);
                bitmap.ReadPixels(new Rect(0, 0, 720, 900), 0, 0); bitmap.Apply();
                var idle = bitmap.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(output, "Murasame.png"), idle);
                var pixels = bitmap.GetPixels32();
                var foreground = 0;
                foreach (var p in pixels) if (Math.Abs(p.r - p.g) > 15 || Math.Abs(p.g - p.b) > 15) foreground++;
                if (foreground < 10000) throw new Exception("The rendered model is empty: " + foreground);
                ApplyPose(visual, poses[1]); Refresh(visual); camera.Render();
                RenderTexture.active = target;
                bitmap.ReadPixels(new Rect(0, 0, 720, 900), 0, 0); bitmap.Apply();
                var tap = bitmap.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(output, "Murasame-tap.png"), tap);
                using (var hash = SHA256.Create())
                    if (Convert.ToBase64String(hash.ComputeHash(idle)) == Convert.ToBase64String(hash.ComputeHash(tap)))
                        throw new Exception("Idle and tap motion produced identical renders.");
                File.WriteAllText(Path.Combine(output, "result.json"), "{\"rendered\":true,\"drawables\":" + visual.Model.Drawables.Length
                    + ",\"parameters\":" + visual.Model.Parameters.Length + ",\"foregroundPixels\":" + foreground + ",\"motionFramesDifferent\":true}");
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(parent);
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
