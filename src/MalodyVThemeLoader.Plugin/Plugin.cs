using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.Plugin;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;

        try
        {
            var runtime = ThemeRuntime.Create(Log);
            ThemeRuntime.Current = runtime;

            try
            {
                var scheduler = AddComponent<ThemeRescanScheduler>();
                scheduler.Initialize();
                runtime.Scheduler = scheduler;
            }
            catch (Exception exception)
            {
                Log.LogWarning($"Delayed theme scan scheduler was not installed: {exception.Message}");
            }

            try
            {
                var controller = AddComponent<ControlModeController>();
                controller.Initialize();
            }
            catch (Exception exception)
            {
                Log.LogWarning($"Control mode controller was not installed: {exception.Message}");
            }

            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            try
            {
                if (!Il2CppInterop.Runtime.Injection.ClassInjector.IsTypeRegisteredInIl2Cpp<InspectorInputFence>())
                    Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<InspectorInputFence>();
                var inspector = AddComponent<SceneInspectorController>(); inspector.Initialize();
            }
            catch (Exception e) { Log.LogWarning("Scene inspector unavailable: " + e.Message); }
            harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo("Theme Harmony patches installed.");
            Log.LogInfo(
                $"Loaded theme '{runtime.Theme.Manifest.Name}' from "
                + runtime.Theme.SourceDisplay);
        }
        catch (Exception exception)
        {
            Log.LogError($"ThemeLoader initialization failed: {exception}");
        }
    }
}
