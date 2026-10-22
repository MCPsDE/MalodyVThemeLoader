using System.Text;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Malody.Edit.Manager;
using Malody.Live2d;
using Malody.Manager;
using Malody.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

[HarmonyPatch(typeof(Malody.Scene.Widget.WidgetLive2dControl), "Start")]
internal static class Live2dVisualStartPatch
{
    private static void Postfix(Malody.Scene.Widget.WidgetLive2dControl __instance)
    {
        ThemeRuntime.Current?.Live2d.ApplyNow(__instance);
    }
}

[HarmonyPatch(typeof(Malody.Scene.Widget.WidgetLive2dControl), "Show")]
internal static class Live2dVisualShowPatch
{
    private static void Postfix(Malody.Scene.Widget.WidgetLive2dControl __instance)
    {
        ThemeRuntime.Current?.Live2d.ApplyNow(__instance);
    }
}

[HarmonyPatch(typeof(Malody.Scene.Widget.WidgetLive2dControl), "OnPointerClick")]
internal static class Live2dVisualClickPatch
{
    private static void Postfix(Malody.Scene.Widget.WidgetLive2dControl __instance, UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
            ThemeRuntime.Current?.Live2d.PlayTap(__instance, eventData);
    }
}

[HarmonyPatch]
internal static class Live2dResultDisplayPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(Malody.Scene.Panel.PanelResultScore).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == "Fill" && (method.GetParameters().Length == 2
                || method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.Name is "MPScore" or "ScoreRecord"));

    private static void Postfix(Malody.Scene.Panel.PanelResultScore __instance)
    {
        ThemeRuntime.Current?.Live2d.ObserveResult(__instance);
    }
}

[HarmonyPatch(typeof(UIGlobal), nameof(UIGlobal.GetSprite))]
internal static class UiGlobalGetSpritePatch
{
    private static void Postfix(string _name, ref Sprite __result)
    {
        var replacement = ThemeRuntime.Current?.Assets.ResolveSprite(_name, __result);
        if (replacement != null)
        {
            __result = replacement;
        }
    }
}

[HarmonyPatch(typeof(EditResource), nameof(EditResource.GetSprite))]
internal static class EditResourceGetSpritePatch
{
    private static void Postfix(string _name, ref Sprite __result)
    {
        var replacement = ThemeRuntime.Current?.Assets.ResolveSprite(_name, __result);
        if (replacement != null)
        {
            __result = replacement;
        }
    }
}

[HarmonyPatch(typeof(SceneSong), "Start")]
internal static class SceneSongStartThemePatch
{
    private static void Postfix()
    {
        SceneStartThemePatch.Apply("SceneSong");
    }
}

[HarmonyPatch(typeof(SceneStore), "Start")]
internal static class SceneStoreStartThemePatch
{
    private static void Postfix()
    {
        SceneStartThemePatch.Apply("SceneStore");
    }
}

[HarmonyPatch(typeof(SceneEditor), "Start")]
internal static class SceneEditorStartThemePatch
{
    private static void Postfix()
    {
        SceneStartThemePatch.Apply("SceneEditor");
    }
}

[HarmonyPatch(typeof(SceneEditorSkin), "Start")]
internal static class SceneEditorSkinStartThemePatch
{
    private static void Postfix()
    {
        SceneStartThemePatch.Apply("SceneEditorSkin");
    }
}

[HarmonyPatch(typeof(SceneManager), nameof(SceneManager.Internal_SceneLoaded))]
internal static class SceneLoadedThemePatch
{
    private static void Postfix(Scene scene, LoadSceneMode mode)
    {
        SceneStartThemePatch.Apply(scene.name);
    }
}

[HarmonyPatch(typeof(Image), nameof(Image.OnEnable))]
internal static class ImageOnEnableThemePatch
{
    private static void Postfix(Image __instance)
    {
        ImageThemePatch.Apply(__instance);
    }
}

