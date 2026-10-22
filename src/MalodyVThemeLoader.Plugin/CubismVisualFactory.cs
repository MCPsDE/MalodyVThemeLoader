using System;
using System.IO;
using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

internal sealed class CubismVisual : IDisposable
{
    public CubismModel Model = null!;
    public CubismMoc Moc = null!;
    public Bounds LocalBounds;

    public void Dispose()
    {
        // OnDestroy releases native model references before the moc asset.
        if (Model != null) UnityEngine.Object.DestroyImmediate(Model.gameObject);
        if (Moc != null) UnityEngine.Object.DestroyImmediate(Moc);
    }
}

internal static class CubismVisualFactory
{
    public static void Show(CubismVisual visual)
    {
        visual.Model.gameObject.SetActive(true);
        // The SDK swaps drawable buffers. Prime both before committing so its
        // first visible frame already has the correct mesh order and opacity.
        visual.Model.ForceUpdateNow();
        visual.Model.ForceUpdateNow();
        var mask = visual.Model.GetComponent<CubismMaskController>();
        if (mask != null)
        {
            Cubism67Api.StartMask(mask); // AddSource is idempotent when Unity later calls Start.
            mask.OnLateUpdate();
        }
    }

    public static CubismVisual Create(byte[] bytes, Texture2D[] textures)
    {
        var result = new CubismVisual();
        try
        {
            result.Moc = CubismMoc.CreateFrom(bytes, true);
            if (result.Moc == null) throw new InvalidDataException("游戏无法读取这个 moc3 版本。");
            result.Model = CubismModel.InstantiateFrom(result.Moc);
            if (result.Model == null) throw new InvalidDataException("模型创建失败。");
            result.Model.gameObject.SetActive(false);
            result.Model.name = "ThemeLoader.Live2D";
            var controller = result.Model.gameObject.AddComponent<CubismRenderController>();
            controller.TryInitializeRenderers();
            var drawables = result.Model.GetComponentsInChildren<CubismDrawable>(true);
            if (drawables.Length == 0) throw new InvalidDataException("模型没有可显示的网格。");
            var firstVertex = true;
            var masked = false;
            foreach (var drawable in drawables)
            {
                var index = Cubism67Api.TextureIndex(drawable);
                if (index < 0 || index >= textures.Length) throw new InvalidDataException("模型纹理索引无效。");
                var material = Resources.Load<Material>("Live2D/Cubism/Materials/" + Cubism67Api.MaterialName(drawable));
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new InvalidDataException("游戏缺少模型需要的 Live2D 材质。");
                var renderer = drawable.GetComponent<CubismRenderer>();
                Cubism67Api.SetMaterial(renderer, material);
                Cubism67Api.SetTexture(renderer, textures[index]);
                masked |= Cubism67Api.Masked(drawable);
                foreach (var vertex in Cubism67Api.Vertices(drawable))
                {
                    if (firstVertex) { result.LocalBounds = new Bounds(vertex, Vector3.zero); firstVertex = false; }
                    else result.LocalBounds.Encapsulate(vertex);
                }
            }
            if (firstVertex || result.LocalBounds.size.y <= .001f)
                throw new InvalidDataException("模型尺寸无效。");
            if (masked) result.Model.gameObject.AddComponent<CubismMaskController>();
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public static void Fit(CubismVisual visual, Transform parent, Bounds destination,
        int layer, float scale, float offsetX, float offsetY)
    {
        var transform = visual.Model.transform;
        transform.SetParent(parent, false);
        var size = visual.LocalBounds.size;
        var factor = Math.Min(destination.size.x / Math.Max(size.x, .001f), destination.size.y / size.y) * scale;
        if (factor <= 0 || float.IsNaN(factor) || float.IsInfinity(factor))
            throw new InvalidDataException("角色显示区域无效。");
        transform.localScale = new Vector3(factor, factor, factor);
        transform.localPosition = destination.center - visual.LocalBounds.center * factor
            + new Vector3(destination.size.x * offsetX, destination.size.y * offsetY, 0);
        foreach (var child in visual.Model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }
}
