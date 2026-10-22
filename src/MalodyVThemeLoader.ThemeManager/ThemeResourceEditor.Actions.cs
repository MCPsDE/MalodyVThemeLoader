using System.Diagnostics;
using System.Media;
using System.Text.Json;
using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

internal sealed partial class ThemeResourceEditor
{
    public event Action? Applied;
    public string? ThemeKey => theme?.Key;
    private string SceneName => sceneBox.SelectedItem is SceneChoice choice ? choice.Name : "";
    private string Hierarchy => image == null || SceneName.Length == 0 ? "" : hierarchyBox.Text.Trim();
    private string ComponentName => image == null || componentBox.SelectedIndex <= 0 ? "" : componentBox.SelectedItem?.ToString() ?? "";
    private Float4 CurrentBorder => new((float)borders["左"].Value, (float)borders["下"].Value, (float)borders["右"].Value, (float)borders["上"].Value);
    private bool ScopePending => image != null && (SceneName != loadedScene || Hierarchy != loadedHierarchy || ComponentName != loadedComponent);

    public bool SetTheme(ThemeItem? item)
    {
        if (theme?.Key == item?.Key && workspace != null)
        {
            if (!dirty && !ScopePending && !IsBusy)
            {
                try { workspace.Reload(); if (image != null || audio != null) LoadSelection(); inspectorPane?.SetWorkspace(workspace, theme); }
                catch (Exception e) { Notify("无法刷新资源：" + e.Message); }
            }
            return true;
        }
        if (!ConfirmLeave()) return false;
        StopAudio(); dirty = false; image = null; audio = null; pendingFile = null; theme = null; workspace = null; liveNodeAddress = "";
        selecting = true;
        try
        {
            sceneBox.Items.Clear(); sceneBox.Items.Add(new SceneChoice("", "所有场景")); sceneBox.SelectedIndex = 0;
            if (item == null || item.IsVirtual) { tree.Nodes.Clear(); preview.SetImage(null); Notify("先选择一个目录主题，或导入 ZIP。 "); return true; }
            workspace = new ThemeResourceWorkspace(gameRoot, item);
            theme = new ThemeItem(item.Key, workspace.Root, true, ThemePackage.InspectDirectory(workspace.Root));
            foreach (var name in workspace.Catalog.Scenes.Where(ThemeResources.IsAllowedScene)) sceneBox.Items.Add(new SceneChoice(name, SceneTitle(name)));
            loadedScene = ""; BuildTree();
            Notify("选择资源；保存后会自动启用实时加载并应用此主题。");
        }
        catch (Exception e) { Notify("无法打开资源：" + e.Message); tree.Nodes.Clear(); preview.SetImage(null); }
        finally { selecting = false; UpdateEnabled(); inspectorPane?.SetWorkspace(workspace, theme); }
        return true;
    }

