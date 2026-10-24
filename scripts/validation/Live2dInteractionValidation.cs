using System;
using System.Collections.Generic;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace MalodyVThemeLoader.Plugin;

internal static class Live2dInteractionValidation
{
    internal static System.Collections.IEnumerator Run(Action<object> completed)
    {
        var checks = 0;
        void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var cameraObject = new GameObject("ClickCamera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10);
        var canvasObject = new GameObject("ClickCanvas");
        var canvas = canvasObject.AddComponent<Canvas>(); canvas.worldCamera = camera; canvas.planeDistance = 5;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvasObject.AddComponent<GraphicRaycaster>();
        var receiver = new GameObject("OriginalClickBox").AddComponent<Image>();
        var rect = receiver.rectTransform; rect.SetParent(canvas.transform, false);
        rect.sizeDelta = new Vector2(80, 100); rect.anchoredPosition = new Vector2(-40, 50);
        receiver.color = Color.clear; receiver.canvasRenderer.cullTransparentMesh = false;
        var oldPadding = new Vector4(-3, -5, -7, -9); receiver.raycastPadding = oldPadding;
        var model = new GameObject("ModelGeometry").transform;
        var bounds = new Bounds(Vector3.zero, new Vector3(2, 6, 0));
        model.position = new Vector3(-.3f, -.7f, 0); model.localScale = new Vector3(1.13f, 1.13f, 1.13f);
        var system = new GameObject("ClickEventSystem").AddComponent<EventSystem>();
        GameObject? Hit(Vector2 position)
        {
            var pointer = new PointerEventData(system); pointer.position = position;
            var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
            system.RaycastAll(pointer, hits);
            return hits.Count == 0 ? null : hits[0].gameObject;
        }
        Vector2 Project(float x, float y)
        {
            var point = camera.WorldToScreenPoint(model.TransformPoint(new Vector3(
                bounds.min.x + bounds.size.x * x, bounds.max.y - bounds.size.y * y, 0)));
            return new Vector2(point.x, point.y);
        }
        Canvas.ForceUpdateCanvases();
        yield return null; // Graphic.depth is assigned by the native render loop.
        var lower = Project(.5f, .94f);
        Require(!RectTransformUtility.RectangleContainsScreenPoint(rect, lower, camera, oldPadding), "Fixture did not reproduce the original lower-body cutoff.");
        Require(Hit(lower) != receiver.gameObject, "The lower-body baseline was already clickable.");
        var oldSize = rect.sizeDelta; var oldPosition = rect.anchoredPosition;
        using (var surface = new Live2dClickSurface(receiver, model, bounds))
        {
            var binding = new Live2dBinding { TapMotion = "fallback" };
            binding.ClickRegions.Add(new Live2dClickRegion { Name = "Lower", X = 0, Y = .7f, Width = 1, Height = .3f, Motion = "legs" });
            for (var mode = 0; mode < 4; mode++)
            {
                canvas.renderMode = mode % 2 == 0 ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = mode % 2 == 0 ? 1 : 1.5f;
                camera.orthographic = mode < 2; camera.fieldOfView = 55;
                camera.transform.position = new Vector3(mode == 3 ? .4f : 0, 0, -10);
                model.localScale = new Vector3(mode == 2 ? -1.13f : 1.13f, 1.13f, 1.13f);
                model.rotation = Quaternion.Euler(0, 0, mode == 3 ? 9 : 0);
                Canvas.ForceUpdateCanvases(); yield return null; surface.Refresh();
                foreach (var x in new[] { .02f, .5f, .98f }) foreach (var y in new[] { .02f, .5f, .94f, .98f })
                {
                    var screen = Project(x, y);
                    Require(Hit(screen) == receiver.gameObject, $"Expanded native raycast missed model point {x},{y} in mode {mode}. screen={screen}; padding={receiver.raycastPadding}; rect={rect.rect}; depth={receiver.depth}; cull={receiver.canvasRenderer.cull}; contains={RectTransformUtility.RectangleContainsScreenPoint(rect, screen, camera, receiver.raycastPadding)}; filter={receiver.Raycast(screen, camera)}; hit={Hit(screen)?.name}");
                    Require(Live2dClickSurface.TryModelPoint(model, bounds, canvas.worldCamera, screen, out var normalized), "Model hit could not be normalized.");
                    Require(Math.Abs(normalized.x - x) < .002f && Math.Abs(normalized.y - y) < .002f, "Hit coordinates differ from authoring preview.");
                    Require(Live2dEvents.ClickMotion(binding, normalized.x, normalized.y) == (y >= .7f ? "legs" : "fallback"), "Lower-body hit dispatched the wrong region animation.");
                }
                Require(rect.sizeDelta == oldSize && rect.anchoredPosition == oldPosition, "Click extension moved or resized the original widget.");
                var button = new GameObject("ForegroundButton").AddComponent<Image>(); button.rectTransform.SetParent(canvas.transform, false);
                var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera;
                Require(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(), Project(.5f, .94f), eventCamera, out var buttonPoint), "Button point mapping failed.");
                button.rectTransform.anchoredPosition = buttonPoint; button.rectTransform.sizeDelta = new Vector2(32, 32);
                button.gameObject.AddComponent<Button>(); Canvas.ForceUpdateCanvases();
                yield return null;
                Require(Hit(Project(.5f, .94f)) == button.gameObject, "Expanded model receiver intercepted a foreground button.");
                Object.DestroyImmediate(button.gameObject);
                Require(Hit(new Vector2(Screen.width - 4, Screen.height - 4)) != receiver.gameObject, "Model receiver expanded to the whole screen.");
            }
            // Do not leave an invisible full-body receiver when the model is
            // behind the camera. Refresh can subsequently recover by itself.
            model.position = new Vector3(0, 0, -12); surface.Refresh();
            Require(receiver.raycastPadding == oldPadding, "Invalid model projection retained an expanded receiver.");
            model.position = new Vector3(-.3f, -.7f, 0); surface.Refresh();
            Require(Hit(Project(.5f, .94f)) == receiver.gameObject, "Click area did not recover after a projection failure.");
        }
        Require(receiver.raycastPadding == oldPadding, "Theme disposal failed to restore original raycast padding.");
        Object.DestroyImmediate(system.gameObject); Object.DestroyImmediate(model.gameObject);
        Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject);

