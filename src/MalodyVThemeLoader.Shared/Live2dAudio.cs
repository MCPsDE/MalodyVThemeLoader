using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NVorbis;

namespace MalodyVThemeLoader.Shared;

public sealed class Live2dAudio
{
    public float[] Samples { get; private set; } = Array.Empty<float>();
    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    private const int MaximumSeconds = 300;
    private const int MaximumSamples = 16 * 1024 * 1024;

    public static Live2dAudio Decode(string file)
    {
        if (new FileInfo(file).Length > 64L * 1024 * 1024) throw new InvalidDataException("音频文件超过 64 MB。");
        var formatPath = file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase) ? file[..^6] : file;
        if (formatPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new NLayer.MpegFile(file);
            CheckFormat(reader.Channels, reader.SampleRate);
            var decoded = new List<float>(); var block = new float[reader.SampleRate * reader.Channels];
            int count;
            while ((count = reader.ReadSamples(block, 0, block.Length)) > 0)
            {
                if (decoded.Count + count > MaximumSamples || decoded.Count + count > reader.SampleRate * reader.Channels * MaximumSeconds)
                    throw new InvalidDataException("音频过长，请缩短音频或降低采样率。");
                for (var i = 0; i < count; i++) decoded.Add(float.IsFinite(block[i]) ? Math.Clamp(block[i], -1, 1) : 0);
            }
            if (decoded.Count == 0) throw new InvalidDataException("音频为空。");
            return new Live2dAudio { Channels = reader.Channels, SampleRate = reader.SampleRate, Samples = decoded.ToArray() };
        }
        if (formatPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new VorbisReader(file);
            CheckFormat(reader.Channels, reader.SampleRate);
            if (reader.TotalTime.TotalSeconds > MaximumSeconds) throw new InvalidDataException("音频不能超过 5 分钟。");
            var samples = new List<float>(); var buffer = new float[reader.SampleRate * reader.Channels];
            int read;
            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                if (samples.Count + read > reader.SampleRate * reader.Channels * MaximumSeconds || samples.Count + read > MaximumSamples)
                    throw new InvalidDataException("音频过长，请缩短音频或降低采样率。");
                for (var i = 0; i < read; i++) samples.Add(float.IsFinite(buffer[i]) ? Math.Clamp(buffer[i], -1, 1) : 0);
            }
            if (samples.Count == 0) throw new InvalidDataException("音频为空。");
            return new Live2dAudio { Channels = reader.Channels, SampleRate = reader.SampleRate, Samples = samples.ToArray() };
        }
        if (!formatPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请选择 OGG、WAV 或 MP3 音频。");
        using var stream = System.IO.File.OpenRead(file); using var input = new BinaryReader(stream, Encoding.ASCII);
        if (new string(input.ReadChars(4)) != "RIFF") throw new InvalidDataException("WAV 文件无效。");
        input.ReadUInt32(); if (new string(input.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAV 文件无效。");
        var format = 0; var channels = 0; var sampleRate = 0; var bits = 0; byte[]? data = null;
        while (stream.Position + 8 <= stream.Length)
        {
            var chunk = new string(input.ReadChars(4)); var size = input.ReadUInt32();
            if (size > stream.Length - stream.Position) throw new InvalidDataException("WAV 数据不完整。");
            var next = stream.Position + size + (size & 1);
            if (chunk == "fmt " && size >= 16)
            { format = input.ReadUInt16(); channels = input.ReadUInt16(); sampleRate = input.ReadInt32(); input.ReadInt32(); input.ReadUInt16(); bits = input.ReadUInt16(); }
            else if (chunk == "data") data = input.ReadBytes((int)size);
            stream.Position = next;
        }
        CheckFormat(channels, sampleRate);
        if (data == null || data.Length == 0 || !(format == 1 && bits is 8 or 16 or 24 or 32 || format == 3 && bits == 32))
            throw new InvalidDataException("WAV 请使用 PCM 或 32 位浮点格式。");
        var stride = bits / 8;
        if (data.Length % (stride * channels) != 0 || data.Length / stride > sampleRate * channels * MaximumSeconds || data.Length / stride > MaximumSamples)
            throw new InvalidDataException("WAV 时长或采样数据无效。");
        var result = new float[data.Length / stride];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * stride;
            var value = format == 3 ? BitConverter.ToSingle(data, offset) : bits == 8 ? (data[offset] - 128) / 128f
                : bits == 16 ? BitConverter.ToInt16(data, offset) / 32768f
                : bits == 24 ? ((data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16) << 8 >> 8) / 8388608f
                : BitConverter.ToInt32(data, offset) / 2147483648f;
            result[i] = float.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;
        }
        return new Live2dAudio { Channels = channels, SampleRate = sampleRate, Samples = result };
    }

    private static void CheckFormat(int channels, int sampleRate)
    {
        if (channels is < 1 or > 2 || sampleRate < 8000 || sampleRate > 192000) throw new InvalidDataException("音频需为单声道或立体声，采样率 8–192 kHz。");
    }

    public byte[] ToWave()
    {
        using var stream = new MemoryStream(); using var output = new BinaryWriter(stream, Encoding.ASCII);
        output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + Samples.Length * 2); output.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        output.Write(16); output.Write((short)1); output.Write((short)Channels); output.Write(SampleRate);
        output.Write(SampleRate * Channels * 2); output.Write((short)(Channels * 2)); output.Write((short)16);
        output.Write(Encoding.ASCII.GetBytes("data")); output.Write(Samples.Length * 2);
        foreach (var sample in Samples) output.Write((short)Math.Clamp((int)(sample * 32767), -32768, 32767));
        output.Flush(); return stream.ToArray();
    }
}
