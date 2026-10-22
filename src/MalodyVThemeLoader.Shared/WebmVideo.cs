using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MalodyVThemeLoader.Shared;

public sealed class WebmVideo
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool HasAlpha { get; private set; }
    public string Codec { get; private set; } = "";

    public static WebmVideo Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length < 16 || stream.Length > 1024L * 1024 * 1024) throw new InvalidDataException("WebM 大小需在 1 GB 以内。");
        var result = new WebmVideo(); var docType = ""; var tracks = 0; var elements = 0;
        void Walk(long end, int depth)
        {
            if (depth > 8) throw new InvalidDataException("WebM 结构无效。");
            while (stream.Position < end)
            {
                if (++elements > 20000) throw new InvalidDataException("WebM 结构过于复杂。");
                var id = Vint(stream, false); var size = Vint(stream, true);
                var stop = size == ulong.MaxValue ? end : checked(stream.Position + (long)size);
                if (stop > end || stop < stream.Position) throw new InvalidDataException("WebM 文件不完整。");
                if (id is 0x1A45DFA3 or 0x18538067 or 0x1654AE6B) Walk(stop, depth + 1);
                else if (id == 0xAE)
                {
                    // Metadata order in a TrackEntry is not fixed. Collect first,
                    // then accept exactly one video track.
                    var fields = new Dictionary<ulong, byte[]>();
                    void Track(long limit, int nested)
                    {
                        if (nested > 3) throw new InvalidDataException("WebM 轨道无效。");
                        while (stream.Position < limit)
                        {
                            if (++elements > 20000) throw new InvalidDataException("WebM 轨道过于复杂。");
                            var field = Vint(stream, false); var length = Vint(stream, true);
                            if (length == ulong.MaxValue || length > (ulong)(limit - stream.Position)) throw new InvalidDataException("WebM 轨道不完整。");
                            var next = stream.Position + (long)length;
                            if (field == 0xE0) Track(next, nested + 1);
                            else if (field is 0x83 or 0x86 or 0xB0 or 0xBA or 0x53C0)
                            {
                                if (length > 64) throw new InvalidDataException("WebM 元数据无效。");
                                var bytes = new byte[(int)length]; if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) throw new EndOfStreamException(); fields[field] = bytes;
                            }
                            stream.Position = next;
                        }
                    }
                    Track(stop, 0);
                    ulong Number(ulong key) { ulong n = 0; if (fields.TryGetValue(key, out var b)) foreach (var v in b) n = (n << 8) | v; return n; }
                    if (Number(0x83) == 1)
                    {
                        tracks++; result.Width = checked((int)Number(0xB0)); result.Height = checked((int)Number(0xBA)); result.HasAlpha = Number(0x53C0) == 1;
                        result.Codec = fields.TryGetValue(0x86, out var codec) ? Encoding.ASCII.GetString(codec) : "";
                    }
                }
                else if (id == 0x4282 && size <= 16)
                {
                    var bytes = new byte[(int)size]; if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) throw new EndOfStreamException(); docType = Encoding.ASCII.GetString(bytes);
                }
                // Cluster frames are deliberately never buffered or decoded here.
                stream.Position = stop;
            }
        }
        try { Walk(stream.Length, 0); }
        catch (Exception e) when (e is EndOfStreamException or OverflowException) { throw new InvalidDataException("WebM 文件不完整。", e); }
        if (docType != "webm" || tracks != 1 || result.Width <= 0 || result.Height <= 0 || result.Width > 8192 || result.Height > 8192
            || (long)result.Width * result.Height > 16 * 1024 * 1024) throw new InvalidDataException("请选择单视频轨道、尺寸不超过 8192 的有效 WebM。");
        if (result.Codec != "V_VP8") throw new InvalidDataException("请将 WebM 转为 VP8 编码；当前不接受 VP9，以保证透明视频兼容。");
        return result;
    }
    private static ulong Vint(Stream stream, bool size)
    {
        var first = stream.ReadByte(); if (first <= 0) throw new InvalidDataException("WebM 数据无效。");
        var mask = 0x80; var length = 1; while ((first & mask) == 0) { mask >>= 1; length++; }
        if (length > (size ? 8 : 4)) throw new InvalidDataException("WebM 元素无效。");
        ulong value = (uint)(size ? first & (mask - 1) : first);
        for (var i = 1; i < length; i++) { var b = stream.ReadByte(); if (b < 0) throw new EndOfStreamException(); value = (value << 8) | (byte)b; }
        return size && value == (1UL << (length * 7)) - 1 ? ulong.MaxValue : value;
    }
}
