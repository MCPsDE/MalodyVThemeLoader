using NAudio.Wave;

namespace MalodyVThemeLoader.Plugin;

// Exercise the real Windows device without emitting test audio. Muting happens
// in the PCM provider, never via the endpoint's or game's volume settings.
internal sealed class MutedVoiceOutput : IWavePlayer
{
    private readonly WaveOutEvent device = new() { DesiredLatency = 40 };
    private MutedProvider? provider;
    internal long BytesRead => provider?.BytesRead ?? 0;
    internal long DevicePosition => Disposed ? 0 : device.GetPosition();
    internal bool Disposed { get; private set; }
    public PlaybackState PlaybackState => device.PlaybackState;
    public WaveFormat OutputWaveFormat => device.OutputWaveFormat;
    public float Volume { get => device.Volume; set => device.Volume = value; }
    public event EventHandler<StoppedEventArgs>? PlaybackStopped
    {
        add => device.PlaybackStopped += value;
        remove => device.PlaybackStopped -= value;
    }
    public void Init(IWaveProvider waveProvider) => device.Init(provider = new MutedProvider(waveProvider));
    public void Play() => device.Play();
    public void Pause() => device.Pause();
    public void Stop() => device.Stop();
    public void Dispose() { if (!Disposed) { device.Dispose(); Disposed = true; } }

    private sealed class MutedProvider : IWaveProvider
    {
        private readonly IWaveProvider input;
        internal long BytesRead;
        internal MutedProvider(IWaveProvider input) => this.input = input;
        public WaveFormat WaveFormat => input.WaveFormat;
        public int Read(byte[] buffer, int offset, int count)
        {
            var read = input.Read(buffer, offset, count);
            Interlocked.Add(ref BytesRead, read);
            Array.Clear(buffer, offset, read);
            return read;
        }
    }
}
