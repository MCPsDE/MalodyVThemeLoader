using Live2D.Cubism.Core;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Rendering.Masking;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

// Generated property aliases change between game builds. Bind managed wrapper
// delegates by the retained SDK accessor method names, so source and runtime
// both work with the current interop cache. No native pointers or detours.
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
    private static readonly Func<CubismDrawable, int> GetTextureIndex = Bind<Func<CubismDrawable, int>>(typeof(CubismDrawable), "get_TextureIndex");
    private static readonly Func<CubismDrawable, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>> GetVertices =
        Bind<Func<CubismDrawable, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>>>(typeof(CubismDrawable), "get_VertexPositions");
    private static readonly Func<CubismDrawable, bool> IsDoubleSided = Bind<Func<CubismDrawable, bool>>(typeof(CubismDrawable), "get_IsDoubleSided");
    private static readonly Func<CubismDrawable, bool> IsMasked = Bind<Func<CubismDrawable, bool>>(typeof(CubismDrawable), "get_IsMasked");
    private static readonly Func<CubismDrawable, bool> IsInverted = Bind<Func<CubismDrawable, bool>>(typeof(CubismDrawable), "get_IsInverted");
    private static readonly Func<CubismDrawable, bool> BlendAdditive = Bind<Func<CubismDrawable, bool>>(typeof(CubismDrawable), "get_BlendAdditive");
    private static readonly Func<CubismDrawable, bool> MultiplyBlend = Bind<Func<CubismDrawable, bool>>(typeof(CubismDrawable), "get_MultiplyBlend");
    private static readonly Action<CubismRenderer, Material> AssignMaterial = Bind<Action<CubismRenderer, Material>>(typeof(CubismRenderer), "set_Material");
    private static readonly Action<CubismRenderer, Texture2D> AssignTexture = Bind<Action<CubismRenderer, Texture2D>>(typeof(CubismRenderer), "set_MainTexture");

    private static T Bind<T>(Type type, string method) where T : Delegate =>
        (type.GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, method)).CreateDelegate<T>();

    public static int TextureIndex(CubismDrawable d) => GetTextureIndex(d);
    public static Vector3[] Vertices(CubismDrawable d) => GetVertices(d).ToArray();
    public static bool Masked(CubismDrawable d) => IsMasked(d);
    public static string MaterialName(CubismDrawable d) => "Unlit"
        + (BlendAdditive(d) ? "Additive" : MultiplyBlend(d) ? "Multiply" : "")
        + (IsMasked(d) ? "Masked" + (IsInverted(d) ? "Inverted" : "") : "")
        + (IsDoubleSided(d) ? "" : "Culling");
    public static void SetMaterial(CubismRenderer r, Material m) => AssignMaterial(r, m);
    public static void SetTexture(CubismRenderer r, Texture2D t) => AssignTexture(r, t);
    public static void StartMask(CubismMaskController mask) => mask.Start();
#endif
}
