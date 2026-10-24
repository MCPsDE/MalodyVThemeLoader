using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using NAudio.Wave;

namespace MalodyVThemeLoader.Plugin;

// Animation voices own a Windows output channel. No Unity object or IL2CPP
// wrapper participates in binding, playback, callbacks, or disposal.
internal sealed class AnimationAudioPlayer : IDisposable
{
    private readonly ManualLogSource log;
    private readonly Dictionary<string, Live2dAudio> data;
    private readonly Dictionary<string, byte[]> waves = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<IWavePlayer> createOutput;
    private IWavePlayer? output;
    private WaveFileReader? reader;
    private EventHandler<StoppedEventArgs>? stopped;
    private bool disposed;
    private string lastFailure = "";

    internal AnimationAudioPlayer(ManualLogSource log, IReadOnlyDictionary<string, Live2dAudio> audio,
        Func<IWavePlayer>? createOutput = null)
    {
        this.log = log;
        data = audio.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        this.createOutput = createOutput ?? (() => new WaveOutEvent { DesiredLatency = 80 });
        // Open the device only when a voice is triggered, after model creation
        // has committed. An unavailable output must not reject the model.
    }

    internal bool Play(string motion)
    {
        if (disposed) return false;
        Stop();
        if (!data.TryGetValue(motion, out var audio)) return false;
        try
        {
            if (!waves.TryGetValue(motion, out var wave)) waves[motion] = wave = audio.ToWave();
            reader = new WaveFileReader(new MemoryStream(wave, writable: false));
            output = createOutput();
            // A worker callback belongs to this trigger, even if a newer
            // action has already replaced it. Log its original motion only.
            stopped = (_, e) =>
            {
                if (e.Exception is not null) log.LogWarning("Live2D Windows voice stopped for " + motion + ": " + e.Exception);
            };
            output.PlaybackStopped += stopped;
            output.Init(reader);
            output.Play();
            lastFailure = "";
            return true;
        }
        catch (Exception e) { Report(motion, e); Stop(); return false; }
    }

    internal void Stop()
    {
        var previous = output; output = null;
        var previousReader = reader; reader = null;
        var previousStopped = stopped; stopped = null;
        if (previous is not null)
        {
            previous.PlaybackStopped -= previousStopped;
            try { previous.Dispose(); }
            catch (Exception e) { Report("stop", e); }
        }
        previousReader?.Dispose();
    }

    private void Report(string motion, Exception e)
    {
        var failure = motion + ": " + e;
        if (failure != lastFailure) log.LogWarning("Live2D voice failed for " + failure);
        lastFailure = failure;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Stop(); waves.Clear(); data.Clear();
    }
}
