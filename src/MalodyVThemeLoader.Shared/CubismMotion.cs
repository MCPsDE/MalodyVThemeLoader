using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MalodyVThemeLoader.Shared;

// Evaluate the documented motion3 curves directly: IL2CPP strips the SDK's
// editor AnimationClip importer. No Animator/controller or game logic changes.
public sealed class CubismMotion
{
    public float Duration { get; private set; }
    public List<CubismMotionCurve> Curves { get; } = new();

    public static CubismMotion Read(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var duration = json.RootElement.GetProperty("Meta").GetProperty("Duration").GetSingle();
        if (!float.IsFinite(duration) || duration <= 0 || duration > 3600)
            throw new InvalidDataException("动画时长无效。");
        var motion = new CubismMotion { Duration = duration };
        foreach (var curve in json.RootElement.GetProperty("Curves").EnumerateArray())
        {
            var values = curve.GetProperty("Segments").EnumerateArray().Select(value => value.GetSingle()).ToArray();
            motion.Curves.Add(new CubismMotionCurve(
                curve.GetProperty("Target").GetString() ?? "",
                curve.GetProperty("Id").GetString() ?? "", values));
        }
        return motion;
    }
}

public sealed class CubismMotionCurve
{
    public string Target { get; }
    public string Id { get; }
    private readonly float[] values;

    public CubismMotionCurve(string target, string id, float[] segments)
    {
        Target = target;
        Id = id;
        values = segments;
        if (values.Length < 2 || values.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("动画曲线无效。");
        var time = values[0];
        for (var i = 2; i < values.Length;)
        {
            var type = values[i];
            var count = type == 1 ? 7 : type is 0 or 2 or 3 ? 3 : 0;
            if (count == 0 || i + count > values.Length || values[i + count - 2] < time)
                throw new InvalidDataException("动画曲线片段无效。");
            time = values[i + count - 2];
            i += count;
        }
    }

    public float Evaluate(float time)
    {
        var x = values[0];
        var y = values[1];
        if (time <= x) return y;
        for (var i = 2; i < values.Length;)
        {
            var type = (int)values[i];
            var end = i + (type == 1 ? 5 : 1);
            var nextX = values[end];
            var nextY = values[end + 1];
            if (time <= nextX)
            {
                if (type == 2) return y;
                if (type == 3) return nextY;
                if (nextX <= x) return nextY;
                if (type == 0) return y + (nextY - y) * (time - x) / (nextX - x);
                // Solve Bezier X instead of assuming evenly spaced controls.
                var low = 0f;
                var high = 1f;
                for (var step = 0; step < 20; step++)
                {
                    var t = (low + high) * .5f;
                    if (Bezier(x, values[i + 1], values[i + 3], nextX, t) < time) low = t;
                    else high = t;
                }
                return Bezier(y, values[i + 2], values[i + 4], nextY, (low + high) * .5f);
            }
            x = nextX;
            y = nextY;
            i += type == 1 ? 7 : 3;
        }
        return y;
    }

    private static float Bezier(float a, float b, float c, float d, float t)
    {
        var u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }
}
