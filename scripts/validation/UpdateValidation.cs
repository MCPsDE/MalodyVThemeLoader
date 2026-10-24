using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MalodyVThemeLoader.Shared;
using MalodyVThemeLoader.ThemeManager;

internal static class UpdateValidation
{
    private static int checks;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    public static async Task Main(string[] args)
    {
        foreach (var mode in new[] { "ok", "unknown", "hash", "missing", "truncated", "cancel", "decline", "launch", "current", "http", "malformed" })
        {
            using var handler = new Fixture(mode); using var client = new HttpClient(handler);
            using var cancellation = new CancellationTokenSource(); var reports = new List<UpdateProgress>();
            var launches = 0; string? staged = null;
            var progress = new InlineProgress(p => { reports.Add(p); if (mode == "cancel" && p.Received > 0) cancellation.Cancel(); });
            var result = await UpdateService.RunAsync(client, new Uri("https://fixture/release"), new Version(2,6,2), args[0], progress,
                cancellation.Token, () => Task.FromResult(mode != "decline"), start =>
                {
                    launches++; staged = start.WorkingDirectory;
                    Require(start.ArgumentList.SequenceEqual(new[] { "--install", Path.GetFullPath(args[0]) }), "Installer arguments changed.");
                    Require(File.Exists(start.FileName), "Installer was not staged."); return mode != "launch";
                });
            Require(result.State == (mode is "ok" or "unknown" ? UpdateState.Started : mode is "cancel" or "decline" ? UpdateState.Cancelled : mode == "current" ? UpdateState.None : UpdateState.Failed), mode + ": " + result);
            Require(launches == (mode is "ok" or "unknown" or "launch" ? 1 : 0), "Unverified/cancelled update was launched: " + mode);
            var download = reports.Where(p => p.Received.HasValue).ToArray();
            Require(download.Zip(download.Skip(1)).All(pair => pair.First.Received <= pair.Second.Received), "Progress moved backwards.");
            if (mode is "ok" or "unknown")
            {
                Require(download.Last().Received == handler.Bytes.Length && reports.Any(p => p.Message.Contains("校验")), "Byte progress or verification phase missing.");
                Require(mode == "unknown" ? download.All(p => p.Percent == null && p.Detail.Contains("MiB")) : download.Last().Percent == 100, "Unknown/known lengths were misrepresented.");
                Directory.Delete(staged!, true);
            }
            if (mode == "launch") Require(!Directory.Exists(staged), "Failed launch leaked its staged directory.");
            Console.WriteLine("PASS update " + mode);
        }
        var root = Path.Combine(args[0], "heartbeat"); Directory.CreateDirectory(root);
        var control = new InspectorControl { Client = "ours", Enabled = true, ShowNames = false, PointerSearch = true, SelectedId = 7, SelectRequest = 11 };
        using (var heartbeat = new InspectorHeartbeat(root))
        {
            heartbeat.Update(control);
            // No UI message pump for longer than the full game connection lease.
            Thread.Sleep(6200);
            var live = InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName)!;
            Require(live.PointerSearch, "Background heartbeat lost the pointer-search switch.");
            Require(live.IsLive && live.Client == "ours" && live.SelectedId == 7 && !live.ShowNames, "Background lease did not survive a blocked UI.");
            var foreign = new InspectorControl { Client = "another-manager", Enabled = true, LeaseUtc = DateTime.UtcNow };
            InspectorFiles.Write(root, InspectorFiles.ControlName, foreign); Thread.Sleep(1200);
            Require(InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName)!.Client == foreign.Client, "Heartbeat stole another live manager's lease.");
            control.Enabled = false; heartbeat.Update(control);
            foreign.LeaseUtc = DateTime.UtcNow.AddSeconds(-30); InspectorFiles.Write(root, InspectorFiles.ControlName, foreign); Thread.Sleep(1200);
            Require(!InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName)!.IsLive, "Disconnected heartbeat reconnected after another client expired.");
        }
        control.Enabled = true;
        InspectorFiles.Write(root, InspectorFiles.ControlName, new InspectorControl());
        var stopped = new InspectorHeartbeat(root); stopped.Update(control); stopped.Dispose(); Thread.Sleep(1200);
        Require(!InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName)!.IsLive, "Disposed pane continued renewing its lease.");
        Console.WriteLine($"PASS {checks} update/heartbeat assertions");
    }
    private sealed class InlineProgress(Action<UpdateProgress> report) : IProgress<UpdateProgress> { public void Report(UpdateProgress value) => report(value); }
    private sealed class Fixture(string mode) : HttpMessageHandler
    {
        public readonly byte[] Bytes = Enumerable.Range(0, 1100000).Select(i => (byte)(i % 251)).ToArray();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (mode == "http") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            HttpContent content;
            if (request.RequestUri!.AbsolutePath == "/release")
                content = mode == "malformed" ? new StringContent("{}") : new StringContent(JsonSerializer.Serialize(new { tag_name = mode == "current" ? "v2.6.2" : "v2.6.3", assets = new[] {
                    new { name = "MalodyVThemeLoader-Setup-2.6.3.exe", browser_download_url = "https://fixture/setup" },
                    new { name = mode == "missing" ? "no-checksums" : "SHA256SUMS.txt", browser_download_url = "https://fixture/checksums" } } }));
            else if (request.RequestUri.AbsolutePath == "/checksums")
                content = new StringContent((mode == "hash" ? new string('0',64) : Convert.ToHexString(SHA256.HashData(Bytes))) + "  MalodyVThemeLoader-Setup-2.6.3.exe\n");
            else
            {
                content = mode == "unknown" ? new UnknownLengthContent(Bytes) : new ByteArrayContent(Bytes);
                if (mode == "truncated") content.Headers.ContentLength = Bytes.Length + 100;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
