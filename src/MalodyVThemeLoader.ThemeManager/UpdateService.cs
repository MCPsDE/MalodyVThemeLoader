using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace MalodyVThemeLoader.ThemeManager;

internal static class UpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/MCPsDE/MalodyVThemeLoader/releases/latest");
    public static async Task<UpdateResult> CheckAndStartAsync(string gameRoot, IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default, Func<Task<bool>>? beforeInstall = null)
    {
        if (Environment.GetEnvironmentVariable("MALODY_THEMELOADER_DISABLE_UPDATES") == "1") return UpdateResult.None;
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MalodyVThemeLoader", CurrentVersion().ToString(3)));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return await RunAsync(client, LatestReleaseUri, CurrentVersion(), gameRoot, progress, cancellationToken, beforeInstall,
            start => Process.Start(start) != null);
    }
    internal static async Task<UpdateResult> RunAsync(HttpClient client, Uri releaseUri, Version currentVersion, string gameRoot,
        IProgress<UpdateProgress>? progress, CancellationToken cancellationToken, Func<Task<bool>>? beforeInstall,
        Func<ProcessStartInfo, bool> launch)
    {
        string? temporaryRoot = null; var started = false;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromMinutes(30));
        var token = lifetime.Token;
        try
        {
            progress?.Report(new("正在检查更新…"));
            using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            checkTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await client.GetAsync(releaseUri, checkTimeout.Token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(checkTimeout.Token));
            var root = document.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()
                || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return UpdateResult.None;
            var tag = root.GetProperty("tag_name").GetString();
            if (!Version.TryParse(tag?.TrimStart('v', 'V'), out var latest) || latest <= currentVersion) return UpdateResult.None;
            var installerName = $"MalodyVThemeLoader-Setup-{latest}.exe";
            var assets = root.GetProperty("assets").EnumerateArray().ToList();
            string AssetUrl(string name) => assets.FirstOrDefault(a => a.GetProperty("name").GetString() == name)
                is var asset && asset.ValueKind != JsonValueKind.Undefined
                ? asset.GetProperty("browser_download_url").GetString() ?? throw new InvalidDataException("下载地址为空。")
                : throw new InvalidDataException($"GitHub Release 缺少 {name}。");
            var installerUrl = AssetUrl(installerName); var checksumUrl = AssetUrl("SHA256SUMS.txt");
            temporaryRoot = Path.Combine(Path.GetTempPath(), "MalodyVThemeLoader-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            var installerPath = Path.Combine(temporaryRoot, installerName);
            var checksumPath = Path.Combine(temporaryRoot, "SHA256SUMS.txt");
            await DownloadAsync(client, checksumUrl, checksumPath, null, token);
            var expected = FindExpectedHash(checksumPath, installerName);
            await DownloadAsync(client, installerUrl, installerPath, progress, token, latest.ToString());
            progress?.Report(new("正在校验安装包…"));
            var actual = await Task.Run(() => ComputeSha256(installerPath, token), token);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return UpdateResult.Failed("安装包校验失败 · 请重试更新");
            token.ThrowIfCancellationRequested();
            progress?.Report(new("更新已就绪 · 正在准备安装…"));
            if (beforeInstall != null && !await beforeInstall()) return UpdateResult.Cancelled;
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo { FileName = installerPath, WorkingDirectory = temporaryRoot, UseShellExecute = true };
            start.ArgumentList.Add("--install"); start.ArgumentList.Add(Path.GetFullPath(gameRoot));
            if (!launch(start)) throw new InvalidOperationException("无法启动更新安装器。");
            started = true; return new(UpdateState.Started, $"已启动 ThemeLoader {latest} 更新。", latest);
        }
        catch (OperationCanceledException) { return cancellationToken.IsCancellationRequested ? UpdateResult.Cancelled : UpdateResult.Failed("更新连接超时 · 请重试更新"); }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or JsonException
            or InvalidOperationException or KeyNotFoundException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { return UpdateResult.Failed("更新失败：" + e.Message); }
        finally { if (!started && temporaryRoot != null) { try { Directory.Delete(temporaryRoot, true); } catch { } } }
    }
    private static async Task DownloadAsync(HttpClient client, string url, string destination, IProgress<UpdateProgress>? progress,
        CancellationToken token, string version = "")
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
        idle.CancelAfter(TimeSpan.FromSeconds(45));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, idle.Token);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(idle.Token);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        var total = response.Content.Headers.ContentLength; long received = 0; var buffer = new byte[131072];
        var clock = Stopwatch.StartNew(); long lastReport = -250;
        progress?.Report(new($"正在下载 {version}", 0, total));
        while (true)
        {
            idle.CancelAfter(TimeSpan.FromSeconds(45));
            var read = await source.ReadAsync(buffer.AsMemory(), idle.Token);
            if (read == 0) break;
            await target.WriteAsync(buffer.AsMemory(0, read), idle.Token); received += read;
            if (clock.ElapsedMilliseconds - lastReport >= 100)
            { lastReport = clock.ElapsedMilliseconds; progress?.Report(new($"正在下载 {version}", received, total)); }
        }
        if (total.HasValue && total != received) throw new InvalidDataException("安装包下载不完整，请重试。");
        progress?.Report(new($"正在下载 {version}", received, total));
    }
    private static string ComputeSha256(string path, CancellationToken token)
    {
        using var stream = File.OpenRead(path); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[131072]; int read;
        while ((read = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static string FindExpectedHash(string path, string name)
    {
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*') == name && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit)) return parts[0];
        }
        throw new InvalidDataException($"校验文件中没有 {name} 的有效条目。");
    }
    private static Version CurrentVersion() => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
}
internal sealed record UpdateProgress(string Message, long? Received = null, long? Total = null)
{
    public int? Percent => Total > 0 && Received.HasValue ? (int)Math.Clamp(Received.Value * 100d / Total.Value, 0, 100) : null;
    public string Detail => Received.HasValue ? (Percent.HasValue ? $"{Percent}% · {Received.Value / 1048576d:F1} / {Total!.Value / 1048576d:F1} MiB" : $"已下载 {Received.Value / 1048576d:F1} MiB") : "";
}
internal sealed record UpdateResult(UpdateState State, string Message, Version? Version = null)
{
    public static UpdateResult None => new(UpdateState.None, "");
    public static UpdateResult Cancelled => new(UpdateState.Cancelled, "已取消本次更新");
    public static UpdateResult Failed(string message) => new(UpdateState.Failed, message);
}
internal enum UpdateState { None, Started, Failed, Cancelled }
