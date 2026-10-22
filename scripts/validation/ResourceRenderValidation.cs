using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx.Logging;
using MalodyVThemeLoader.Plugin;
using MalodyVThemeLoader.Shared;
using UnityEditor;
using UnityEngine;

public static class ResourceRenderValidation
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            var root = Environment.GetEnvironmentVariable("MALODY_RESOURCE_FIXTURE");
            var output = Environment.GetEnvironmentVariable("MALODY_RESOURCE_RENDER_OUTPUT");
            var catalog = JsonSerializer.Deserialize<AssetCatalog>(File.ReadAllText(Path.Combine(root, "BepInEx/plugins/MalodyVThemeLoader/catalog.json")), JsonDefaults.Options);
            var themeRoot = Path.Combine(root, "ThemeLoader/themes/Author-dark");
            var store = new ThemeAssetStore(new ManualLogSource(), new LoadedTheme(themeRoot, new ThemeManifest()), catalog);
            var entry = catalog.Images.Single(e => e.LogicalId == "Textures/Malody/editor/btn_editsys_base.png");
            var source = new Texture2D(2, 2); source.LoadImage(File.ReadAllBytes(Path.Combine(root, "ThemeLoader/themes/Default/images/" + entry.LogicalId)));
            var original = Sprite.Create(source, new Rect(0, 0, source.width, source.height), new Vector2(.25f, .75f), 100, 0, SpriteMeshType.FullRect, new Vector4(8,8,8,8));
            original.name = entry.RuntimeName;
            var global = store.ResolveSprite(original.name, original, "SceneStore");
            var scoped = store.ResolveSprite(original.name, original, "SceneSong");
            Require(global != null && scoped != null && global != scoped && global.texture != scoped.texture, "Scenes reused one replacement texture.");
            Require(store.GetOriginalSprite(global) == original && store.GetOriginalSprite(scoped) == original, "Replacement provenance was lost.");
            Require(store.ResolveSprite(global.name, global, "SceneSong") == scoped, "A global atlas result could not resolve to a scene sprite.");
            var rule = store.ResourceRules.ResolveImage(entry.LogicalId, "SceneSong");
            Require(scoped.border.x == rule.Border.X && scoped.border.y == rule.Border.Y && scoped.border.z == rule.Border.Z && scoped.border.w == rule.Border.W, "Unity sprite did not retain the authored slice edges.");
            Require(Mathf.Abs(scoped.pivot.x / scoped.rect.width - .25f) < .001f && Mathf.Abs(scoped.pivot.y / scoped.rect.height - .75f) < .001f && scoped.pixelsPerUnit == 100, "Sprite pivot or pixels-per-unit changed.");
            Require(store.ResolveSprite(original.name, original, "ScenePlay") == null, "Image replacement entered gameplay.");
            var componentRule = new ThemeImageRule { LogicalId = entry.LogicalId, Scene = "SceneSong", Hierarchy = "Canvas/Panel/Target", Component = "Image", File = store.ResourceRules.ResolveImage(entry.LogicalId, "SceneStore").File, Border = new Float4(3,3,3,3), Mode = "sliced" };
            store.ResourceRules.Images.Add(componentRule);
            var specific = store.ResolveSprite(original.name, scoped, "SceneSong", "Canvas/Panel/Target", "Image");
            Require(specific != null && specific.border.x == 3 && specific != scoped, "Component slice settings were shared with the scene.");
            Require(store.ResolveSprite(original.name, specific, "SceneSong", "Canvas/Panel/Other", "Image") == scoped, "Component replacement leaked to a sibling.");

            var cameraObject = new GameObject("validation camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.6f; camera.transform.position = new Vector3(0,0,-10); camera.backgroundColor = new Color(.12f,.12f,.12f); camera.clearFlags = CameraClearFlags.SolidColor;
            for (var i=0; i<2; i++)
            {
                var owner = new GameObject("scene image " + i); var renderer = owner.AddComponent<SpriteRenderer>();
                renderer.sprite = i == 0 ? global : scoped; renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(2.5f,1.2f); owner.transform.position = new Vector3(i == 0 ? -1.6f : 1.6f,0,0);
            }
            var target = new RenderTexture(640,320,24); camera.targetTexture = target; camera.Render();
            RenderTexture.active = target; var result = new Texture2D(640,320,TextureFormat.RGBA32,false); result.ReadPixels(new Rect(0,0,640,320),0,0); result.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(output,"scene-slices.png"),result.EncodeToPNG());
            var pixels=result.GetPixels32(); var left=0; var right=0;
            for (var y=20; y<300; y++) for (var x=0; x<640; x++) { var p=pixels[y*640+x]; if (p.r > 70 || p.g > 70 || p.b > 70) { if (x<320) left++; else right++; } }
            Require(left > 100 && right > 100, "One of the actual Unity sliced sprites rendered blank.");
            componentRule.Border.X = 10000;
            Require(store.ResolveSprite(original.name, original, "SceneSong", "Canvas/Panel/Target", "Image") == null, "Invalid borders replaced the original sprite.");
            componentRule.Border.X=3; componentRule.File="overrides/missing.png";
            Require(store.ResolveSprite(original.name, original, "SceneSong", "Canvas/Panel/Target", "Image") == null, "Missing files replaced the original sprite.");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"passed\":true,\"leftPixels\":"+left+",\"rightPixels\":"+right+",\"checks\":\"Unity source sprite creation, independent scene textures, provenance, control scope, slice borders, pivot/PPU, rendered sliced meshes, gameplay exclusion and invalid/missing fallbacks\"}");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
