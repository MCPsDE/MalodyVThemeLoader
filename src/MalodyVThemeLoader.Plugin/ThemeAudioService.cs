using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using Malody.Audio;
using System.Reflection;

namespace MalodyVThemeLoader.Plugin;

internal sealed class ThemeAudioService : IDisposable
{
    private readonly ManualLogSource log;
    private readonly LoadedTheme theme;
    private readonly ThemeResources rules;
    private readonly Dictionary<string, AudioClip> clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> failed = new(StringComparer.OrdinalIgnoreCase);
    private GameObject? owner;
    private AudioSource? effects;
    private AudioSource? music;
    private int samplesLoaded;
    private static readonly Func<SoundManager, float>? ReadEffectVolume = BindEffectVolume();

    public ThemeAudioService(ManualLogSource log, LoadedTheme theme, ThemeResources rules)
    {
        this.log = log; this.theme = theme; this.rules = rules;
    }

    public bool PlayEffect(SoundManager manager, int effect)
    {
        if (effect < 0 || effect >= UiAudioEvents.Effects.Length || ReadEffectVolume == null) return false;
        try
        {
            var clip = Resolve(UiAudioEvents.Effects[effect]);
            if (clip == null) return false;
            EnsureSources();
            var rawVolume = ReadEffectVolume(manager);
            var volume = float.IsFinite(rawVolume) ? Math.Clamp(rawVolume, 0, 1) : 0;
            effects!.PlayOneShot(clip, volume);
            return true;
        }
        catch (Exception e) { Report(UiAudioEvents.Effects[effect], e); return false; }
    }

    private static Func<SoundManager, float>? BindEffectVolume()
    {
        // In the supported adapter, effectVol is the only instance float on
        // SoundManager. Its generated alias changes; the static interval is
        // excluded. An unknown layout keeps the original UI sound.
        try
        {
            var getters = typeof(SoundManager).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(float) && p.GetMethod != null && p.GetIndexParameters().Length == 0).ToArray();
            return getters.Length == 1 ? getters[0].GetMethod!.CreateDelegate<Func<SoundManager, float>>() : null;
        }
        catch { return null; }
    }

    public bool PlayResult(string key, float volume)
    {
        if (key != "music_finish" || SceneManager.GetActiveScene().name != "SceneResult") return false;
        try
        {
            var clip = Resolve(key);
            if (clip == null) return false;
            EnsureSources();
            music!.Stop(); music.clip = clip; music.volume = float.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
            music.Play();
            return true;
        }
        catch (Exception e) { Report(key, e); return false; }
    }

    public void StopResult() { if (music != null) music.Stop(); }

    public void Tick()
    {
        var scene = SceneManager.GetActiveScene().name;
        if (scene != "SceneResult") StopResult();
        if (ThemeResources.IsAllowedScene(scene)) return;
        if (effects != null && effects.isPlaying) effects.Stop();
        StopResult();
    }

    private AudioClip? Resolve(string eventName)
    {
        var rule = rules.ResolveAudio(eventName, SceneManager.GetActiveScene().name);
        if (rule == null) return null;
        var path = PathRules.GetSafeCombinedPath(PathRules.GetSafeCombinedPath(theme.ThemeDirectory, theme.Manifest.AudioRoot), rule.File);
        if (clips.TryGetValue(path, out var cached)) return cached;
        if (failed.Contains(path)) return null;
        AudioClip? clip = null;
        try
        {
            var decoded = Live2dAudio.Decode(path);
            if (samplesLoaded + decoded.Samples.Length > 32 * 1024 * 1024) throw new InvalidDataException("Theme UI audio exceeds the decoded cache limit.");
            clip = AudioClip.Create("ThemeLoader UI " + eventName, decoded.Samples.Length / decoded.Channels, decoded.Channels, decoded.SampleRate, false);
            if (clip == null || !clip.SetData(decoded.Samples, 0)) throw new InvalidDataException("Unity could not prepare UI audio.");
            samplesLoaded += decoded.Samples.Length;
            clips[path] = clip;
            return clip;
        }
        catch (Exception e)
        {
            if (clip != null) UnityEngine.Object.Destroy(clip);
            failed.Add(path); Report(eventName, e);
            return null;
        }
    }

    private void EnsureSources()
    {
        if (owner != null) return;
        owner = new GameObject("ThemeLoader UI Audio") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(owner);
        effects = owner.AddComponent<AudioSource>(); music = owner.AddComponent<AudioSource>();
        foreach (var source in new[] { effects, music })
        { source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.volume = 1; }
    }

    private void Report(string key, Exception e)
    {
        if (failed.Add("event:" + key)) log.LogWarning($"UI audio '{key}' failed; using original: {e.Message}");
    }

    public void Dispose()
    {
        if (effects != null) effects.Stop();
        StopResult();
        if (owner != null) UnityEngine.Object.Destroy(owner);
        foreach (var clip in clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
        clips.Clear(); owner = null; effects = null; music = null;
    }
}