[HarmonyPatch(typeof(Image), "set_sprite")]
internal static class ImageSetSpriteThemePatch
{
    private static void Postfix(Image __instance)
    {
        ImageThemePatch.Apply(__instance);
    }
}

[HarmonyPatch(typeof(Image), "set_overrideSprite")]
internal static class ImageSetOverrideSpriteThemePatch
{
    private static void Postfix(Image __instance)
    {
        ImageThemePatch.Apply(__instance);
    }
}

internal static class ImageThemePatch
{
    [ThreadStatic]
    private static bool applying;

    public static void Apply(Image image)
    {
        if (applying)
        {
            return;
        }

        try
        {
            applying = true;
            ThemeRuntime.Current?.Applier.ApplyImage(image, true);
        }
        catch (Exception exception)
        {
            ThemeRuntime.Current?.Log.LogWarning(
                $"Immediate Image theme application failed: {exception.Message}");
        }
        finally
        {
            applying = false;
        }
    }
}

internal static class SceneStartThemePatch
{
    public static void Apply(string sceneName)
    {
        try
        {
            var runtime = ThemeRuntime.Current;
            if (runtime == null)
            {
                return;
            }

            runtime.Applier.ApplyScene(sceneName);
            runtime.Scheduler?.Schedule(sceneName);
        }
        catch (Exception exception)
        {
            ThemeRuntime.Current?.Log.LogError(
                $"Theme scan failed after {sceneName}.Start: {exception}");
        }
    }
}

[HarmonyPatch]
internal static class LegacyLive2dTexturePatch
{
    private static bool Prepare() =>
        LegacyLive2dPatchTarget.FindMethod("LoadTexture") != null;

    private static MethodBase TargetMethod() =>
        LegacyLive2dPatchTarget.FindMethod("LoadTexture")!;

    private static bool Prefix(string path, ref Texture2D __result)
    {
        var file = ThemeRuntime.Current?.Assets.ResolveLegacyLive2dFile(path, ".png");
        if (file == null)
        {
            return true;
        }

        var bytes = File.ReadAllBytes(file);
        Il2CppStructArray<byte> il2CppBytes = bytes;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, il2CppBytes, false))
        {
            UnityEngine.Object.Destroy(texture);
            return true;
        }

        texture.name = Path.GetFileNameWithoutExtension(file);
        __result = texture;
        return false;
    }
}

[HarmonyPatch]
internal static class LegacyLive2dBinaryPatch
{
    private static bool Prepare() =>
        LegacyLive2dPatchTarget.FindMethod("LoadBin") != null;

    private static MethodBase TargetMethod() =>
        LegacyLive2dPatchTarget.FindMethod("LoadBin")!;

    private static bool Prefix(string path, ref Il2CppStructArray<byte> __result)
    {
        var file = ThemeRuntime.Current?.Assets.ResolveLegacyLive2dFile(
            path, "", ".bytes", ".moc.bytes", ".mtn.bytes", ".json");
        if (file == null)
        {
            return true;
        }

        __result = File.ReadAllBytes(file);
        return false;
    }
}

[HarmonyPatch]
internal static class LegacyLive2dStringPatch
{
    private static bool Prepare() =>
        LegacyLive2dPatchTarget.FindMethod("LoadString") != null;

    private static MethodBase TargetMethod() =>
        LegacyLive2dPatchTarget.FindMethod("LoadString")!;

    private static bool Prefix(string path, ref string __result)
    {
        var file = ThemeRuntime.Current?.Assets.ResolveLegacyLive2dFile(
            path, ".json", "");
        if (file == null)
        {
            return true;
        }

        __result = File.ReadAllText(file, Encoding.UTF8);
        return false;
    }
}

internal static class LegacyLive2dPatchTarget
{
    public static MethodInfo? FindMethod(string methodName)
    {
        return typeof(LAppModelProxy).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Malody.Live2d")
            .Select(type => AccessTools.Method(type, methodName))
            .FirstOrDefault(method => method != null);
    }
}
