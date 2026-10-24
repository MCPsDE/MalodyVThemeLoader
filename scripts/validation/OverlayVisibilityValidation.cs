#nullable disable
using System;
using System.IO;
using System.Linq;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MalodyVThemeLoader.Plugin;

internal static class OverlayVisibilityValidation
{
    internal static int Run(string output, bool render)
    {
        var checks = 0;
        void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name).AddComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(200, 100); return rect;
        }
        Image Background(string name, Transform parent)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>(); image.color = new Color(0, 0, 0, .2f);
            return image;
        }
        var previous = SceneManager.GetActiveScene();
        var entrance = Rect("CanvasForegroundDPI", null);
        var tip = Background("tip", Rect("mask", Rect("EntranceIcon", Rect("LeftPanel", entrance))));
        Require(BuiltInOverlayVisibility.Hide(tip) && tip.color.a == 0, "Entrance tip overlay was not cleared in its exact hierarchy.");
        Object.DestroyImmediate(entrance.gameObject);

        var result = SceneManager.CreateScene("SceneResult"); SceneManager.SetActiveScene(result);
        var canvas = Rect("Canvas", null).gameObject.AddComponent<Canvas>();
        var backdrop = Background("Backdrop", canvas.transform); backdrop.color = Color.white;
        var mask = Background("bgmask", canvas.transform);
        var clip = mask.gameObject.AddComponent<RectMask2D>();
        var text = Rect("Text", mask.transform).gameObject.AddComponent<Text>();
        foreach (var name in new[] { "bottom", "bottom_mp", "bottom_watch" })
        {
            var bottom = Background(name, canvas.transform);
            var raycast = bottom.raycastTarget;
            Require(BuiltInOverlayVisibility.Hide(bottom) && bottom.color.a == 0, "Result bottom overlay was not cleared: " + name);
            Require(bottom.enabled && bottom.raycastTarget == raycast, "Overlay removal changed input or component state.");
        }
        var artwork = Background("Image", canvas.transform); artwork.color = new Color(1, 1, 1, .447f); artwork.gameObject.SetActive(false);
        Require(!BuiltInOverlayVisibility.Hide(artwork) && artwork.color.a == .447f, "Actual background artwork was cleared.");
        var unrelated = Background("bgmask", Rect("OtherPanel", canvas.transform));
        Require(!BuiltInOverlayVisibility.Hide(unrelated) && unrelated.color.a == .2f, "A same-name node in another hierarchy was cleared.");
        var duplicate = Rect("bgmask", canvas.transform);
        Require(!BuiltInOverlayVisibility.Hide(mask) && mask.color.a == .2f, "Ambiguous sibling overlays were changed.");
        Object.DestroyImmediate(duplicate.gameObject);
        var duplicateCanvas = Rect("Canvas", null);
        Require(!BuiltInOverlayVisibility.Hide(mask), "Ambiguous scene roots were changed.");
        Object.DestroyImmediate(duplicateCanvas.gameObject);
        unrelated.gameObject.SetActive(false);

        Camera camera = null; RenderTexture target = null;
        Color Pixel()
        {
            Canvas.ForceUpdateCanvases(); camera.Render();
            var before = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
            var color = pixels.GetPixel(128, 128);
            Object.DestroyImmediate(pixels); RenderTexture.active = before; return color;
        }
        var originalPixel = Color.clear;
        if (render)
        {
            camera = new GameObject("Overlay validation camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 128; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            target = new RenderTexture(256, 256, 24); target.Create(); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            originalPixel = Pixel();
        }
        else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Require(BuiltInOverlayVisibility.Hide(mask) && !BuiltInOverlayVisibility.Hide(mask), "Overlay clearing is not idempotent.");
        Require(mask.enabled && clip.isActiveAndEnabled && text.enabled && text.gameObject.activeInHierarchy, "Clearing Image alpha removed its clipping or content.");
        if (render) Require(Pixel().r > originalPixel.r + .1f, "The original overlay remains in the rendered output.");
        Canvas.ForceUpdateCanvases();
        var node = SceneNodeLocator.Collect(null, out _, true).Single(n => n.Transform == mask.transform);
        Require(node.Info.CanDecorate, "Transparent overlay node cannot be edited.");
        var point = RectTransformUtility.WorldToScreenPoint(SceneNodeLocator.CameraFor(mask.rectTransform), mask.rectTransform.TransformPoint(Vector3.zero));
        Require(SceneNodeLocator.HitThrough(node, point), "Transparent overlay node cannot be found with through search.");
        var theme = new LoadedTheme(Path.Combine(output, "overlay-fixture"), new ThemeManifest()); Directory.CreateDirectory(theme.ImageDirectory);
        var png = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++) png.SetPixel(x, y, new Color(0, 0, 0, .2f));
        png.Apply(); File.WriteAllBytes(Path.Combine(theme.ImageDirectory, "half.png"), ImageConversion.EncodeToPNG(png)); Object.DestroyImmediate(png);
        var rules = new ThemeResources(); rules.Nodes.Add(new ThemeNodeRule { Scene = "SceneResult", Address = node.Info.Address, ImageFile = "half.png" });
        using (var decorations = new NodeDecorationService(Plugin.Log, theme, rules))
        {
            decorations.Tick(); Canvas.ForceUpdateCanvases();
            var custom = mask.transform.Find("ThemeLoader.Decoration").GetComponentInChildren<Image>();
            Require(custom != null && custom.enabled && custom.color == Color.white && custom.sprite != null, "Transparent original Graphic also hid the custom PNG.");
            Require(SceneNodeLocator.Hit(node, point), "Decorated transparent overlay cannot be picked normally.");
            Require(!BuiltInOverlayVisibility.Hide(custom) && custom.color.a == 1, "Custom backgrounds were also suppressed.");
            if (render) Require(Math.Abs(Pixel().r - originalPixel.r) < .025f, "A semi-transparent PNG did not reproduce the original overlay pixels.");
        }
        if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target); }
        Object.DestroyImmediate(canvas.gameObject);
        SceneManager.SetActiveScene(previous); SceneManager.UnloadSceneAsync(result);
        return checks;
    }
}
