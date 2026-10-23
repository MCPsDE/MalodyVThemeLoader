using MalodyVThemeLoader.Shared;
using UnityEngine;

namespace MalodyVThemeLoader.Plugin;

// One scan/frame owns this index. Each sibling set is read once, including
// duplicate counts. It is never retained across hierarchy changes or frames.
internal sealed class SceneAddressIndex
{
    private readonly Dictionary<int, string> segments = new();
    private readonly Dictionary<int, string> addresses = new();
    private readonly HashSet<int> parents = new();
    private readonly HashSet<int> scenes = new();
    internal int SiblingReads { get; private set; }
    internal string AddressOf(Transform target) => AddressOf(target, 0);
    private string AddressOf(Transform target, int depth)
    {
        if (target == null || depth >= 96) return "";
        var id = target.GetInstanceID();
        if (addresses.TryGetValue(id, out var cached)) return cached;
        var parent = target.parent;
        if (parent != null)
        {
            if (parents.Add(parent.GetInstanceID()))
            {
                var siblings = new List<Transform>();
                for (var i = 0; i < parent.childCount; i++) siblings.Add(parent.GetChild(i));
                Index(siblings);
            }
        }
        else if (scenes.Add(target.gameObject.scene.handle)) Index(target.gameObject.scene.GetRootGameObjects().Select(root => root.transform).OrderBy(root => root.GetSiblingIndex()));
        if (!segments.TryGetValue(id, out var segment)) return "";
        var prefix = parent != null ? AddressOf(parent, depth + 1) : "";
        if (parent != null && prefix.Length == 0) return "";
        var result = prefix.Length == 0 ? segment : prefix + "/" + segment; addresses[id] = result; return result;
    }
    private void Index(IEnumerable<Transform> siblings)
    {
        var items = new List<(int Id, string Name)>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var sibling in siblings)
        {
            if (sibling == null) continue;
            SiblingReads++; var name = sibling.name;
            if (SceneNodeLocator.IsOwnName(name)) continue;
            items.Add((sibling.GetInstanceID(), name)); counts.TryGetValue(name, out var count); counts[name] = count + 1;
        }
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            occurrences.TryGetValue(item.Name, out var index); occurrences[item.Name] = index + 1;
            segments[item.Id] = SceneNodeAddress.Segment(item.Name, index, counts[item.Name]);
        }
    }
}
