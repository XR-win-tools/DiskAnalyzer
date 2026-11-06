using DiskAnalyzer.Models;

namespace DiskAnalyzer.Services;

public class ScanSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = "";
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public CancellationTokenSource Cts { get; } = new();

    public long FilesScanned { get; private set; }
    public long FoldersScanned { get; private set; }
    public long TotalSize { get; private set; }
    public string CurrentPath { get; private set; } = "";

    public bool IsDone { get; set; }
    public bool IsCancelled { get; set; }
    public string? Error { get; set; }
    public ScanResult? Result { get; set; }

    public void UpdateProgress(long files, long folders, long size, string currentPath)
    {
        lock (this)
        {
            FilesScanned = files;
            FoldersScanned = folders;
            TotalSize = size;
            CurrentPath = currentPath;
        }
    }
}