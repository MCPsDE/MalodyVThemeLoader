using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

[BepInPlugin("test.presentation.lifetime", "Presentation lifetime probe", "1.0.0")]
public sealed class PresentationProbe : BasePlugin
{
    public override void Load() => AddComponent<PresentationDriver>();
}

public sealed class PresentationDriver : MonoBehaviour
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ConstructClip();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CreateSound(IntPtr clip, IntPtr name, int samples, int channels, int frequency, bool stream);
    private static readonly ConstructClip Construct = IL2CPP.ResolveICall<ConstructClip>("UnityEngine.AudioClip::Construct_Internal");
    private static readonly CreateSound Create = IL2CPP.ResolveICall<CreateSound>("UnityEngine.AudioClip::CreateUserSound");
    private readonly List<string> audioErrors = new();
    private readonly List<AudioClip> clips = new();
    private readonly bool baseline = Environment.GetEnvironmentVariable("MALODY_PRESENTATION_BASELINE") == "1";
    private readonly List<UnityEngine.Object> keepers = new();
    private readonly List<object> motionFrames = new();
    private AnimationAudioPlayer? voices;
    private Dictionary<string, Live2dAudio> voiceData = null!;
    private int checks;
    private int audioPlays;
    private int intermediateFrames;
    private int recoveredHandles;
    private bool finished;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool managedCastWorks;
    private bool initialSlideWorks;
    private int tick;
    private Animator animator = null!;
    private RectTransform mask = null!;
    public PresentationDriver(IntPtr pointer) : base(pointer) { }
    private void Start()
    {
        try
        {
            var entrance = SceneManager.GetActiveScene().GetRootGameObjects().Single(root => root.name == "CanvasForegroundDPI").transform.Find("LeftPanel/EntranceIcon");
            var found = entrance.Find("mask"); managedCastWorks = found is RectTransform;
            mask = found.GetComponent<RectTransform>(); animator = entrance.GetComponent<Animator>();
            EntranceIconSlideController.Bind(SceneManager.GetActiveScene());
            initialSlideWorks = Mathf.Abs(mask.anchorMax.x - 1) < .001f && mask.anchoredPosition.x < -270;
            if (!baseline)
            {
                Require(initialSlideWorks, "Native RectTransform binding did not put the full-width tip outside the left edge.");
                AudioListener.volume = 0; Time.captureFramerate = 60;
                PrepareAudio();
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void Update()
    {
        if (finished) return;
        try
        {
            if (!baseline) { ValidateAudio(); return; }
            if (tick % 10 == 0)
            {
                var stage = "create";
                try
                {
                    // Replay the shipped game's unstripped helper as well as
                    // the native method retained by this isolated player.
                    var clip = tick % 20 == 0 ? CreateLikeGame("Voice " + tick) : AudioClip.Create("Voice " + tick, 2205, 1, 22050, false);
                    clips.Add(clip); stage = "SetData";
                    if (!clip.SetData(new float[2205], 0)) throw new Exception("SetData returned false");
                    stage = "after GC"; GC.Collect(); GC.WaitForPendingFinalizers(); IL2CPP.il2cpp_gc_collect(0);
                    if (clip.samples != 2205) throw new Exception("Clip lost samples");
                }
                catch (Exception e) { audioErrors.Add(stage + ": " + e); }
            }
            if (tick == 35) Resources.UnloadUnusedAssets();
            if (tick == 70)
                foreach (var clip in clips)
                    try { if (clip.samples != 2205) throw new Exception("Lost cached clip"); }
                    catch (Exception e) { audioErrors.Add("after UnloadUnusedAssets: " + e); }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void LateUpdate()
    {
        if (finished) return;
        try
        {
            if (mask != null)
            {
                var progress = Mathf.Clamp01(mask.anchorMax.x);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                EntranceIconSlideController.Tick();
                if (!baseline)
                {
                    Require(Mathf.Abs(mask.rect.width - 274) < 1 && Mathf.Abs(mask.Find("tip").GetComponent<RectTransform>().rect.width - 274) < 1, "Tip was stretched by the native animation.");
                    if ((state.IsName("hover") || state.IsName("leave")) && state.normalizedTime < 1)
                    {
                        Require(Mathf.Abs(mask.anchoredPosition.x + 274 * (1 - progress)) < 1, "Native animation progress was not mapped to translation.");
                        if (progress > .01f && progress < .99f) intermediateFrames++;
                    }
                    if (tick < 100) motionFrames.Add(new { tick, progress, x = mask.anchoredPosition.x, time = state.normalizedTime, hover = state.IsName("hover"), leave = state.IsName("leave") });
                }
            }
            tick++;
            if (tick == (baseline ? 120 : 240))
            {
                if (!baseline) Require(intermediateFrames > 4 && recoveredHandles == 2 && audioPlays > 20, "Required animation/audio recovery coverage was not executed.");
                Finish(true, "");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private void PrepareAudio()
    {
        var wave = Path.Combine(Paths.GameRootPath, "voice.wav");
        using (var writer = new BinaryWriter(File.Create(wave)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 4410 * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(4410 * 2);
            for (var i = 0; i < 4410; i++) writer.Write((short)(i % 2 == 0 ? 16384 : -16384));
        }
        voiceData = new() { ["idle"] = Live2dAudio.Decode(wave), ["tap"] = Live2dAudio.Decode(Path.Combine(Paths.GameRootPath, "voice.ogg")), ["result"] = Live2dAudio.Decode(wave) };
        voices = new AnimationAudioPlayer(BepInEx.Logging.Logger.CreateLogSource("Voice validation"), voiceData);
        var actual = CachedClips()["idle"]; var samples = new Il2CppStructArray<float>(actual.samples);
        Require(actual.GetData(samples, 0) && Mathf.Abs(samples[0] - .5f) < .001f && Mathf.Abs(samples[1] + .5f) < .001f, "PCM samples were not copied into native audio.");
    }
    private T Field<T>(string name) => (T)typeof(AnimationAudioPlayer).GetField(name, Private)!.GetValue(voices)!;
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private Dictionary<string, AudioClip> CachedClips() => Field<Dictionary<string, AudioClip>>("clips");
    private void ValidateAudio()
    {
        if (tick == 1 || tick == 70) animator.Play("hover", 0, 0);
        if (tick == 35) animator.Play("leave", 0, 0);
        if (tick == 20 || tick == 90 || tick == 160) Resources.UnloadUnusedAssets();
        if (tick % 10 == 0) { GC.Collect(); IL2CPP.il2cpp_gc_collect(0); GC.WaitForPendingFinalizers(); }
        if (tick == 40) UnityEngine.Object.Destroy(CachedClips()["result"]);
        if (tick == 60) UnityEngine.Object.Destroy(Field<GameObject>("owner"));
        if (tick == 100)
        {
            var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("SceneVoiceTransition"));
            SceneManager.UnloadSceneAsync(previous);
        }
        if (tick == 120)
        {
            var clip = CachedClips()["tap"]; keepers.Add(new AudioClip(clip.Pointer)); InvalidateHandle(clip);
        }
        if (tick == 140)
        {
            var source = Field<AudioSource>("source"); keepers.Add(new AudioSource(source.Pointer)); InvalidateHandle(source);
        }
        if (tick % 5 != 0) return;
        var file = tick == 45 ? "result" : tick == 120 ? "tap" : tick % 10 == 0 ? "idle" : "tap";
        voices!.Play(file); audioPlays++;
        var active = Field<AudioSource>("source");
        Require(active != null && !active.WasCollected && active.clip != null && active.clip.samples == voiceData[file].Samples.Length / voiceData[file].Channels, "Bound voice did not recover after destruction, GC or scene change.");
        Require((active.clip.hideFlags & HideFlags.DontUnloadUnusedAsset) != 0, "Cached audio is not retained by Unity.");
        foreach (var pair in CachedClips())
            if (tick != 40 || pair.Key != "result") Require(!pair.Value.WasCollected && pair.Value != null, "An inactive cached voice was unloaded.");
        if (tick >= 180 && tick % 10 == 0)
        {
            voices.Stop(); Require(active.clip == null, "Stopping voice kept the native clip bound.");
            voices.Dispose(); voices.Dispose(); voices = new AnimationAudioPlayer(BepInEx.Logging.Logger.CreateLogSource("Reload validation"), voiceData);
        }
    }
    private void InvalidateHandle(Il2CppObjectBase value)
    {
        // Fault injection for the user's exact exception. This is not a claim
        // that the isolated f3 player naturally reproduces the f2 failure.
        var field = typeof(Il2CppObjectBase).GetField("myGcHandle", Private)!;
        IL2CPP.il2cpp_gchandle_free((IntPtr)field.GetValue(value)!); field.SetValue(value, IntPtr.Zero);
        try { _ = value.Pointer; throw new Exception("Fault injection did not invalidate the native handle."); }
        catch (Il2CppInterop.Runtime.ObjectCollectedException) { recoveredHandles++; }
    }
    private void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static AudioClip CreateLikeGame(string name)
    {
        var clip = Il2CppObjectPool.Get<AudioClip>(Construct());
        Create(clip.Pointer, IL2CPP.ManagedStringToIl2Cpp(name), 2205, 1, 22050, false);
        return clip;
    }
    private void Finish(bool passed, string error)
    {
        if (finished) return; finished = true;
        voices?.Dispose(); foreach (var keeper in keepers) if (!keeper.WasCollected && keeper != null) UnityEngine.Object.Destroy(keeper);
        File.WriteAllText(Environment.GetEnvironmentVariable("MALODY_PRESENTATION_RESULT")!, JsonSerializer.Serialize(new {
            passed, error, baseline, managedCastWorks, initialSlideWorks, audioErrors, checks, audioPlays, intermediateFrames, recoveredHandles,
            scope = "isolated Unity f3 IL2CPP; WAV/OGG; GC, unused-asset unload, scene changes and explicit collected-handle fault injection; not actual f2 game"
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(Paths.GameRootPath, "motion-frames.json"), JsonSerializer.Serialize(motionFrames));
        enabled = false; Application.Quit(passed ? 0 : 1);
    }
}
