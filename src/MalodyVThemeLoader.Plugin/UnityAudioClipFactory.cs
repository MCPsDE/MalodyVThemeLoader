using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal static class UnityAudioClipFactory
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ConstructClip();
    // Same Unity binding used by the game's unstripped AudioClip.Create.
    // Build a fresh wrapper: its global wrapper pool does not reject a cached
    // object whose IL2CPP handle was collected. Never change the shared pool.
    private static readonly ConstructClip Construct = IL2CPP.ResolveICall<ConstructClip>("UnityEngine.AudioClip::Construct_Internal");

    internal static AudioClip Create(string name, Live2dAudio data)
    {
        var pointer = Construct();
        if (pointer == IntPtr.Zero) throw new InvalidDataException("Unity 无法创建音频。");
        var clip = new AudioClip(pointer);
        try
        {
            // The managed cache is outside Unity's asset reachability walk.
            // Retain even voices that are not currently assigned to a source.
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clip.CreateUserSound(name, data.Samples.Length / data.Channels, data.Channels, data.SampleRate, false);
            var samples = new Il2CppStructArray<float>(data.Samples);
            try
            {
                if (!clip.SetData(samples, 0)) throw new InvalidDataException("Unity 无法写入音频采样。");
            }
            finally { GC.KeepAlive(samples); GC.KeepAlive(clip); }
            return clip;
        }
        catch
        {
            if (!clip.WasCollected && clip != null) UnityEngine.Object.Destroy(clip);
            throw;
        }
    }
}
