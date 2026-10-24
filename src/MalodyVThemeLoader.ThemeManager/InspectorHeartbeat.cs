using MalodyVThemeLoader.Shared;

namespace MalodyVThemeLoader.ThemeManager;

// This timer must never access WinForms controls: modal dialogs and long UI
// operations must not interrupt the game connection's five-second lease.
internal sealed class InspectorHeartbeat : IDisposable
{
    private readonly object gate = new();
    private readonly string root;
    private readonly System.Threading.Timer timer;
    private InspectorControl current = new();
    private bool disposed;
    internal InspectorHeartbeat(string root)
    {
        this.root = root;
        timer = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
    }
    internal void Update(InspectorControl value)
    {
        lock (gate)
        {
            if (disposed) return;
            current = new InspectorControl {
                Client = value.Client, Enabled = value.Enabled, ShowFrames = value.ShowFrames,
                ShowNames = value.ShowNames, IncludeInactive = value.IncludeInactive, PointerSearch = value.PointerSearch,
                Runtime = value.Runtime, SelectedId = value.SelectedId, SelectRequest = value.SelectRequest
            };
            Write();
        }
    }
    private void Tick()
    {
        lock (gate) { if (!disposed && current.Enabled) { try { Write(); } catch { /* Next heartbeat retries. */ } } }
    }
    private void Write()
    {
        var owner = InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName);
        if (owner?.IsLive == true && owner.Client != current.Client) return;
        current.LeaseUtc = DateTime.UtcNow;
        InspectorFiles.Write(root, InspectorFiles.ControlName, current);
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; timer.Dispose(); current.Enabled = false;
            try { if (InspectorFiles.Read<InspectorControl>(root, InspectorFiles.ControlName)?.Client == current.Client) Write(); } catch { }
        }
    }
}
