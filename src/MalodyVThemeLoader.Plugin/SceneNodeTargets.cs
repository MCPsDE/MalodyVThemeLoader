using MalodyVThemeLoader.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalodyVThemeLoader.Plugin;

// Resolve only the requested paths. Index each required sibling set once,
// without traversing unrelated branches or constructing inspector metadata.
internal sealed class SceneNodeTargets
{
    private sealed record Segment(string Name, int Index, int Count);
    private sealed record TargetPath(string Address, Segment[] Segments, int RepeatDepth = -1);
    private readonly Dictionary<string, List<TargetPath>> paths = new(StringComparer.Ordinal);
    internal int LastSiblingReads { get; private set; }

    internal SceneNodeTargets(IEnumerable<ThemeNodeRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!SceneComponentQuery.IsAllowedScene(rule.Scene) || !SceneNodeAddress.IsValid(rule.Address)) continue;
            if (!paths.TryGetValue(rule.Scene, out var items)) paths[rule.Scene] = items = new();
            if (items.Any(item => item.Address == rule.Address)) continue;
            items.Add(new TargetPath(rule.Address, rule.Address.Split('/').Select(part =>
            {
                var hash = part.LastIndexOf('#'); var colon = part.LastIndexOf(':');
                return new Segment(Uri.UnescapeDataString(part[..hash]), int.Parse(part[(hash + 1)..colon]), int.Parse(part[(colon + 1)..]));
            }).ToArray()));
        }
    }

    internal SceneNodeTargets(IEnumerable<ThemeTextRule> rules) : this(rules.Select(r => new ThemeNodeRule { Scene = r.Scene, Address = r.Address }))
    {
        // A template expands only the one ancestor the author explicitly selected.
        foreach (var group in rules.GroupBy(r => r.Scene))
        {
            if (!paths.TryGetValue(group.Key, out var items)) continue;
            var expanded = new List<TargetPath>();
            foreach (var rule in group)
            {
                var path = items.FirstOrDefault(p => p.Address == rule.Address);
                if (path != null && !expanded.Any(p => p.Address == rule.Address && p.RepeatDepth == rule.RepeatDepth))
                    expanded.Add(path with { RepeatDepth = rule.RepeatDepth });
            }
            paths[group.Key] = expanded;
        }
    }

    internal List<SceneNode> Resolve()
    {
        LastSiblingReads = 0;
        var result = new List<SceneNode>();
        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded || !paths.TryGetValue(scene.name, out var requested)) continue;
            var rootIndex = Index(scene.GetRootGameObjects().Where(root => root != null).Select(root => root.transform).OrderBy(root => root.GetSiblingIndex()));
            var childIndexes = new Dictionary<int, Dictionary<string, List<Transform>>>();
            foreach (var path in requested)
            {
                if (path.RepeatDepth >= 0)
                {
                    Expand(rootIndex, 0, "");
                    continue;
                    void Expand(Dictionary<string, List<Transform>> siblings, int depth, string parentAddress)
                    {
                        if (result.Count >= 2048 || LastSiblingReads > 32768) return;
                        var segment = path.Segments[depth];
                        if (!siblings.TryGetValue(segment.Name, out var candidates)
                            || depth != path.RepeatDepth && candidates.Count != segment.Count) return;
                        for (var occurrence = 0; occurrence < candidates.Count; occurrence++)
                        {
                            if (depth != path.RepeatDepth && occurrence != segment.Index) continue;
                            var target = candidates[occurrence];
                            var address = parentAddress + (parentAddress.Length == 0 ? "" : "/") + SceneNodeAddress.Segment(segment.Name, occurrence, candidates.Count);
                            if (depth + 1 == path.Segments.Length)
                            {
                                result.Add(new SceneNode(target, new InspectorNode { Id = target.GetInstanceID(), Scene = scene.name, Address = address }));
                                if (result.Count >= 2048) return;
                            }
                            else
                            {
                                var id = target.GetInstanceID();
                                if (!childIndexes.TryGetValue(id, out var childrenIndex))
                                {
                                    var children = new List<Transform>();
                                    for (var child = 0; child < target.childCount && child < 32768; child++) children.Add(target.GetChild(child));
                                    childIndexes[id] = childrenIndex = Index(children);
                                }
                                Expand(childrenIndex, depth + 1, address);
                            }
                        }
                    }
                }
                Transform? target = null;
                var siblings = rootIndex;
                for (var depth = 0; depth < path.Segments.Length; depth++)
                {
                    var segment = path.Segments[depth];
                    if (!siblings.TryGetValue(segment.Name, out var candidates) || candidates.Count != segment.Count)
                    { target = null; break; }
                    target = candidates[segment.Index];
                    if (depth + 1 == path.Segments.Length) break;
                    var id = target.GetInstanceID();
                    if (!childIndexes.TryGetValue(id, out siblings))
                    {
                        var children = new List<Transform>();
                        for (var child = 0; child < target.childCount; child++) children.Add(target.GetChild(child));
                        childIndexes[id] = siblings = Index(children);
                    }
                }
                if (target == null) continue;
                result.Add(new SceneNode(target, new InspectorNode {
                    Id = target.GetInstanceID(), Scene = scene.name, Address = path.Address,
                    CanDecorate = target.GetComponent<RectTransform>() != null && target.GetComponentInParent<Canvas>() != null
                }));
            }
        }
        return result;
    }

    private Dictionary<string, List<Transform>> Index(IEnumerable<Transform> siblings)
    {
        var result = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
        foreach (var sibling in siblings)
        {
            if (sibling == null) continue;
            LastSiblingReads++; var name = sibling.name;
            if (SceneNodeLocator.IsOwnName(name)) continue;
            if (!result.TryGetValue(name, out var items)) result[name] = items = new();
            items.Add(sibling);
        }
        return result;
    }
}
