using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
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
    private MutedVoiceOutput? voiceOutput;
    private AudioClip factoryClip = null!;
    private GameObject unrelatedAudioOwner = null!;
    private int checks;
    private int audioPlays;
    private int intermediateFrames;
    private int siblingFrames;
    private readonly List<(Animator Animator, RectTransform Mask)> siblings = new();
    private bool finished;
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
            if (!baseline)
                for (var index = 1; index < 6; index++)
                {
                    var sibling = entrance.parent.Find($"EntranceIcon ({index})");
                    Require(sibling != null, "A repeated entrance fixture is missing.");
                    siblings.Add((sibling.GetComponent<Animator>(), sibling.Find("mask").GetComponent<RectTransform>()));
                }
            EntranceIconSlideController.Bind(SceneManager.GetActiveScene());
            initialSlideWorks = Mathf.Abs(mask.anchorMax.x - 1) < .001f && mask.anchoredPosition.x < -270;
            if (!baseline)
            {
                Require(initialSlideWorks, "Native RectTransform binding did not put the full-width tip outside the left edge.");
                foreach (var sibling in siblings)
                    Require(sibling.Mask.anchorMax.x == 1 && sibling.Mask.anchoredPosition.x < -270, "A later entrance was not bound to translation.");
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
                var siblingProgress = siblings.Where(item => item.Mask != null).Select(item =>
                    (item.Mask, State: item.Animator.GetCurrentAnimatorStateInfo(0), Progress: Mathf.Clamp01(item.Mask.anchorMax.x))).ToArray();
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
                    foreach (var sibling in siblingProgress)
                    {
                        Require(Mathf.Abs(sibling.Mask.rect.width - 274) < 1, "A later entrance tip still stretches.");
                        if ((sibling.State.IsName("hover") || sibling.State.IsName("leave")) && sibling.State.normalizedTime < 1)
                        {
                            Require(Mathf.Abs(sibling.Mask.anchoredPosition.x + 274 * (1 - sibling.Progress)) < 1,
                                "A later entrance's own animation curve was not mapped to translation.");
                            if (sibling.Progress > .01f && sibling.Progress < .99f) siblingFrames++;
                        }
                    }
                }
            }
            tick++;
            if (tick == (baseline ? 120 : 240))
            {
                if (!baseline) Require(intermediateFrames > 4 && audioPlays > 20, "Required animation/audio coverage was not executed.");
                if (!baseline) Require(siblingFrames > 20, "Repeated entrances were not exercised through animation.");
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
        voices = NewVoicePlayer();
        unrelatedAudioOwner = new GameObject("Unrelated Unity audio");
        unrelatedAudioOwner.AddComponent<AudioSource>();
        var actual = factoryClip = UnityAudioClipFactory.Create("UI factory validation", voiceData["idle"]);
        var samples = new Il2CppStructArray<float>(actual.samples);
        Require(actual.GetData(samples, 0) && Mathf.Abs(samples[0] - .5f) < .001f && Mathf.Abs(samples[1] + .5f) < .001f, "PCM samples were not copied into native audio.");
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private AnimationAudioPlayer NewVoicePlayer() => new(BepInEx.Logging.Logger.CreateLogSource("Voice validation"), voiceData,
        CreateVoiceOutput);
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private NAudio.Wave.IWavePlayer CreateVoiceOutput() => voiceOutput = new MutedVoiceOutput();
    private void ValidateAudio()
    {
        if (tick == 1 || tick == 70) animator.Play("hover", 0, 0);
        if (tick == 35) animator.Play("leave", 0, 0);
        for (var index = 0; index < siblings.Count; index++)
        {
            var sibling = siblings[index].Animator;
            if (sibling == null) continue;
            if (tick == 2 + index * 3 || tick == 70 + index * 2) sibling.Play("hover", 0, 0);
            if (tick == 35 + index * 2) sibling.Play("leave", 0, 0);
        }
        if (tick == 20 || tick == 90 || tick == 160) Resources.UnloadUnusedAssets();
        if (tick % 10 == 0) { GC.Collect(); IL2CPP.il2cpp_gc_collect(0); GC.WaitForPendingFinalizers(); }
        if (tick == 40) UnityEngine.Object.Destroy(factoryClip);
        if (tick == 60) UnityEngine.Object.Destroy(unrelatedAudioOwner);
        if (tick == 100)
        {
            var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("SceneVoiceTransition"));
            SceneManager.UnloadSceneAsync(previous);
        }
        if (tick % 5 != 0) return;
        var file = tick == 45 ? "result" : tick == 120 ? "tap" : tick % 10 == 0 ? "idle" : "tap";
        Require(voices!.Play(file), "Windows voice failed after GC, resource unload or scene change."); audioPlays++;
        Require(voiceOutput is { Disposed: false } && voiceOutput.OutputWaveFormat.SampleRate == voiceData[file].SampleRate,
            "Voice output did not retain the bound format.");
        if (tick >= 180 && tick % 10 == 0)
        {
            voices.Stop(); Require(voiceOutput!.Disposed, "Stopping voice kept the Windows output open.");
            voices.Dispose(); voices.Dispose(); voices = NewVoicePlayer();
        }
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
            passed, error, baseline, managedCastWorks, initialSlideWorks, audioErrors, checks, audioPlays, intermediateFrames, siblingFrames,
            scope = "isolated Unity f3 IL2CPP; real Windows voice output (software muted); WAV/OGG; GC, unused-asset unload, scene changes; not actual f2 game"
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(Paths.GameRootPath, "motion-frames.json"), JsonSerializer.Serialize(motionFrames));
        enabled = false; Application.Quit(passed ? 0 : 1);
    }
}
