using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceWorkspace
{
    public void SaveText(ThemeTextRule rule, string? previousIdentity)
    {
        rule.Validate(); var next = Fresh();
        next.Texts.RemoveAll(r => r.Identity == rule.Identity || r.Identity == previousIdentity);
        next.Texts.Add(rule); next.Write(Root); Rules = next;
    }
    public void RemoveText(string identity)
    {
        var next = Fresh(); next.Texts.RemoveAll(r => r.Identity == identity); next.Write(Root); Rules = next;
    }
}
