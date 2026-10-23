using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EntranceIconMotionValidation
{
    public static void Run()
    {
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/entranceIcon.controller");
            Require(controller != null, "Original entranceIcon controller was not imported.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/SceneSong.unity");

            var canvas = Rect("CanvasForegroundDPI", null);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sizeDelta = new Vector2(1920, 1080);
            var panel = Rect("LeftPanel", canvas);
            panel.anchorMin = panel.anchorMax = new Vector2(0, .5f);
            panel.pivot = new Vector2(0, .5f);
            panel.sizeDelta = new Vector2(274, 586);
            var entrance = Rect("EntranceIcon", panel);
            entrance.anchorMin = new Vector2(0, 1);
            entrance.anchorMax = new Vector2(1, 1);
            entrance.pivot = new Vector2(.5f, 1);
            entrance.sizeDelta = new Vector2(0, 81);
            var animator = entrance.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var mask = Rect("mask", entrance);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = new Vector2(0, 1);
            mask.pivot = new Vector2(.5f, .5f);
            mask.gameObject.AddComponent<RectMask2D>();
            var tip = Rect("tip", mask);
            tip.anchorMin = Vector2.zero;
            tip.anchorMax = Vector2.one;
            tip.gameObject.AddComponent<Image>().color = Color.white;

            MalodyVThemeLoader.Plugin.EntranceIconSlideController.Bind(scene);
            var width = mask.parent.GetComponent<RectTransform>().rect.width;
            Require(Mathf.Abs(mask.anchoredPosition.x + width) < 1, "Icon does not start fully outside the left edge.");

            CheckSample(animator, mask, "hover", width);
            CheckSample(animator, mask, "leave", width);

            animator.Play("hover", 0, 1f);
            animator.Update(0);
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/entranceIconEnter.anim").SampleAnimation(entrance.gameObject,
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/entranceIconEnter.anim").length);
            MalodyVThemeLoader.Plugin.EntranceIconSlideController.Tick();
            Require(Mathf.Abs(mask.anchoredPosition.x) < 1, "Completed enter animation did not finish at its original position.");
            animator.Play("empty", 0, 0);
            animator.Update(0);
            MalodyVThemeLoader.Plugin.EntranceIconSlideController.Tick();
            Require(Mathf.Abs(mask.anchoredPosition.x) < 1, "Completed enter position was not retained in the empty Animator state.");

            animator.Play("leave", 0, 1f);
            animator.Update(0);
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/entranceIconLeave.anim").SampleAnimation(entrance.gameObject,
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/entranceIconLeave.anim").length);
            MalodyVThemeLoader.Plugin.EntranceIconSlideController.Tick();
            Require(Mathf.Abs(mask.anchoredPosition.x + width) < 1, "Completed leave animation did not finish off-screen.");
            animator.Play("empty", 0, 0);
            animator.Update(0);
            MalodyVThemeLoader.Plugin.EntranceIconSlideController.Tick();
            Require(Mathf.Abs(mask.anchoredPosition.x + width) < 1, "Completed leave position was not retained in the empty Animator state.");

            var result = System.Environment.GetEnvironmentVariable("MALODY_ENTRANCE_TEST_RESULT");
            System.IO.File.WriteAllText(result, "PASS: original enter/leave curves drive full-width translation and retain both final positions.");
        }
        catch (System.Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            throw;
        }
    }

    private static void CheckSample(Animator animator, RectTransform mask, string stateName, float width)
    {
        animator.Play(stateName, 0, .5f);
        animator.Update(.001f);
        var state = animator.GetCurrentAnimatorStateInfo(0);
        Require(state.IsName(stateName), $"Animator did not enter state {stateName}.");
        var clipPath = stateName == "hover" ? "Assets/entranceIconEnter.anim" : "Assets/entranceIconLeave.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        Require(clip != null, $"Original {stateName} animation clip was not imported.");
        clip.SampleAnimation(animator.gameObject, clip.length * .5f);
        var progress = mask.anchorMax.x;
        Require(progress > .01f && progress < .99f, $"Original {stateName} clip did not expose an intermediate progress sample: {progress}.");
        MalodyVThemeLoader.Plugin.EntranceIconSlideController.Tick();
        Require(Mathf.Abs(mask.anchoredPosition.x + width * (1 - progress)) < 1,
            $"{stateName} progress {progress} was not mapped to full-width translation.");
        Require(Mathf.Abs(mask.anchorMax.x - 1) < .001f && Mathf.Abs(mask.sizeDelta.x) < .001f,
            $"{stateName} changed the full-size slide viewport.");
    }

    private static RectTransform Rect(string name, RectTransform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = gameObject.GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }
}
