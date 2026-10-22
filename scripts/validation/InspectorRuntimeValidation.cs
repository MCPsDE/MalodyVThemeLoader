#nullable disable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MalodyVThemeLoader.Plugin;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

public sealed class InspectorRuntimeValidation : MonoBehaviour
{
    private string output;
    private string loader;
    private int checks;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MALODY_INSPECTOR_OUTPUT"))) return;
        new GameObject("ThemeLoader.ValidationDriver").AddComponent<InspectorRuntimeValidation>();
    }
    private IEnumerator Start()
    {
        output = Environment.GetEnvironmentVariable("MALODY_INSPECTOR_OUTPUT"); loader = Path.Combine(BepInEx.Paths.GameRootPath, "ThemeLoader");
        var test = Run();
        while (true)
        {
            bool more; object next;
            try { more = test.MoveNext(); next = more ? test.Current : null; }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = false, checks, error = e.ToString() }, JsonDefaults.Options)); yield break; }
            if (!more) break; yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = true, checks, video = "VP8 alpha + 45% opacity rendered", scope = "isolated Unity play mode; adapted production sources", screen = new[] { Screen.width, Screen.height } }, JsonDefaults.Options));
    }
    private void Require(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static object Call(object owner, string method, params object[] arguments) => owner.GetType().GetMethod(method, Flags).Invoke(owner, arguments);
    private static void Force(SceneInspectorController inspector) { inspector.GetType().GetField("nextPoll", Flags).SetValue(inspector, 0f); inspector.GetType().GetField("nextSnapshot", Flags).SetValue(inspector, 0f); Call(inspector, "Update"); }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name); var r = go.AddComponent<RectTransform>(); r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = position; return r;
    }
    private static Color Pixel(RenderTexture target, float x, float y)
    {
        var previous = RenderTexture.active; RenderTexture.active = target; var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        read.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0); read.Apply(); var color = read.GetPixel((int)(x * target.width), (int)(y * target.height)); Destroy(read); RenderTexture.active = previous; return color;
    }
    private IEnumerator Run()
    {
        var scene = SceneManager.CreateScene("SceneSong"); SceneManager.SetActiveScene(scene);
        var canvas = new GameObject("Canvas").AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var container = Rect("空白容器", canvas.transform, new Vector2(320, 240), Vector2.zero);
        var caption = Rect("标题", container, new Vector2(220, 32), new Vector2(0, 75)).gameObject.AddComponent<Text>(); caption.font = Font.CreateDynamicFontFromOSFont("Arial", 20); caption.text = "Foreground"; caption.color = Color.white; caption.raycastTarget = false;
        var a = Rect("重复节点", canvas.transform, new Vector2(80, 80), new Vector2(-250, 0)).gameObject.AddComponent<Image>();
        var b = Rect("重复节点", canvas.transform, new Vector2(80, 80), new Vector2(250, 0)).gameObject.AddComponent<Image>();
        var texture = new Texture2D(4, 4); texture.SetPixels(Enumerable.Repeat(Color.green, 16).ToArray()); texture.Apply();
        a.sprite = b.sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, 4, 4), new Vector2(.5f, .5f)); a.sprite.name = "source_icon";
        var hidden = Rect("隐藏", canvas.transform, new Vector2(20, 20), Vector2.zero); hidden.gameObject.SetActive(false);
        var mask = Rect("裁剪", canvas.transform, new Vector2(70, 70), new Vector2(0, -190)); mask.gameObject.AddComponent<RectMask2D>();
        var clipped = Rect("超出裁剪", mask, new Vector2(300, 40), Vector2.zero); clipped.gameObject.AddComponent<Image>();
        Canvas.ForceUpdateCanvases();
        var themeRoot = Path.Combine(loader, "themes/Inspector"); Directory.CreateDirectory(Path.Combine(themeRoot, "images"));
        File.Copy(Path.Combine(Environment.GetEnvironmentVariable("MALODY_INSPECTOR_MEDIA"), "background.png"), Path.Combine(themeRoot, "images/background.png"), true);
        File.Copy(Path.Combine(Environment.GetEnvironmentVariable("MALODY_INSPECTOR_MEDIA"), "vp8.webm"), Path.Combine(themeRoot, "images/video.webm"), true);
        var catalog = new AssetCatalog(); catalog.Images.Add(new ImageCatalogEntry { LogicalId = "Textures/icon.png", RuntimeName = "source_icon", Width = 4, Height = 4 });
        var theme = new LoadedTheme(themeRoot, new ThemeManifest()); var store = new ThemeAssetStore(Plugin.Log, theme, catalog);
        var nodes = SceneNodeLocator.Collect(store, out var truncated);
        var ai = nodes.Single(n => n.Transform == a.transform); var bi = nodes.Single(n => n.Transform == b.transform); var ci = nodes.Single(n => n.Transform == container);
        Require(!truncated && ai.Info.Address != bi.Info.Address && ai.Info.Address == SceneNodeLocator.AddressOf(a.transform), "Duplicate addresses are unstable.");
        Require(ai.Info.LogicalId == "Textures/icon.png" && bi.Info.LogicalId == ai.Info.LogicalId, "Original asset mapping failed.");
        store.ResourceRules.Images.Add(new ThemeImageRule { LogicalId = "Textures/icon.png", Scene = "SceneSong", Hierarchy = ai.Info.Hierarchy, Component = "Image", NodeAddress = ai.Info.Address, File = "background.png" });
        Require(store.ResolveSprite(a.sprite.name, a.sprite, "SceneSong", ai.Info.Hierarchy, "Image", ai.Info.Address) != null
            && store.ResolveSprite(b.sprite.name, b.sprite, "SceneSong", bi.Info.Hierarchy, "Image", bi.Info.Address) == null, "Exact image rules leaked across same-name siblings.");
        Require(nodes.Any(n => n.Transform == hidden && !n.Info.Active) && ci.Info.CanDecorate, "Hidden/empty hierarchy missing.");
        var clipNode = nodes.Single(n => n.Transform == clipped);
        var outside = RectTransformUtility.WorldToScreenPoint(null, clipped.TransformPoint(new Vector3(120, 0)));
        Require(!SceneNodeLocator.Hit(clipNode, outside), "Picking ignored RectMask2D.");
        var rules = new ThemeResources(); rules.Nodes.Add(new ThemeNodeRule { Scene = "SceneSong", Address = ci.Info.Address, Hierarchy = ci.Info.Hierarchy, ImageFile = "background.png", VideoFile = "video.webm", VideoOpacity = .45f });
        rules.Nodes.Add(new ThemeNodeRule { Scene = "SceneSong", Address = nodes.Single(n => n.Transform == caption.transform).Info.Address, Hierarchy = "Canvas/空白容器/标题", ImageFile = "background.png" });
        rules.Write(themeRoot); var service = new NodeDecorationService(Plugin.Log, theme, rules);
        ThemeRuntime.Current = new ThemeRuntime { Theme = theme, Assets = store, Decorations = service };
        service.Tick(); Canvas.ForceUpdateCanvases(); yield return null;
        var layer = container.Find("ThemeLoader.Decoration"); Require(layer != null && layer.GetSiblingIndex() == 0, "Background layer was not inserted behind content.");
        Require(caption.transform.GetSiblingIndex() > 0 && caption.enabled && a.sprite == b.sprite, "Decoration changed original content.");
        Require(layer.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget && g.GetComponents<Graphic>().Length == 1), "Decorations intercept input or share one Graphic host.");
        var player = layer.GetComponentInChildren<VideoPlayer>();
        var until = Time.realtimeSinceStartup + 15;
        while ((!player.isPrepared || player.frame < 0) && Time.realtimeSinceStartup < until) { service.Tick(); yield return null; }
        Require(player.isPrepared && player.frame >= 0, "VP8 alpha video did not produce a frame.");
        service.Tick(); var raw = layer.GetComponentInChildren<RawImage>(); Require(raw.enabled && Math.Abs(raw.color.a - .45f) < .001, "Video opacity was not applied.");
        var opaque = Pixel(player.targetTexture, .25f, .5f); var transparent = Pixel(player.targetTexture, .75f, .5f);
        Require(opaque.b > .7f && opaque.a > .8f && transparent.a < .1f, "Video lost per-pixel alpha.");
        // Render the actual UI layers to a camera target to check compositing.
        var camera = new GameObject("Validation camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = Screen.height / 2f; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create(); camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; Canvas.ForceUpdateCanvases(); yield return null; service.Tick(); Canvas.ForceUpdateCanvases(); camera.Render();
        var center = RectTransformUtility.WorldToScreenPoint(camera, container.position);
        var left = Pixel(target, (center.x - 80) / target.width, center.y / target.height); var right = Pixel(target, (center.x + 80) / target.width, center.y / target.height);
        Require(left.r > .3f && left.b > .2f && right.r > .8f && right.b < .15f, "UI did not composite video over the retained background.");
        Save(target, "node-video.png");
        container.gameObject.SetActive(false); service.Tick(); Require(!layer.gameObject.activeInHierarchy, "Video layer remained visible after target hide.");
        container.gameObject.SetActive(true); yield return null; service.Tick();
        var control = new InspectorControl { Client = "runtime-validation", Enabled = true, LeaseUtc = DateTime.UtcNow, ShowNames = true, ShowFrames = true };
        InspectorFiles.Write(loader, InspectorFiles.ControlName, control);
        var inspector = new GameObject("ThemeLoader.TestInspector").AddComponent<SceneInspectorController>(); inspector.Initialize(); Force(inspector); yield return null;
        var snapshot = InspectorFiles.Read<InspectorSnapshot>(loader, InspectorFiles.SnapshotName);
        Require(snapshot != null && snapshot.Nodes.Any(n => n.Id == ai.Info.Id) && snapshot.Nodes.All(n => !n.Name.StartsWith("ThemeLoader.")), "Inspector snapshot includes its own layers or omits source objects.");
        var point = RectTransformUtility.WorldToScreenPoint(camera, a.transform.position); Call(inspector, "Pick", point);
        snapshot = InspectorFiles.Read<InspectorSnapshot>(loader, InspectorFiles.SnapshotName);
        Require(snapshot.SelectedId == ai.Info.Id && snapshot.SelectionOrigin == "game", "Middle-click picker selected the wrong visible node.");
        var first = snapshot.SelectedId; Call(inspector, "Pick", point); snapshot = InspectorFiles.Read<InspectorSnapshot>(loader, InspectorFiles.SnapshotName);
        Require(first != snapshot.SelectedId, "Repeated picking did not cycle overlapping hierarchy.");
        control.Runtime = snapshot.Runtime; control.SelectedId = bi.Info.Id; control.SelectRequest = 10; control.LeaseUtc = DateTime.UtcNow; InspectorFiles.Write(loader, InspectorFiles.ControlName, control); Force(inspector);
        Require(InspectorFiles.Read<InspectorSnapshot>(loader, InspectorFiles.SnapshotName).SelectedId == bi.Info.Id, "Editor selection did not return to the game.");
        Call(inspector, "LateUpdate"); var overlay = inspector.GetType().GetField("overlay", Flags).GetValue(inspector) as GameObject;
        var overlayCanvas = overlay.GetComponent<Canvas>(); overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera; overlayCanvas.worldCamera = camera; overlayCanvas.planeDistance = .5f; Canvas.ForceUpdateCanvases(); camera.Render(); Save(target, "hierarchy.png");
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var events = new GameObject("ThemeLoader.TestEvents").AddComponent<EventSystem>();
        var fence = overlay.GetComponentInChildren<InspectorInputFence>(); fence.CaptureForFrame(true, Time.frameCount);
        var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Middle };
        var raycasts = new System.Collections.Generic.List<RaycastResult>(); events.RaycastAll(pointer, raycasts);
        Require(raycasts.Count > 0 && raycasts[0].gameObject == fence.gameObject, "Middle click was not intercepted by the loader-owned layer.");
        yield return null; yield return null; yield return null;
        pointer.button = PointerEventData.InputButton.Left; raycasts.Clear(); events.RaycastAll(pointer, raycasts);
        Require(raycasts.Count > 0 && raycasts[0].gameObject == a.gameObject, "Inspection layer swallowed ordinary game UI clicks.");
        var mesh = inspector.GetType().GetField("mesh", Flags).GetValue(inspector) as Mesh;
        Require(mesh.vertexCount > 16 && overlay.GetComponentsInChildren<Text>().Length > 2, "Inspector did not draw bounds and names.");
        var emptyPixel = Pixel(target, .1f, .1f); Require(emptyPixel.r < .05f && emptyPixel.g < .05f && emptyPixel.b < .05f, "Debug outlines filled unrelated screen areas.");
        var stress = Rect("批次测试", canvas.transform, Vector2.one, Vector2.zero);
        for (var i = 0; i < 4000; i++) Rect("N" + i, stress, Vector2.one, Vector2.zero);
        control.ShowNames = false; control.LeaseUtc = DateTime.UtcNow; InspectorFiles.Write(loader, InspectorFiles.ControlName, control); Canvas.ForceUpdateCanvases(); Force(inspector); Call(inspector, "LateUpdate");
        var batches = inspector.GetType().GetField("meshBatches", Flags).GetValue(inspector) as System.Collections.Generic.List<Mesh>;
        Require(batches.Count > 1 && batches.All(m => m.vertexCount <= 60000 && m.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16), "Large hierarchy did not split UI index batches.");
        Destroy(stress.gameObject); yield return null;
        var projection = new GameObject("ThemeLoader.ProjectionTest").AddComponent<ControlModeController>();
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "ThemeLoader.ProjectionQuad"; quad.transform.localRotation = Quaternion.Euler(180, 0, 0); quad.transform.localScale = new Vector3(8, 6, 1);
        var projectionCamera = new GameObject("ThemeLoader.ProjectionCamera").AddComponent<Camera>(); projectionCamera.transform.position = new Vector3(0, 0, -8);
        typeof(ControlModeController).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, projection);
        typeof(ControlModeController).GetField("controlMode", Flags).SetValue(projection, true); typeof(ControlModeController).GetField("screenQuad", Flags).SetValue(projection, quad); typeof(ControlModeController).GetField("displayCamera", Flags).SetValue(projection, projectionCamera);
        var projected = projectionCamera.WorldToScreenPoint(new Vector3(1.6f, .6f, 0));
        Require(ControlModeController.TryMapInspectorPoint(projected, out var mapped) && Math.Abs(mapped.x / Screen.width - .7f) < .002 && Math.Abs(mapped.y / Screen.height - .6f) < .002, "Projection picking did not undo the captured-frame flip.");
        quad.SetActive(false); Require(ControlModeController.TryMapInspectorPoint(new Vector2(20, 30), out mapped) && mapped == new Vector2(20, 30), "Inactive projection did not use original coordinates.");
        typeof(ControlModeController).GetField("controlMode", Flags).SetValue(projection, false); Destroy(projection.gameObject); Destroy(quad); Destroy(projectionCamera.gameObject);
        control.LeaseUtc = DateTime.UtcNow.AddSeconds(-30); InspectorFiles.Write(loader, InspectorFiles.ControlName, control); Force(inspector);
        Require(!overlay.activeSelf && !SceneInspectorController.IsInspecting, "Expired connection did not remove debug overlay.");
        service.Dispose(); yield return null;
        Require(container.Find("ThemeLoader.Decoration") == null && caption.enabled && a.sprite == b.sprite, "Disposal did not restore the original hierarchy.");
        var invalid = new ThemeResources(); invalid.Nodes.Add(new ThemeNodeRule { Scene = "SceneSong", Address = ci.Info.Address, Hierarchy = ci.Info.Hierarchy, ImageFile = "missing.png" });
        var bad = new NodeDecorationService(Plugin.Log, theme, invalid); bad.Tick(); yield return null;
        Require(container.Find("ThemeLoader.Decoration") == null && bad.Status("SceneSong", ci.Info.Address).StartsWith("加载失败"), "Invalid decoration left an orphan or claimed success."); bad.Dispose();
        Require(a.sprite == b.sprite, "Unrelated source images were changed.");
        store.Dispose(); camera.targetTexture = null; target.Release(); Destroy(target); Destroy(inspector.gameObject);
    }
    private static void Save(RenderTexture target, string name)
    {
        var old = RenderTexture.active; RenderTexture.active = target; var bitmap = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        bitmap.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0); bitmap.Apply(); File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("MALODY_INSPECTOR_OUTPUT"), name), bitmap.EncodeToPNG()); Destroy(bitmap); RenderTexture.active = old;
    }
}
