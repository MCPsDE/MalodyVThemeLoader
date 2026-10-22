using BepInEx;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ThemeRescanScheduler : MonoBehaviour
{
    private static readonly float[] Delays = { 0.1f, 0.5f, 1.5f, 3.0f, 6.0f, 10.0f };
    private readonly List<ScheduledScan> scans = new();
    private readonly object watcherLock = new();
    private ThemeRuntime? runtime;
    private BepInEx.Logging.ManualLogSource? log;
    private FileSystemWatcher? watcher;
    private string loaderRoot = "";
    private string lastActiveScene = "";
    private bool realtimeMode;
    private bool reloadRequested;
    private bool watcherReloadQueued;
    private float reloadAt;
    private float nextSettingsPoll;
    private string reloadReason = "";
    private string watcherReloadReason = "";

    public ThemeRescanScheduler(IntPtr pointer) : base(pointer)
    {
    }

    public void Initialize()
    {
        runtime = ThemeRuntime.Current;
        if (runtime == null)
        {
            return;
        }

        log = runtime.Log;
        loaderRoot = Path.Combine(Paths.GameRootPath, "ThemeLoader");
        realtimeMode = runtime.Settings.RealtimeMode;
        ConfigureWatcher(realtimeMode);
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        runtime.Log.LogInfo("Delayed theme scan scheduler installed.");
    }

    public void Schedule(string sceneName)
    {
        if (runtime == null || !SceneThemeApplier.IsAllowedScene(sceneName))
        {
            return;
        }

        var now = Time.realtimeSinceStartup;
        scans.RemoveAll(scan =>
            string.Equals(scan.SceneName, sceneName, StringComparison.OrdinalIgnoreCase));

        foreach (var delay in Delays)
        {
            scans.Add(new ScheduledScan(sceneName, now + delay));
        }
    }

    private void Update()
    {
        if (runtime == null)
        {
            PollRealtimeSettings();
            ConsumeWatcherReload();
            RunPendingReload();
            return;
        }

        PollRealtimeSettings();
        ConsumeWatcherReload();
        RunPendingReload();
        CheckActiveScene();

        if (scans.Count == 0)
        {
            return;
        }

        var now = Time.realtimeSinceStartup;
        for (var index = scans.Count - 1; index >= 0; index--)
        {
            var scan = scans[index];
            if (scan.RunAt > now)
            {
                continue;
            }

            scans.RemoveAt(index);
            try
            {
                runtime.Applier.ApplyScene(scan.SceneName);
            }
            catch (Exception exception)
            {
                runtime.Log.LogError(
                    $"Delayed theme scan failed for {scan.SceneName}: {exception}");
            }
        }
    }

    private void LateUpdate()
    {
        if (runtime == null) return;
        // CubismModel.Update reads its previous native parameter buffer. Write
        // animation values afterwards, before its PreLateUpdate model tick.
        try { runtime.Live2d.Tick(); }
        catch (Exception e) { runtime.Live2d.ReportUpdateFailure(e); }
    }

    private void CheckActiveScene()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName)
            || string.Equals(sceneName, lastActiveScene, StringComparison.Ordinal))
        {
            return;
        }

        lastActiveScene = sceneName;
        if (!SceneThemeApplier.IsAllowedScene(sceneName))
        {
            return;
        }

        runtime!.Log.LogInfo($"Theme active scene changed: {sceneName}.");
        try
        {
            runtime.Applier.ApplyScene(sceneName);
            Schedule(sceneName);
        }
        catch (Exception exception)
        {
            runtime.Log.LogError(
                $"Theme scan failed after active scene changed to {sceneName}: {exception}");
        }
    }

    private void PollRealtimeSettings()
    {
        var now = Time.realtimeSinceStartup;
        if (now < nextSettingsPoll || string.IsNullOrWhiteSpace(loaderRoot))
        {
            return;
        }

        nextSettingsPoll = now + 1.0f;
        bool enabled;
        try
        {
            enabled = ThemeRuntime.ReadSettings(loaderRoot).RealtimeMode;
        }
        catch
        {
            enabled = false;
        }

        if (enabled == realtimeMode)
        {
            return;
        }

        realtimeMode = enabled;
        ConfigureWatcher(enabled);
        RequestReload(enabled
            ? "realtime mode enabled"
            : "realtime mode disabled");
    }

    private void ConfigureWatcher(bool enabled)
    {
        watcher?.Dispose();
        watcher = null;
        if (!enabled || string.IsNullOrWhiteSpace(loaderRoot) || !Directory.Exists(loaderRoot))
        {
            return;
        }

        try
        {
            watcher = new FileSystemWatcher(loaderRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime
            };
            watcher.Changed += OnWatchedPathChanged;
            watcher.Created += OnWatchedPathChanged;
            watcher.Deleted += OnWatchedPathChanged;
            watcher.Renamed += OnWatchedPathRenamed;
            watcher.EnableRaisingEvents = true;
            log?.LogInfo("Theme realtime mode watcher enabled.");
        }
        catch (Exception exception)
        {
            log?.LogWarning(
                $"Theme realtime mode watcher was not enabled: {exception.Message}");
        }
    }

    private void OnWatchedPathChanged(object sender, FileSystemEventArgs eventArgs)
    {
        if (IsRelevantChange(eventArgs.FullPath))
        {
            QueueWatcherReload(Path.GetFileName(eventArgs.FullPath));
        }
    }

    private void OnWatchedPathRenamed(object sender, RenamedEventArgs eventArgs)
    {
        if (IsRelevantChange(eventArgs.FullPath)
            || IsRelevantChange(eventArgs.OldFullPath))
        {
            QueueWatcherReload(Path.GetFileName(eventArgs.FullPath));
        }
    }

    private void QueueWatcherReload(string reason)
    {
        if (!realtimeMode)
        {
            return;
        }

        lock (watcherLock)
        {
            watcherReloadQueued = true;
            watcherReloadReason = reason;
        }
    }

    private void ConsumeWatcherReload()
    {
        string reason;
        lock (watcherLock)
        {
            if (!watcherReloadQueued)
            {
                return;
            }

            watcherReloadQueued = false;
            reason = watcherReloadReason;
            watcherReloadReason = "";
        }

        RequestReload(reason.Length == 0 ? "theme files changed" : reason);
    }

    private bool IsRelevantChange(string path)
    {
        if (string.IsNullOrWhiteSpace(loaderRoot))
        {
            return false;
        }

        string relative;
        try
        {
            relative = PathRules.NormalizeLogicalPath(
                Path.GetRelativePath(loaderRoot, path));
        }
        catch
        {
            return false;
        }

        return relative.Equals("active-theme.txt", StringComparison.OrdinalIgnoreCase)
               || relative.Equals("settings.json", StringComparison.OrdinalIgnoreCase)
               || relative.StartsWith("themes/", StringComparison.OrdinalIgnoreCase);
    }

    private void RequestReload(string reason)
    {
        if (!realtimeMode && !reason.Contains("realtime mode", StringComparison.Ordinal))
        {
            return;
        }

        reloadRequested = true;
        reloadAt = Time.realtimeSinceStartup + 0.35f;
        reloadReason = reason;
    }

    private void RunPendingReload()
    {
        if (!reloadRequested || Time.realtimeSinceStartup < reloadAt)
        {
            return;
        }

        reloadRequested = false;
        ReloadRuntime(reloadReason.Length == 0 ? "theme files changed" : reloadReason);
    }

    private void ReloadRuntime(string reason)
    {
        var currentLog = log ?? runtime?.Log;
        if (currentLog == null)
        {
            return;
        }

        var sceneName = SceneManager.GetActiveScene().name;
        currentLog.LogInfo($"Theme realtime reload requested: {reason}.");

        var previous = ThemeRuntime.Current;
        ThemeRuntime.Current = null;
        scans.Clear();

        try
        {
            previous?.Videos.StopAll();
            previous?.Live2d.Dispose();
            ThemeReplacementRegistry.RestoreAll(currentLog);

            var next = ThemeRuntime.Create(currentLog);
            next.Scheduler = this;
            ThemeRuntime.Current = next;
            runtime = next;
            log = next.Log;
            realtimeMode = next.Settings.RealtimeMode;
            ConfigureWatcher(realtimeMode);

            if (SceneThemeApplier.IsAllowedScene(sceneName))
            {
                next.Applier.ApplyScene(sceneName);
                Schedule(sceneName);
            }

            currentLog.LogInfo(
                $"Theme realtime reload complete: '{next.Theme.Manifest.Name}' from "
                + next.Theme.SourceDisplay);
        }
        catch (Exception exception)
        {
            runtime = null;
            ThemeRuntime.Current = null;
            ConfigureWatcher(realtimeMode);
            currentLog.LogError($"Theme realtime reload failed; original assets remain: {exception}");
        }
    }

    private void OnDestroy()
    {
        watcher?.Dispose();
        watcher = null;
    }

    private readonly record struct ScheduledScan(string SceneName, float RunAt);
}
