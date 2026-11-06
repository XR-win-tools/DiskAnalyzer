namespace DiskAnalyzer.Models;

public class FileEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Directory { get; set; } = "";
    public long Size { get; set; }
}