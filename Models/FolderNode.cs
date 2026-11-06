namespace DiskAnalyzer.Models;

public class FolderNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
    public long FileCount { get; set; }
    public List<FolderNode> Children { get; set; } = new();
}