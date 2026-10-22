using System.Runtime.InteropServices;
using MalodyVThemeLoader.Shared;
using SkiaSharp;

namespace MalodyVThemeLoader.ThemeManager;

// Standalone editor renderer. The Core library is loaded from the selected
// game installation; no Unity/game assemblies or user game process are loaded.
internal sealed unsafe class NativeCubismPreview : IDisposable
{
    private nint library;
    private nint moc;
    private nint model;
    private void* mocMemory;
    private void* modelMemory;
    private readonly Dictionary<string, nint> functions = new();
    private readonly List<SKBitmap> textures = new();
    private readonly List<SKShader> textureShaders = new();
    private readonly List<SKImage> textureImages = new();
    private readonly Dictionary<string, int> parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> parts = new(StringComparer.Ordinal);
    private float[] defaults = Array.Empty<float>();
    private float[] partDefaults = Array.Empty<float>();
    private float opacity = 1;
    private PreviewGpuContext? gpu;
    public bool IsGpu => gpu != null;
    public string LastFrameProfile { get; private set; } = "";
    public RectangleF Bounds { get; private set; }

    public NativeCubismPreview(string gameRoot, string live2dRoot, string file)
    {
        try
        {
            var native = Path.Combine(gameRoot, "Malody V_Data", "Plugins", "x86_64", "Live2DCubismCore.dll");
            if (!File.Exists(native)) throw new FileNotFoundException("游戏目录缺少 Live2D 运行库，请重新选择游戏目录。", native);
            library = NativeLibrary.Load(native);
            var document = Live2dModelDocument.Read(live2dRoot, file);
            var bytes = File.ReadAllBytes(Live2dModelDocument.Resolve(live2dRoot, document.Moc));
            if (bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("模型文件过大。");
            mocMemory = NativeMemory.AlignedAlloc((nuint)((bytes.Length + 63) & ~63), 64);
            bytes.CopyTo(new Span<byte>(mocMemory, bytes.Length));
            if (((delegate* unmanaged[Cdecl]<void*, uint, int>)Fn("csmHasMocConsistency"))(mocMemory, (uint)bytes.Length) == 0)
                throw new InvalidDataException("模型数据无效。");
            moc = ((delegate* unmanaged[Cdecl]<void*, uint, nint>)Fn("csmReviveMocInPlace"))(mocMemory, (uint)bytes.Length);
            if (moc == 0) throw new InvalidDataException("当前游戏运行库不支持此模型。");
            var size = ((delegate* unmanaged[Cdecl]<nint, uint>)Fn("csmGetSizeofModel"))(moc);
            if (size == 0 || size > 256 * 1024 * 1024) throw new InvalidDataException("模型内存尺寸无效。");
            modelMemory = NativeMemory.AlignedAlloc((nuint)((size + 15) & ~15u), 16);
            model = ((delegate* unmanaged[Cdecl]<nint, void*, uint, nint>)Fn("csmInitializeModelInPlace"))(moc, modelMemory, size);
            if (model == 0) throw new InvalidDataException("模型预览初始化失败。");
            foreach (var path in document.Textures)
            {
                var bitmap = SKBitmap.Decode(Live2dModelDocument.Resolve(live2dRoot, path));
                if (bitmap == null) throw new InvalidDataException("无法读取模型纹理。 ");
                textures.Add(bitmap);
                bitmap.SetImmutable();
                var image = SKImage.FromBitmap(bitmap); textureImages.Add(image);
                textureShaders.Add(image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp));
            }
            ReadIds("Parameter", parameters); ReadIds("Part", parts);
            defaults = new ReadOnlySpan<float>((void*)Ptr("csmGetParameterDefaultValues"), parameters.Count).ToArray();
            partDefaults = new ReadOnlySpan<float>((void*)Ptr("csmGetPartOpacities"), parts.Count).ToArray();
            Update(null, 0);
            var first = true; var minX = 0f; var minY = 0f; var maxX = 0f; var maxY = 0f;
            var count = Count("csmGetDrawableCount");
            var counts = (int*)Ptr("csmGetDrawableVertexCounts");
            var positions = (float**)Ptr("csmGetDrawableVertexPositions");
            for (var drawable = 0; drawable < count; drawable++)
                for (var vertex = 0; vertex < counts[drawable]; vertex++)
                {
                    var x = positions[drawable][vertex * 2]; var y = positions[drawable][vertex * 2 + 1];
                    if (first) { minX = maxX = x; minY = maxY = y; first = false; }
                    else { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                }
            Bounds = RectangleF.FromLTRB(minX, minY, maxX, maxY);
            if (Bounds.Width <= 0 || Bounds.Height <= 0) throw new InvalidDataException("模型没有可预览的网格。");
            try { gpu = new PreviewGpuContext(); }
            catch (Exception) { gpu = null; }
        }
        catch { Dispose(); throw; }
    }

    private nint Fn(string name)
    {
        if (!functions.TryGetValue(name, out var address)) functions[name] = address = NativeLibrary.GetExport(library, name);
        return address;
    }
    private nint Ptr(string name) => ((delegate* unmanaged[Cdecl]<nint, nint>)Fn(name))(model);
    private int Count(string name) => ((delegate* unmanaged[Cdecl]<nint, int>)Fn(name))(model);
    private void ReadIds(string prefix, Dictionary<string, int> target)
    {
        var count = Count("csmGet" + prefix + "Count"); var ids = (nint*)Ptr("csmGet" + prefix + "Ids");
        for (var i = 0; i < count; i++) target[Marshal.PtrToStringUTF8(ids[i]) ?? ""] = i;
    }

    public void Update(CubismMotion? motion, float time)
    {
        var values = (float*)Ptr("csmGetParameterValues");
        var minimums = (float*)Ptr("csmGetParameterMinimumValues"); var maximums = (float*)Ptr("csmGetParameterMaximumValues");
        var partValues = (float*)Ptr("csmGetPartOpacities");
        for (var i = 0; i < defaults.Length; i++) values[i] = defaults[i];
        for (var i = 0; i < partDefaults.Length; i++) partValues[i] = partDefaults[i];
        opacity = 1;
        if (motion != null)
            foreach (var curve in motion.Curves)
            {
                var value = curve.Evaluate(Math.Clamp(time, 0, motion.Duration));
                if (curve.Target == "Parameter" && parameters.TryGetValue(curve.Id, out var index)) values[index] = Math.Clamp(value, minimums[index], maximums[index]);
                else if (curve.Target == "PartOpacity" && parts.TryGetValue(curve.Id, out var part)) partValues[part] = Math.Clamp(value, 0, 1);
                else if (curve.Target == "Model" && curve.Id == "Opacity") opacity = Math.Clamp(value, 0, 1);
            }
        ((delegate* unmanaged[Cdecl]<nint, void>)Fn("csmUpdateModel"))(model);
    }

    public Bitmap Render(int width, int height, Color background, out RectangleF modelRectangle)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        gpu?.MakeCurrent();
        using var surface = gpu == null ? SKSurface.Create(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes)
            : SKSurface.Create(gpu.Context, true, bitmap.Info);
        if (surface == null) throw new InvalidOperationException("无法创建模型预览画面。");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(background.R, background.G, background.B));
        var factor = Math.Min((width - 32f) / Bounds.Width, (height - 32f) / Bounds.Height);
        modelRectangle = new RectangleF((width - Bounds.Width * factor) / 2, (height - Bounds.Height * factor) / 2, Bounds.Width * factor, Bounds.Height * factor);
        var rectangle = modelRectangle;
        var count = Count("csmGetDrawableCount");
        var order = (int*)Ptr("csmGetDrawableRenderOrders"); var flags = (byte*)Ptr("csmGetDrawableConstantFlags");
        var visible = (byte*)Ptr("csmGetDrawableDynamicFlags"); var alpha = (float*)Ptr("csmGetDrawableOpacities");
        var masks = (int**)Ptr("csmGetDrawableMasks"); var maskCounts = (int*)Ptr("csmGetDrawableMaskCounts");
        var textureIndices = (int*)Ptr("csmGetDrawableTextureIndices");
        var vertexCounts = (int*)Ptr("csmGetDrawableVertexCounts"); var positions = (float**)Ptr("csmGetDrawableVertexPositions");
        var uv = (float**)Ptr("csmGetDrawableVertexUvs"); var indexCounts = (int*)Ptr("csmGetDrawableIndexCounts"); var indices = (ushort**)Ptr("csmGetDrawableIndices");
        var multiply = (float*)Ptr("csmGetDrawableMultiplyColors"); var screen = (float*)Ptr("csmGetDrawableScreenColors");
        void DrawMesh(int drawable, bool mask)
        {
            var textureIndex = textureIndices[drawable];
            if (textureIndex < 0 || textureIndex >= textures.Count) throw new InvalidDataException("模型纹理索引无效。");
            var texture = textures[textureIndex]; var points = new SKPoint[vertexCounts[drawable]]; var coords = new SKPoint[points.Length];
            for (var v = 0; v < points.Length; v++)
            {
                points[v] = new SKPoint(rectangle.Left + (positions[drawable][v * 2] - Bounds.Left) * factor,
                    rectangle.Bottom - (positions[drawable][v * 2 + 1] - Bounds.Top) * factor);
                coords[v] = new SKPoint(uv[drawable][v * 2] * texture.Width, (1 - uv[drawable][v * 2 + 1]) * texture.Height);
            }
            var triangles = new ReadOnlySpan<ushort>(indices[drawable], indexCounts[drawable]).ToArray();
            using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, points, coords, null, triangles);
            using var paint = new SKPaint { Shader = textureShaders[textureIndex], IsAntialias = false, Color = SKColors.White.WithAlpha((byte)(Math.Clamp(mask ? 1 : alpha[drawable] * opacity, 0, 1) * 255)),
                BlendMode = mask ? SKBlendMode.SrcOver : (flags[drawable] & 1) != 0 ? SKBlendMode.Plus : (flags[drawable] & 2) != 0 ? SKBlendMode.Multiply : SKBlendMode.SrcOver };
            if (!mask && (multiply[drawable * 4] != 1 || multiply[drawable * 4 + 1] != 1 || multiply[drawable * 4 + 2] != 1
                || screen[drawable * 4] != 0 || screen[drawable * 4 + 1] != 0 || screen[drawable * 4 + 2] != 0))
            {
                var offset = drawable * 4;
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(new[] {
                    multiply[offset] * (1 - screen[offset]), 0, 0, 0, screen[offset] * 255,
                    0, multiply[offset + 1] * (1 - screen[offset + 1]), 0, 0, screen[offset + 1] * 255,
                    0, 0, multiply[offset + 2] * (1 - screen[offset + 2]), 0, screen[offset + 2] * 255,
                    0, 0, 0, 1f, 0 });
            }
            canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);
            paint.ColorFilter?.Dispose();
        }
        var sorted = Enumerable.Range(0, count).OrderBy(drawable => order[drawable]).ToArray();
        foreach (var drawable in sorted)
        {
            if ((visible[drawable] & 1) == 0 || alpha[drawable] <= 0) continue;
            if (maskCounts[drawable] == 0) { DrawMesh(drawable, false); continue; }
            var clipLeft = float.MaxValue; var clipTop = float.MaxValue; var clipRight = float.MinValue; var clipBottom = float.MinValue;
            for (var v = 0; v < vertexCounts[drawable]; v++)
            {
                var x = rectangle.Left + (positions[drawable][v * 2] - Bounds.Left) * factor;
                var y = rectangle.Bottom - (positions[drawable][v * 2 + 1] - Bounds.Top) * factor;
                clipLeft = Math.Min(clipLeft, x); clipRight = Math.Max(clipRight, x); clipTop = Math.Min(clipTop, y); clipBottom = Math.Max(clipBottom, y);
            }
            var clipBounds = new SKRect(clipLeft - 1, clipTop - 1, clipRight + 1, clipBottom + 1);
            canvas.Save(); canvas.ClipRect(clipBounds);
            canvas.SaveLayer(clipBounds, null);
            DrawMesh(drawable, false);
            using var maskPaint = new SKPaint { BlendMode = (flags[drawable] & 8) != 0 ? SKBlendMode.DstOut : SKBlendMode.DstIn };
            canvas.SaveLayer(clipBounds, maskPaint);
            for (var i = 0; i < maskCounts[drawable]; i++) DrawMesh(masks[drawable][i], true);
            canvas.Restore(); canvas.Restore(); canvas.Restore();
        }
        var meshTime = watch.ElapsedMilliseconds;
        canvas.Flush();
        if (gpu != null && !surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            throw new InvalidOperationException("无法读取模型预览画面。");
        var readTime = watch.ElapsedMilliseconds;
        var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var bits = result.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, result.PixelFormat);
        try
        {
            for (var row = 0; row < height; row++)
                new ReadOnlySpan<byte>((byte*)bitmap.GetPixels() + row * bitmap.RowBytes, width * 4)
                    .CopyTo(new Span<byte>((byte*)bits.Scan0 + row * bits.Stride, width * 4));
        }
        finally { result.UnlockBits(bits); }
        LastFrameProfile = $"mesh={meshTime}, read={readTime - meshTime}, copy={watch.ElapsedMilliseconds - readTime}ms";
        return result;
    }

    public void Dispose()
    {
        foreach (var shader in textureShaders) shader.Dispose(); textureShaders.Clear();
        foreach (var image in textureImages) image.Dispose(); textureImages.Clear();
        foreach (var texture in textures) texture.Dispose(); textures.Clear();
        if (modelMemory != null) { NativeMemory.AlignedFree(modelMemory); modelMemory = null; }
        if (mocMemory != null) { NativeMemory.AlignedFree(mocMemory); mocMemory = null; }
        model = moc = 0;
        gpu?.Dispose(); gpu = null;
        if (library != 0) { NativeLibrary.Free(library); library = 0; }
    }
}
