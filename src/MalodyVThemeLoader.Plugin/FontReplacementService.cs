using System.Runtime.InteropServices;
using BepInEx.Logging;
using MalodyVThemeLoader.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MalodyVThemeLoader.Plugin;

internal sealed class FontReplacementService
{
    private const uint PrivateFont = 0x10;

    private readonly ManualLogSource log;
    private readonly Dictionary<string, FontReplacement> replacements =
        new(StringComparer.OrdinalIgnoreCase);

    public FontReplacementService(ManualLogSource log, LoadedTheme theme)
    {
        this.log = log;

        foreach (var mapping in theme.Manifest.FontMappings)
        {
            TryLoadMapping(theme, mapping);
        }
    }

    public TMP_FontAsset? ResolveTmpFont(TMP_FontAsset? original)
    {
        if (original == null
            || !replacements.TryGetValue(NormalizeName(original.name), out var replacement))
        {
            return null;
        }

        return replacement.TmpFont;
    }

    public Font? ResolveLegacyFont(Font? original)
    {
        if (original == null
            || !replacements.TryGetValue(NormalizeName(original.name), out var replacement))
        {
            return null;
        }

        return replacement.LegacyFont;
    }

    private void TryLoadMapping(LoadedTheme theme, FontMapping mapping)
    {
        try
        {
            var fontPath = PathRules.GetSafeCombinedPath(theme.FontDirectory, mapping.File);
            if (!File.Exists(fontPath))
            {
                log.LogWarning($"Theme font does not exist: {fontPath}");
                return;
            }

            if (AddFontResourceEx(fontPath, PrivateFont, IntPtr.Zero) == 0)
            {
                log.LogWarning($"Windows could not register theme font: {fontPath}");
                return;
            }

            var legacyFont = Font.CreateDynamicFontFromOSFont(mapping.Family, mapping.Size);
            if (legacyFont == null)
            {
                log.LogWarning($"Unity could not create font family '{mapping.Family}'.");
                return;
            }

            var atlasSize = Math.Clamp(mapping.AtlasSize, 256, 4096);
            var tmpFont = TMP_FontAsset.CreateFontAsset(
                legacyFont,
                mapping.Size,
                9,
                GlyphRenderMode.SDFAA,
                atlasSize,
                atlasSize,
                AtlasPopulationMode.Dynamic,
                true);
            if (tmpFont == null)
            {
                log.LogWarning($"TMP could not create font asset for '{mapping.Family}'.");
                return;
            }

            tmpFont.name = $"ThemeLoader {mapping.Family}";
            var replacement = new FontReplacement(legacyFont, tmpFont);
            foreach (var sourceName in mapping.SourceNames)
            {
                replacements[NormalizeName(sourceName)] = replacement;
            }
        }
        catch (Exception exception)
        {
            log.LogWarning($"Failed to load theme font '{mapping.File}': {exception.Message}");
        }
    }

    private static string NormalizeName(string name)
    {
        const string instanceSuffix = " Instance";
        var normalized = name?.Trim() ?? "";
        return normalized.EndsWith(instanceSuffix, StringComparison.OrdinalIgnoreCase)
            ? normalized[..^instanceSuffix.Length]
            : normalized;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "AddFontResourceExW")]
    private static extern int AddFontResourceEx(
        string fileName,
        uint flags,
        IntPtr reserved);
}

internal sealed record FontReplacement(Font LegacyFont, TMP_FontAsset TmpFont);
