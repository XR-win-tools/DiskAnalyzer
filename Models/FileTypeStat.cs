namespace DiskAnalyzer.Models;

public class FileTypeStat
{
    public string Extension { get; set; } = "";
    public int Count { get; set; }
    public long TotalSize { get; set; }
    public double Percent { get; set; }
}