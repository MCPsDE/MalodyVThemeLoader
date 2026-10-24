namespace MalodyVThemeLoader.Plugin;

// One state per displayed replacement. A pending result can arrive before the
// model, but polling the same completed result must not restart its animation.
internal sealed class Live2dResultAnimation
{
    private string played = "";

    internal bool Apply(string result, IReadOnlyDictionary<string, string> motions, Func<string, bool> play)
    {
        if (result == played || !motions.TryGetValue(result, out var file) || !play(file)) return false;
        played = result;
        return true;
    }
}
