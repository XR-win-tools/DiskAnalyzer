namespace DiskAnalyzer.Models;

public class ScanResult
{
    public FolderNode Root { get; set; } = null!;
    public List<FileEntry> TopFiles { get; set; } = new();
    public List<FileTypeStat> FileTypes { get; set; } = new();
    public long TotalSize { get; set; }
    public long TotalFiles { get; set; }
    public double ElapsedSeconds { get; set; }
}