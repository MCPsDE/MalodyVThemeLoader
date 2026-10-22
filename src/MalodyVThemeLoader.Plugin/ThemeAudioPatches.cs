using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Malody.Audio;

namespace MalodyVThemeLoader.Plugin;

// Only UI presentation. No chart, library-preview or hit-sound engine hooks.
[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.Play), new[] { typeof(SoundManager.EffectType) })]
internal static class UiEffectAudioPatch
{
    private static bool Prefix(SoundManager __instance, SoundManager.EffectType __0) => !(ThemeRuntime.Current?.Audio.PlayEffect(__instance, (int)__0) ?? false);
}

[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.Play), new[] { typeof(string), typeof(Il2CppStructArray<byte>), typeof(float) })]
internal static class ResultUiAudioPatch
{
    private static bool Prefix(string __0, float __2) => !(ThemeRuntime.Current?.Audio.PlayResult(__0, __2) ?? false);
}

[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.Stop))]
internal static class StopResultUiAudioPatch
{
    private static void Postfix() => ThemeRuntime.Current?.Audio.StopResult();
}
