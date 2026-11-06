using System.Collections.Concurrent;

namespace DiskAnalyzer.Services;

public class ScanSessionManager
{
    private readonly ConcurrentDictionary<string, ScanSession> _sessions = new();

    public ScanSession Create()
    {
        Cleanup();
        var s = new ScanSession();
        _sessions[s.Id] = s;
        return s;
    }

    public ScanSession? Get(string id) =>
        _sessions.TryGetValue(id, out var s) ? s : null;

    private void Cleanup()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(30);
        foreach (var kv in _sessions)
        {
            if (kv.Value.IsDone && kv.Value.CreatedAt < cutoff)
                _sessions.TryRemove(kv.Key, out _);
        }
    }
}