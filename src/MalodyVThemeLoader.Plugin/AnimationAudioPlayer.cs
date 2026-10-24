using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal sealed class AnimationAudioPlayer : IDisposable
{
    private readonly ManualLogSource log;
    private readonly Dictionary<string, Live2dAudio> data;
    private readonly Dictionary<string, AudioClip> clips = new(StringComparer.OrdinalIgnoreCase);
    private GameObject? owner;
    private AudioSource? source;
    private bool disposed;
    private bool failureLogged;

    internal AnimationAudioPlayer(ManualLogSource log, IReadOnlyDictionary<string, Live2dAudio> audio)
    {
        this.log = log;
        data = audio.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var pair in data) clips.Add(pair.Key, UnityAudioClipFactory.Create("ThemeLoader." + pair.Key, pair.Value));
            if (clips.Count > 0) EnsureSource();
        }
        catch { Dispose(); throw; }
    }

    internal void Play(string motion)
    {
        if (disposed) return;
        Stop();
        if (!data.TryGetValue(motion, out var audio)) return;
        try
        {
            EnsureSource();
            if (!clips.TryGetValue(motion, out var clip) || !Alive(clip))
                clips[motion] = clip = UnityAudioClipFactory.Create("ThemeLoader." + motion, audio);
            source!.clip = clip;
            source.Play();
            failureLogged = false;
        }
        catch (Exception e) { Report(e); ReleaseSource(); }
    }

    internal void Stop()
    {
        if (!Alive(source)) return;
        try { source!.Stop(); source.clip = null; }
        catch (Exception e) { Report(e); ReleaseSource(); }
    }

    private void EnsureSource()
    {
        if (Alive(source)) return;
        ReleaseSource();
        owner = new GameObject("ThemeLoader.Live2dAudio") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(owner);
        source = owner!.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
    }

    private static bool Alive(UnityEngine.Object? value) => value is not null && !value.WasCollected && value != null;
    private void Report(Exception e)
    {
        if (!failureLogged) log.LogWarning("Live2D animation audio failed; retrying on the next trigger: " + e);
        failureLogged = true;
    }
    private void ReleaseSource()
    {
        var previous = source; source = null;
        if (Alive(previous))
            try { previous!.Stop(); previous.clip = null; UnityEngine.Object.Destroy(previous); } catch { }
        var previousOwner = owner; owner = null;
        if (Alive(previousOwner))
            try { previousOwner!.SetActive(false); UnityEngine.Object.Destroy(previousOwner); } catch { }
    }
    public void Dispose()
    {
        if (disposed) return;
        Stop(); disposed = true; ReleaseSource();
        foreach (var clip in clips.Values)
            if (Alive(clip)) UnityEngine.Object.Destroy(clip);
        clips.Clear(); data.Clear();
    }
}
