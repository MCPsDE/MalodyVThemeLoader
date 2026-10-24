using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using NAudio.Wave;

namespace MalodyVThemeLoader.Plugin;

internal static class AnimationVoiceBindingValidation
{
    private static int checks;
    private static int plays;
    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void WaitFor(Func<bool> ready, string message)
    {
        var timer = Stopwatch.StartNew();
        while (!ready() && timer.ElapsedMilliseconds < 3000) Thread.Sleep(5);
        Require(ready(), message);
    }
    public static int Main(string[] args)
    {
        var root = Path.Combine(args[0], "live2d");
        var warnings = new List<string>();
        var outputs = new List<MutedVoiceOutput>();
        var devicesCreated = 0;
        using var log = new ManualLogSource("Voice binding validation");
        log.LogEvent += (_, e) => warnings.Add(e.Data.ToString()!);
        try
        {
            var manager = Assembly.LoadFrom(args[1]);
            var import = manager.GetType("MalodyVThemeLoader.ThemeManager.Live2dWorkspace", true)!.GetMethod("ImportAudio")!;
            var document = Live2dModelDocument.Read(root, "models/Murasame.model3.json");
            var motions = document.Motions.Select(motion => motion.File).Distinct().ToArray();
            var files = Directory.GetFiles(Path.Combine(root, "models", "sounds"), "*.wav");
            Require(motions.Length >= 7 && files.Length > 0, "The real model/voice sample is missing.");
            var binding = new Live2dBinding { Model = "models/Murasame.model3.json", IdleMotion = motions[0], TapMotion = motions[1] };
            for (var i = 0; i < motions.Length; i++)
            {
                var relative = (string)import.Invoke(null, new object[] { root, files[i % files.Length] })!;
                binding.MotionAudio[motions[i]] = relative;
            }
            for (var i = 0; i < 5; i++) binding.ResultMotions["M" + (i + 1)] = motions[i + 2];
            binding.ClickRegions.Add(new Live2dClickRegion { Name = "语音区域", X = .1f, Y = .1f, Width = .8f, Height = .8f, Motion = motions[3] });
            // An OGG voice goes through the same real manager importer as WAV.
            binding.MotionAudio[motions[^1]] = (string)import.Invoke(null, new object[] { root, Path.Combine(args[0], "voice.ogg") })!;
            var json = Path.Combine(root, Live2dModelDocument.BindingFile);
            File.WriteAllText(json, JsonSerializer.Serialize(binding, JsonDefaults.Options));
            binding = JsonSerializer.Deserialize<Live2dBinding>(File.ReadAllText(json), JsonDefaults.Options)!;
            document.ValidateBinding(root, binding);
            Require(binding.MotionAudio.Count == motions.Length && binding.ResultMotions.Count == 5, "Persisted voice bindings were lost.");
            var decoded = binding.MotionAudio.ToDictionary(pair => pair.Key, pair => Live2dAudio.Decode(Live2dModelDocument.Resolve(root, pair.Value)));
            var failNext = false;
            IWavePlayer CreateOutput()
            {
                devicesCreated++;
                if (failNext) { failNext = false; throw new IOException("Injected unavailable voice device"); }
                var output = new MutedVoiceOutput(); outputs.Add(output); return output;
            }
            using var player = new AnimationAudioPlayer(log, decoded, CreateOutput);
            Require(devicesCreated == 0, "Model binding eagerly opened an audio device.");
            for (var pass = 0; pass < 3; pass++)
            {
                foreach (var motion in motions)
                {
                    var previous = outputs.LastOrDefault();
                    Require(player.Play(motion), "Bound voice failed: " + motion); plays++;
                    if (previous is not null) Require(previous.Disposed, "The preceding animation voice was not released.");
                    var current = outputs[^1];
                    Require(current.OutputWaveFormat.SampleRate == decoded[motion].SampleRate
                        && current.OutputWaveFormat.Channels == decoded[motion].Channels, "The voice format changed.");
                    WaitFor(() => current.BytesRead > 0 && current.DevicePosition > 0, "The Windows device did not consume PCM.");
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    Require(!current.Disposed, "Managed GC released a playing voice.");
                }
            }
            for (var i = 0; i < 120; i++)
            {
                Require(player.Play(motions[i % motions.Length]), "A rapidly switched voice failed."); plays++;
            }
            Require(outputs.Take(outputs.Count - 1).All(output => output.Disposed), "Rapid switches leaked an output.");
            Require(!player.Play("unbound") && outputs[^1].Disposed, "An unbound action did not stop the previous voice.");
            var oggMotion = motions[^1];
            Require(player.Play(oggMotion), "OGG voice failed."); plays++;
            WaitFor(() => outputs[^1].PlaybackState == PlaybackState.Stopped, "OGG playback did not finish naturally.");
            player.Stop(); player.Stop();
            Require(outputs.All(output => output.Disposed), "Stop left an audio output open.");
            Require(warnings.Count == 0, "Unexpected native playback warning: " + string.Join("\n", warnings));
            failNext = true;
            Require(!player.Play(motions[0]), "An unavailable output was reported as successful.");
            Require(warnings.Count == 1 && warnings[0].Contains(motions[0]) && warnings[0].Contains("Injected unavailable"), "Failure diagnostics lost the action or cause.");
            Require(player.Play(motions[0]), "Voice did not recover on its next trigger."); plays++;
            player.Dispose(); player.Dispose();
            Require(!player.Play(motions[0]) && outputs.All(output => output.Disposed), "Disposed voices reopened a device or leaked a handle.");
            // Apply/restore loops create the same voice owner as model reloads.
            for (var i = 0; i < 20; i++)
            {
                using var reload = new AnimationAudioPlayer(log, decoded, CreateOutput);
                Require(reload.Play(motions[i % motions.Length]), "A reapplied theme could not play its voice."); plays++;
            }
            Require(outputs.All(output => output.Disposed), "Theme reload leaked an audio output.");
            var result = new { passed = true, checks, plays, motions = motions.Length, sampleVoices = files.Length, devicesCreated,
                expectedDeviceFailures = 1, realWindowsOutput = true, mutedInSoftware = true,
                scope = "actual model WAVs and OGG imported by manager, persisted/reopened binding, real device PCM consumption, rapid switch, GC, stop, natural end, reload, injected device failure; no actual game launched" };
            var text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(args[0], "result.json"), text); Console.WriteLine(text); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); Console.Error.WriteLine(string.Join("\n", warnings)); return 1; }
    }
}
