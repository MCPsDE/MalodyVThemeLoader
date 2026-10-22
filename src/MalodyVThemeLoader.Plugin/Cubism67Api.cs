using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

// Malody 6.7 / Cubism SDK: property names in the shipped IL2CPP assembly are
// obfuscated. Keep that version-specific surface here. Editor validation uses
// the corresponding public SDK properties, with the same construction code.
internal static class Cubism67Api
{
#if THEMELOADER_EDITOR_TEST
    public static int TextureIndex(CubismDrawable d) => d.TextureIndex;
    public static Vector3[] Vertices(CubismDrawable d) => d.VertexPositions;
    public static bool Masked(CubismDrawable d) => d.IsMasked;
    public static string MaterialName(CubismDrawable d) => "Unlit"
        + (d.BlendAdditive ? "Additive" : d.MultiplyBlend ? "Multiply" : "")
        + (d.IsMasked ? "Masked" + (d.IsInverted ? "Inverted" : "") : "")
        + (d.IsDoubleSided ? "" : "Culling");
    public static void SetMaterial(CubismRenderer r, Material m) => r.Material = m;
    public static void SetTexture(CubismRenderer r, Texture2D t) => r.MainTexture = t;
    public static void StartMask(CubismMaskController mask) => mask.SendMessage("Start");
#else
    public static int TextureIndex(CubismDrawable d) => d.bogz;
    public static Vector3[] Vertices(CubismDrawable d) => d.bohe.ToArray();
    public static bool Masked(CubismDrawable d) => d.bohi;
    public static string MaterialName(CubismDrawable d) => "Unlit"
        + (d.bohk ? "Additive" : d.bohl ? "Multiply" : "")
        + (d.bohi ? "Masked" + (d.bohj ? "Inverted" : "") : "")
        + (d.bohh ? "" : "Culling");
    public static void SetMaterial(CubismRenderer r, Material m) => r.boev = m;
    public static void SetTexture(CubismRenderer r, Texture2D t) => r.boew = t;
    public static void StartMask(CubismMaskController mask) => mask.Start();
#endif
}
