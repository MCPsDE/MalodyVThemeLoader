using UnityEngine.UI;

namespace MalodyVThemeLoader.Plugin;

internal static class TextSelectionAdapter
{
    // Presentation state only. Never reads or writes ranking configuration.
    internal static bool? Selected(Text? text)
    {
        if (text == null) return null;
        var rank = text.GetComponentInParent<Malody.Scene.Widget.WidgetRankToggle>();
        if (rank == null || rank.labels == null || rank.images == null) return null;
        for (var i = 0; i < Math.Min(rank.labels.Length, rank.images.Length); i++)
            if (rank.labels[i] == text) return rank.images[i] != null && rank.images[i].activeSelf;
        return null;
    }
}