        var atlas = Resources.Load<SpriteAtlas>("ResultRankProbe");
        Require(atlas != null, "Packed result atlas fixture was not included in the isolated player.");
        var atlasName = "";
        var motions = new Dictionary<string, string>();
        foreach (var key in Live2dEvents.ResultKeys) motions[key] = "motion-" + key;
        var dispatched = 0;
        for (var level = 0; level < 5; level++) for (var rank = 1; rank <= 5; rank++)
        {
            var sprite = atlas!.GetSprite($"rank_score_{level}{rank}");
            Require(sprite != null, "Packed rank sprite is missing."); atlasName = sprite!.name;
            Require(atlasName.EndsWith("(Clone)", StringComparison.Ordinal), "Unity atlas clone suffix was not reproduced.");
            var result = Live2dEvents.ResultFromSprite(atlasName);
            Require(result == "M" + (6 - rank), "Native atlas rank did not map to its M1-M5 result.");
            var state = new Live2dResultAnimation();
            Require(!state.Apply(result, motions, _ => false), "Unavailable model was marked as having played its result.");
            Require(state.Apply(result, motions, file => { dispatched++; return file == "motion-" + result; }), "Pending result did not play once the model was ready.");
            for (var poll = 0; poll < 10; poll++)
                Require(!state.Apply(result, motions, _ => throw new Exception("Repeated result restarted the animation.")), "Repeated result was replayed.");
            Object.DestroyImmediate(sprite);
        }
        Require(dispatched == 25, "Result animation dispatch count is wrong.");
        foreach (var invalid in new[] { "rank_score_bg_2(Clone)", "rank_score_20(Clone)", "rank_score_211(Clone)", "rank_score_91(Clone)", "rank_score_21(Clone)(Clone)" })
            Require(Live2dEvents.ResultFromSprite(invalid) == "", "Unsupported artwork triggered a result animation.");
        completed(new { passed = true, checks, atlasName, dispatched, scope = "native UGUI raycasts and packed SpriteAtlas; production click projection/result dispatch; surrogate model geometry" });
    }
}