    private void BuildTree()
    {
        if (workspace == null) return;
        var previous = selecting; selecting = true;
        var query = search.Text.Trim(); var selectedKey = image?.LogicalId ?? audio?.Event;
        tree.BeginUpdate(); tree.Nodes.Clear();
        try
        {
            var images = tree.Nodes.Add("图片"); var sounds = tree.Nodes.Add("界面音频");
            foreach (var entry in workspace.Catalog.Images.Where(e => e.LogicalId.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                var parts = entry.LogicalId.Split('/'); var node = images;
                foreach (var part in parts[..^1]) node = node.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == part && n.Tag == null) ?? node.Nodes.Add(part);
                var leaf = node.Nodes.Add(parts[^1]); leaf.Tag = entry; leaf.ToolTipText = entry.LogicalId;
                if (entry.LogicalId == selectedKey) tree.SelectedNode = leaf;
            }
            foreach (var entry in workspace.Catalog.Audio.Where(e => (e.Title + e.Event + e.LogicalId).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                var leaf = sounds.Nodes.Add(entry.Title); leaf.Tag = entry; leaf.ToolTipText = entry.LogicalId;
                if (entry.Event == selectedKey) tree.SelectedNode = leaf;
            }
            images.Expand(); sounds.Expand(); if (query.Length > 0) images.ExpandAll();
            tree.SelectedNode?.EnsureVisible();
        }
        finally { tree.EndUpdate(); selecting = previous; }
    }

    private void SelectResource(object resource)
    {
        liveNodeAddress = "";
        StopAudio(); image = resource as ImageCatalogEntry; audio = resource as AudioCatalogEntry; dirty = false; pendingFile = null;
        selecting = true;
        hierarchyBox.Text = ""; componentBox.SelectedIndex = 0;
        if (audio?.Event == "music_finish") SetScene("SceneResult");
        selecting = false;
        usages.Items.Clear();
        usagesTitle.Text = image != null ? "使用位置 · 双击选择" : "音频用途";
        if (image != null)
        {
            foreach (var usage in image.Usages) usages.Items.Add(new UsageChoice(usage));
            if (image.Usages.Count == 0) usages.Items.Add("可选择场景，或填写控件路径");
        }
        else if (audio != null) usages.Items.Add(audio.Event == "music_finish" ? "结算界面" : "各界面的交互音效");
        LoadSelection();
    }

    private void ScopeChanged()
    {
        if (selecting) return;
        if (!ConfirmLeave()) { selecting = true; SetScene(loadedScene); selecting = false; return; }
        selecting = true; hierarchyBox.Text = ""; componentBox.SelectedIndex = 0; liveNodeAddress = ""; selecting = false;
        LoadSelection();
    }

    private void SelectUsage()
    {
        if (usages.SelectedItem is not UsageChoice choice || !ConfirmLeave()) return;
        selecting = true; liveNodeAddress = ""; SetScene(choice.Usage.Scene); hierarchyBox.Text = choice.Usage.Hierarchy;
        componentBox.SelectedItem = choice.Usage.Component; selecting = false; LoadSelection();
    }

    private void SetScene(string name)
    {
        var match = sceneBox.Items.Cast<SceneChoice>().FirstOrDefault(c => c.Name == name);
        if (match == null && name.Length > 0) { match = new SceneChoice(name, SceneTitle(name)); sceneBox.Items.Add(match); }
        sceneBox.SelectedItem = match ?? sceneBox.Items[0];
    }

    private void ScopeInputChanged()
    {
        if (selecting) return;
        if (Hierarchy.Length == 0 && componentBox.SelectedIndex > 0)
        { selecting = true; componentBox.SelectedIndex = 0; selecting = false; }
        UpdateEnabled();
        if (ScopePending) Notify("点击“读取此范围”，加载该控件的图片与切片。");
        else Notify(dirty ? "尚未保存 · 请保存当前资源修改。" : "当前范围未改变。");
    }

    private void CommitScope()
    {
        if (workspace == null || image == null || IsBusy) return;
        try
        {
            var probe = new ThemeResources();
            probe.Images.Add(new ThemeImageRule { LogicalId = image.LogicalId, Scene = SceneName, Hierarchy = Hierarchy, Component = ComponentName });
            probe.Validate();
            if (!ConfirmLeave()) return;
            if (ScopePending) liveNodeAddress = "";
            workspace.Reload(); LoadSelection(); Notify("已读取此范围，可替换图片或调整切片。");
        }
        catch (Exception e) { Notify("无法读取范围：" + e.Message); }
    }

    private void CancelScope()
    {
        selecting = true; hierarchyBox.Text = loadedHierarchy;
        componentBox.SelectedItem = loadedComponent.Length == 0 ? componentBox.Items[0] : loadedComponent;
        selecting = false; UpdateEnabled(); Notify(dirty ? "尚未保存 · 已保留原有修改。" : "已取消定位。");
    }

    private void LoadSelection()
    {
        if (workspace == null) return;
        StopAudio(); pendingFile = null; decodedAudio = null; dirty = false; loadedScene = SceneName;
        loadedHierarchy = Hierarchy; loadedComponent = ComponentName;
        selecting = true;
        try
        {
            if (image != null)
            {
                resourceName.Text = Path.GetFileName(image.LogicalId);
                tips.SetToolTip(resourceName, image.LogicalId);
                var rule = workspace.Rules.ResolveImage(image.LogicalId, SceneName, Hierarchy, ComponentName, liveNodeAddress);
                currentRuleFile = rule?.File ?? ""; currentFileName = rule?.DisplayName ?? ""; currentMode = rule?.Mode ?? "";
                SetBorders(rule?.Border ?? image.Border);
                sliceToggle.Checked = currentMode == "sliced" || currentMode.Length == 0 && image.Usages.Any(u => u.Scene == SceneName && u.Sliced && (Hierarchy.Length == 0 || ThemeResources.MatchHierarchy(u.Hierarchy, Hierarchy)));
                var path = workspace.ImageFile(image, SceneName, Hierarchy, ComponentName, liveNodeAddress);
                preview.EmptyText = "暂无图片，导入文件开始编辑";
                preview.SetImage(path == null ? null : ThemeResourceWorkspace.ReadBitmap(path));
                fileState.Text = (workspace.ExactImage(image.LogicalId, SceneName, Hierarchy, ComponentName, liveNodeAddress) != null ? "此范围已覆盖" : "继承主题图片")
                    + Environment.NewLine + (path == null ? "尚无图片" : currentFileName.Length > 0 ? currentFileName : Path.GetFileName(path));
                tips.SetToolTip(fileState, path ?? "");
            }
            else if (audio != null)
            {
                resourceName.Text = audio.Title; tips.SetToolTip(resourceName, audio.LogicalId);
                var rule = workspace.Rules.ResolveAudio(audio.Event, SceneName.Length > 0 ? SceneName : "SceneSong"); currentRuleFile = rule?.File ?? ""; currentFileName = rule?.DisplayName ?? "";
                var path = workspace.AudioFile(audio, SceneName);
                decodedAudio = path == null ? null : Live2dAudio.Decode(path);
                preview.EmptyText = "暂无音频，导入 OGG、WAV 或 MP3"; preview.SetAudio(decodedAudio);
                fileState.Text = (workspace.ExactAudio(audio.Event, SceneName) != null ? "此范围已覆盖" : "继承原音频")
                    + Environment.NewLine + (path == null ? "尚无音频" : currentFileName.Length > 0 ? currentFileName : Path.GetFileName(path)); tips.SetToolTip(fileState, path ?? "");
            }
        }
        catch (Exception e) { preview.EmptyText = "预览不可用，可导入文件替换"; preview.SetImage(null); Notify("预览失败：" + e.Message); }
        finally { selecting = false; UpdateSlicePreview(); UpdateEnabled(); }
    }

    private void SetBorders(Float4 value)
    {
        var previous = selecting; selecting = true;
        borders["左"].Value = (decimal)Math.Clamp(value.X, 0, 16384); borders["下"].Value = (decimal)Math.Clamp(value.Y, 0, 16384);
        borders["右"].Value = (decimal)Math.Clamp(value.Z, 0, 16384); borders["上"].Value = (decimal)Math.Clamp(value.W, 0, 16384);
        selecting = previous; UpdateSlicePreview();
    }

    private void UpdateSlicePreview()
    {
        preview.Sliced = sliceToggle.Checked && ComponentName != "RawImage";
        preview.TargetSize = new Size((int)targetWidth.Value, (int)targetHeight.Value); preview.SetBorder(CurrentBorder);
        foreach (var number in borders.Values) number.Enabled = sliceToggle.Checked;
    }

    private void MarkDirty() { if (selecting || image == null && audio == null) return; dirty = true; Notify("尚未保存 · 预览中的修改只在保存后应用。"); UpdateEnabled(); }

    private void UpdateEnabled()
    {
        var selected = workspace != null && (image != null || audio != null);
        body.Enabled = !IsBusy;
        sceneBox.Enabled = selected; locationFields.Visible = image != null;
        componentFields.Visible = image != null && Hierarchy.Length > 0;
        scopeActions.Visible = image != null && (Hierarchy.Length > 0 || ScopePending);
        readScope.Enabled = ScopePending; cancelScope.Enabled = ScopePending;
        hierarchyBox.Enabled = SceneName.Length > 0;
        imageFields.Visible = image != null && ComponentName != "RawImage"; playback.Visible = audio != null;
        import.Text = image != null ? "替换图片…" : "替换音频…";
        sliceToggle.Enabled = ComponentName != "RawImage";
        import.Enabled = selected && !ScopePending;
        imageFields.Enabled = !ScopePending; preview.Enabled = !ScopePending;
        play.Enabled = audio != null && decodedAudio != null;
        stop.Enabled = player != null;
        save.Enabled = selected && dirty && !IsBusy && !ScopePending;
        restore.Enabled = !ScopePending && (image != null ? workspace?.ExactImage(image.LogicalId, SceneName, Hierarchy, ComponentName, liveNodeAddress) != null
            : audio != null && workspace?.ExactAudio(audio.Event, SceneName) != null);
    }

    private async Task PickFileAsync()
    {
        using var dialog = new OpenFileDialog { CheckFileExists = true, Multiselect = false,
            Title = image != null ? "替换图片" : "替换音频",
            Filter = image != null ? "图片 (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp" : "音频 (*.ogg;*.wav;*.mp3)|*.ogg;*.wav;*.mp3" };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await ImportFileAsync(dialog.FileName);
    }

    internal async Task ImportFileAsync(string file)
    {
        if (IsBusy || workspace == null || ScopePending || image == null && audio == null) return;
        StopAudio(); IsBusy = true; UpdateEnabled(); Notify("正在读取文件…");
        try
        {
            if (image != null)
            {
                var bitmap = await Task.Run(() => ThemeResourceWorkspace.ReadBitmap(file));
                preview.SetImage(bitmap);
            }
            else
            {
                decodedAudio = await Task.Run(() => Live2dAudio.Decode(file)); preview.SetAudio(decodedAudio);
            }
            pendingFile = file; fileState.Text = "待保存" + Environment.NewLine + Path.GetFileName(file); tips.SetToolTip(fileState, file); MarkDirty();
        }
        catch (Exception e) { Notify("无法导入：" + e.Message); }
        finally { IsBusy = false; UpdateEnabled(); }
    }

    public async Task SaveAsync()
    {
        if (inspectorPane?.Visible == true) { await inspectorPane.SaveAsync(); return; }
        if (IsBusy || !dirty || workspace == null || theme == null) return;
        if (ScopePending) { Notify("请先读取控件范围，再保存资源。 "); return; }
        IsBusy = true; UpdateEnabled(); Notify("正在保存资源…");
        try
        {
            if (image != null)
            {
                var rule = new ThemeImageRule { LogicalId = image.LogicalId, Scene = SceneName, Hierarchy = Hierarchy, Component = ComponentName, NodeAddress = liveNodeAddress,
                    File = currentRuleFile, DisplayName = currentFileName, Mode = ComponentName == "RawImage" ? "" : currentMode,
                    Border = sliceToggle.Checked && ComponentName != "RawImage" ? CurrentBorder : null };
                if (sliceToggle.Checked && ComponentName != "RawImage") rule.Mode = "sliced";
                var source = pendingFile;
                await Task.Run(() => workspace.SaveImage(rule, source));
            }
            else if (audio != null)
            {
                var rule = new ThemeAudioRule { Event = audio.Event, Scene = SceneName, File = currentRuleFile, DisplayName = currentFileName }; var source = pendingFile;
                await Task.Run(() => workspace.SaveAudio(rule, source));
            }
            library.SetRealtimeMode(true); library.Activate(theme);
            dirty = false; pendingFile = null; LoadSelection(); Applied?.Invoke(); Notify(SavedMessage());
        }
        catch (Exception e) { Notify("保存失败：" + e.Message); }
        finally { IsBusy = false; UpdateEnabled(); }
    }

    private void RestoreScope()
    {
        if (IsBusy || workspace == null || theme == null) return;
        try
        {
            if (image != null) workspace.RemoveImage(image.LogicalId, SceneName, Hierarchy, ComponentName, liveNodeAddress);
            else if (audio != null) workspace.RemoveAudio(audio.Event, SceneName);
            library.SetRealtimeMode(true); library.Activate(theme); dirty = false;
            LoadSelection(); Applied?.Invoke(); Notify("已移除此范围的覆盖，恢复继承图片或音频。");
        }
        catch (Exception e) { Notify("恢复失败：" + e.Message); }
    }

    private string SavedMessage()
    {
        try
        {
            var path = Path.Combine(gameRoot, "BepInEx", "plugins", "MalodyVThemeLoader", "MalodyVThemeLoader.Plugin.dll");
            var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
            if (!Version.TryParse(version, out var installed) || installed < new Version(2, 5, 0)) return "已保存；请安装 2.5.0 或更高版本后启动游戏。";
            var statusPath = Path.Combine(gameRoot, "ThemeLoader", "live2d-status.json");
            if (File.Exists(statusPath))
            {
                using var status = JsonDocument.Parse(File.ReadAllText(statusPath));
                if (status.RootElement.TryGetProperty("pluginVersion", out var running) && Version.TryParse(running.GetString(), out var old) && old < new Version(2, 5, 0))
                    return "已保存；重启游戏后应用新资源功能。";
            }
        }
        catch { }
        return "已保存并应用主题 · 实时加载已开启。";
    }

    private void Audition()
    {
        StopAudio();
        if (decodedAudio == null) return;
        try { soundStream = new MemoryStream(decodedAudio.ToWave()); player = new SoundPlayer(soundStream); player.Load(); player.Play(); Notify("正在试听 · " + audio?.Title); }
        catch (Exception e) { StopAudio(); Notify("无法试听：" + e.Message); }
        UpdateEnabled();
    }

    private void StopAudio() { player?.Stop(); player?.Dispose(); player = null; soundStream?.Dispose(); soundStream = null; }
    private void Notify(string text) { state.Text = text; report(text); }
    private static string SceneTitle(string name) => name switch {
        "SceneSong" => "大厅 / 选曲", "SceneResult" => "游玩结算", "SceneLogo" => "启动界面", "SceneChart" => "谱面详情", "SceneStore" => "商店",
        "SceneInventory" => "物品栏", "SceneSkin" => "皮肤", "SceneEditor" => "谱面编辑器", "SceneEditorSkin" => "皮肤编辑器", "SceneRoom" => "房间界面", _ => name };
    private sealed record SceneChoice(string Name, string Title) { public override string ToString() => Title + (Name.Length == 0 ? "" : " · " + Name); }
    private sealed record UsageChoice(ImageUsage Usage) { public override string ToString() => SceneTitle(Usage.Scene) + " · " + Usage.Hierarchy; }
}
