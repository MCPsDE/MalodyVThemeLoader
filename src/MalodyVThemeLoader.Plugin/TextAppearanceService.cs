using System.Text.RegularExpressions;
using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal sealed class TextAppearanceService : IDisposable
{
    private readonly ManualLogSource log;
    private readonly List<ThemeTextRule> rules;
    private readonly SceneNodeTargets targets;
    private readonly Dictionary<(int, string), Binding> bindings = new();
    private readonly List<(int, string)> retired = new();
    private float nextScan;
    internal TextAppearanceService(ManualLogSource log, ThemeResources resources)
    { this.log = log; rules = resources.Texts; targets = new SceneNodeTargets(rules); }

    internal void Tick()
    {
        if (rules.Count == 0) return;
        if (Time.realtimeSinceStartup >= nextScan)
        {
            nextScan = Time.realtimeSinceStartup + .75f;
            try
            {
                var wanted = new HashSet<(int, string)>();
                foreach (var node in targets.Resolve())
                    foreach (var type in new[] { "Text", "TMP_Text" })
                    {
                        var matches = rules.Where(r => r.Matches(node.Info.Scene, node.Info.Address, type)).ToList();
                        if (matches.Count == 0) continue;
                        var exact = matches.Where(r => r.RepeatDepth < 0).ToList();
                        if (exact.Count > 0) matches = exact;
                        if (matches.Count != 1) continue; // Ambiguous templates fail closed.
                        var key = (node.Info.Id, type); wanted.Add(key);
                        if (bindings.ContainsKey(key)) continue;
                        var graphic = type == "Text" ? node.Transform.GetComponent<Text>()?.TryCast<Graphic>()
                            : node.Transform.GetComponent<TMP_Text>()?.TryCast<Graphic>();
                        if (graphic != null && bindings.Count < 2048) bindings[key] = new Binding(graphic, matches[0]);
                    }
                foreach (var key in bindings.Keys.Where(k => !wanted.Contains(k)).ToArray())
                { bindings[key].Dispose(); bindings.Remove(key); }
            }
            catch (Exception e) { log.LogWarning("Text rule discovery: " + e.Message); }
        }
        retired.Clear();
        foreach (var pair in bindings)
        {
            try { if (pair.Value.Tick()) continue; }
            catch (Exception e) { log.LogWarning("Text rule retired: " + e.Message); }
            retired.Add(pair.Key);
        }
        foreach (var key in retired) { bindings[key].Dispose(); bindings.Remove(key); }
    }
    public void Dispose() { foreach (var binding in bindings.Values) binding.Dispose(); bindings.Clear(); }

    internal static Color Parse(string hex, float alpha = 1)
    {
        var rgb = TextColor.Rgb(hex); return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
    internal static string Hex(Color color) => $"#{Mathf.Clamp(Mathf.RoundToInt(color.r * 255), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(color.g * 255), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(color.b * 255), 0, 255):X2}";
    internal static (string Component, string Content) Summary(Transform target)
    {
        var legacy = target.GetComponent<Text>(); var tmp = legacy == null ? target.GetComponent<TMP_Text>() : null;
        var value = legacy != null ? legacy.text : tmp != null ? tmp.text : "";
        value ??= ""; if (value.Length > 160) value = value[..160] + "…";
        return (legacy != null ? "Text" : tmp != null ? "TMP_Text" : "", value.Replace('\n', ' ').Replace('\r', ' '));
    }
    internal static List<InspectorText> Describe(Transform target)
    {
        var result = new List<InspectorText>();
        var legacy = target.GetComponent<Text>(); var tmp = target.GetComponent<TMP_Text>();
        if (legacy != null) result.Add(new InspectorText { Component = "Text", Content = Clip(legacy.text), Color = Hex(legacy.color), Opacity = legacy.color.a, RichText = legacy.supportRichText });
        if (tmp != null)
        {
            var g = tmp.colorGradient;
            var preset = tmp.colorGradientPreset;
            if (preset != null) g = new VertexGradient { topLeft = preset.topLeft, topRight = preset.topRight, bottomLeft = preset.bottomLeft, bottomRight = preset.bottomRight };
            var info = new InspectorText { Component = "TMP_Text", Content = Clip(tmp.text), Color = Hex(tmp.color), Opacity = tmp.color.a, RichText = tmp.richText,
                Gradient = tmp.enableVertexGradient, GradientColors = new[] { Hex(g.topLeft), Hex(g.topRight), Hex(g.bottomLeft), Hex(g.bottomRight) } };
            var material = tmp.fontSharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_FaceColor")) info.FaceColor = Hex(material.GetColor("_FaceColor"));
                if (material.HasProperty("_OutlineColor")) info.OutlineColor = Hex(material.GetColor("_OutlineColor"));
            }
            result.Add(info);
        }
        return result;
        static string Clip(string? text) => string.IsNullOrEmpty(text) ? "" : text.Length <= 256 ? text : text[..256] + "…";
    }

    private sealed class Binding : IDisposable
    {
        private static readonly Regex colorTags = new(@"</?color(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^>]*))?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        private readonly Graphic graphic;
        private readonly Text? legacy;
        private readonly TMP_Text? tmp;
        private readonly ThemeTextRule rule;
        private readonly Selectable? selectable;
        private readonly RectTransform hoverRect;
        private Color source, applied;
        private readonly Color normalColor;
        private readonly Color? selectedColor, hoverColor;
        private readonly Dictionary<uint, Color> mappings;
        private readonly VertexGradient desiredGradient;
        private VertexGradient lastGradient;
        private bool lastGradientEnabled;
        private bool wroteColor, sourceOverride, sourceGradient, wroteGradient;
        private VertexGradient gradient;
        private string sourceText = "", appliedText = "";
        private bool wroteText;
        private Material? originalMaterial, ownedMaterial;
        private TMP_ColorGradient? sourcePreset;
        private bool wrotePreset;
        private bool disposed;

        internal Binding(Graphic graphic, ThemeTextRule rule)
        {
            this.graphic = graphic; this.rule = rule; source = graphic.color;
            normalColor = Parse(rule.Color); selectedColor = rule.SelectedColor.Length > 0 ? Parse(rule.SelectedColor) : null;
            hoverColor = rule.HoverColor.Length > 0 ? Parse(rule.HoverColor) : null;
            mappings = rule.Mappings.ToDictionary(m => TextColor.Rgb(m.Source), m => Parse(m.Target));
            desiredGradient = new VertexGradient { topLeft = Parse(rule.GradientColors[0]), topRight = Parse(rule.GradientColors[1]), bottomLeft = Parse(rule.GradientColors[2]), bottomRight = Parse(rule.GradientColors[3]) };
            legacy = graphic.TryCast<Text>(); tmp = graphic.TryCast<TMP_Text>();
            selectable = graphic.GetComponentInParent<Selectable>();
            hoverRect = selectable != null ? selectable.GetComponent<RectTransform>() ?? graphic.rectTransform : graphic.rectTransform;
            if (tmp != null)
            {
                sourceOverride = tmp.overrideColorTags; sourceGradient = tmp.enableVertexGradient; gradient = tmp.colorGradient;
                if (rule.FaceColor.Length > 0 || rule.OutlineColor.Length > 0)
                {
                    originalMaterial = tmp.fontSharedMaterial;
                    if (originalMaterial != null)
                    {
                        ownedMaterial = new Material(originalMaterial) { name = SceneNodeLocator.OwnPrefix + "TextMaterial" };
                        SetMaterialColors(); tmp.fontSharedMaterial = ownedMaterial;
                    }
                }
            }
        }
        internal bool Tick()
        {
            if (disposed || graphic == null || !SceneComponentQuery.IsAllowedScene(graphic.gameObject.scene.name)) return false;
            if (!graphic.gameObject.activeInHierarchy) return true;
            var observed = graphic.color;
            // Observe fresh game writes, including animation alpha, before changing RGB.
            if (!wroteColor || observed.r != applied.r || observed.g != applied.g || observed.b != applied.b)
            { source.r = observed.r; source.g = observed.g; source.b = observed.b; }
            if (!wroteColor || observed.a != applied.a) source.a = observed.a;
            var sourceRgb = (uint)(Mathf.Clamp(Mathf.RoundToInt(source.r * 255), 0, 255) << 16 | Mathf.Clamp(Mathf.RoundToInt(source.g * 255), 0, 255) << 8 | Mathf.Clamp(Mathf.RoundToInt(source.b * 255), 0, 255));
            applied = mappings.TryGetValue(sourceRgb, out var mapped) ? mapped : normalColor;
            if (selectedColor.HasValue && Selected()) applied = selectedColor.Value;
            if (hoverColor.HasValue && Hovered()) applied = hoverColor.Value;
            applied.a = rule.Opacity ?? observed.a;
            if (observed != applied) graphic.color = applied;
            wroteColor = true;
            if (tmp != null)
            {
                if (rule.RichText == "uniform") tmp.overrideColorTags = true;
                if (rule.Gradient != "preserve")
                {
                    var current = tmp.colorGradient;
                    if (!wroteGradient || tmp.enableVertexGradient != lastGradientEnabled) sourceGradient = tmp.enableVertexGradient;
                    if (!wroteGradient || !SameGradient(current, lastGradient)) gradient = current;
                    wroteGradient = true; lastGradientEnabled = rule.Gradient == "custom";
                    if (tmp.enableVertexGradient != lastGradientEnabled) tmp.enableVertexGradient = lastGradientEnabled;
                    if (rule.Gradient == "custom")
                    {
                        if (tmp.colorGradientPreset != null)
                        {
                            sourcePreset = tmp.colorGradientPreset; tmp.colorGradientPreset = null; wrotePreset = true;
                        }
                        if (!SameGradient(current, desiredGradient)) tmp.colorGradient = desiredGradient;
                        lastGradient = desiredGradient;
                    }
                    else lastGradient = current;
                }
                if (ownedMaterial != null && tmp.fontSharedMaterial != ownedMaterial)
                {
                    // Panel/font changes may install a new source material. Clone it, never mutate it.
                    var previous = ownedMaterial; originalMaterial = tmp.fontSharedMaterial;
                    if (originalMaterial != null)
                    {
                        ownedMaterial = new Material(originalMaterial) { name = SceneNodeLocator.OwnPrefix + "TextMaterial" };
                        SetMaterialColors(); tmp.fontSharedMaterial = ownedMaterial;
                    }
                    else ownedMaterial = null;
                    UnityEngine.Object.Destroy(previous);
                }
            }
            if (legacy != null && rule.RichText == "uniform" && legacy.supportRichText)
            {
                var current = legacy.text ?? "";
                if (!wroteText || current != appliedText)
                {
                    sourceText = current; appliedText = colorTags.Replace(current, ""); wroteText = true;
                    if (current != appliedText) legacy.text = appliedText;
                }
            }
            return true;
        }
        private void SetMaterialColors()
        {
            if (ownedMaterial == null) return;
            if (rule.FaceColor.Length > 0 && ownedMaterial.HasProperty("_FaceColor")) ownedMaterial.SetColor("_FaceColor", Parse(rule.FaceColor, ownedMaterial.GetColor("_FaceColor").a));
            if (rule.OutlineColor.Length > 0 && ownedMaterial.HasProperty("_OutlineColor")) ownedMaterial.SetColor("_OutlineColor", Parse(rule.OutlineColor, ownedMaterial.GetColor("_OutlineColor").a));
        }
        private static bool SameGradient(VertexGradient a, VertexGradient b) => a.topLeft == b.topLeft && a.topRight == b.topRight && a.bottomLeft == b.bottomLeft && a.bottomRight == b.bottomRight;
        private bool Selected()
        {
            var rankSelected = TextSelectionAdapter.Selected(legacy);
            if (rankSelected.HasValue) return rankSelected.Value;
            var toggle = selectable?.TryCast<Toggle>();
            if (toggle != null) return toggle.isOn;
            var current = EventSystem.current?.currentSelectedGameObject;
            return selectable != null && current != null && (current == selectable.gameObject || current.transform.IsChildOf(selectable.transform));
        }
        private bool Hovered()
        {
            return Application.isFocused && SceneInspectorController.TryMouse(out var mouse)
                && RectTransformUtility.RectangleContainsScreenPoint(hoverRect, mouse, SceneNodeLocator.CameraFor(hoverRect));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try
            {
                if (graphic != null)
                {
                    var current = graphic.color;
                    if (wroteColor)
                    {
                        // Restore only values still owned by us; retain later game updates.
                        if (current.r == applied.r && current.g == applied.g && current.b == applied.b)
                        { current.r = source.r; current.g = source.g; current.b = source.b; }
                        if (rule.Opacity.HasValue && current.a == applied.a) current.a = source.a;
                        graphic.color = current;
                    }
                    if (legacy != null && wroteText && legacy.text == appliedText) legacy.text = sourceText;
                    if (tmp != null)
                    {
                        if (rule.RichText == "uniform") tmp.overrideColorTags = sourceOverride;
                        if (wroteGradient) { tmp.enableVertexGradient = sourceGradient; tmp.colorGradient = gradient; }
                        if (wrotePreset && tmp.colorGradientPreset == null) tmp.colorGradientPreset = sourcePreset;
                        if (ownedMaterial != null && tmp.fontSharedMaterial == ownedMaterial) tmp.fontSharedMaterial = originalMaterial;
                    }
                }
            }
            catch { /* Scene destruction can retire native components before managed cleanup. */ }
            finally { if (ownedMaterial != null) UnityEngine.Object.Destroy(ownedMaterial); ownedMaterial = null; }
        }
    }
}
