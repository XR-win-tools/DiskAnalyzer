using System.Diagnostics;
using System.IO;
using DiskAnalyzer.Models;

namespace DiskAnalyzer.Services;

public class DiskScanner
{
    private const int TopFilesHardLimit = 20000;
    private const int TopFilesKeep = 5000;
    private const int TopFilesFinal = 500;
    private const int ProgressIntervalMs = 100;

    public ScanResult Scan(ScanSession session)
    {
        var sw = Stopwatch.StartNew();
        var ct = session.Cts.Token;
        string rootPath = session.Path;

        var topFiles = new List<FileEntry>();
        var typeStats = new Dictionary<string, FileTypeStat>(StringComparer.OrdinalIgnoreCase);
        long totalFiles = 0, totalSize = 0, foldersScanned = 0;
        var lastReport = Stopwatch.StartNew();

        string rootName = Path.GetFileName(rootPath.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(rootName)) rootName = rootPath;

        var root = new FolderNode { Name = rootName, FullPath = rootPath };
        var stack = new Stack<FolderNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var node = stack.Pop();
            foldersScanned++;

            DirectoryInfo dirInfo;
            try { dirInfo = new DirectoryInfo(node.FullPath); }
            catch { continue; }

            // ---- 扫描文件 ----
            try
            {
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    ct.ThrowIfCancellationRequested();
                    long size;
                    try { size = file.Length; } catch { continue; }

                    node.Size += size;
                    node.FileCount++;
                    totalFiles++;
                    totalSize += size;

                    topFiles.Add(new FileEntry
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Directory = file.DirectoryName ?? "",
                        Size = size
                    });

                    string ext = string.IsNullOrEmpty(file.Extension)
                        ? "(无扩展名)" : file.Extension.ToLowerInvariant();
                    if (!typeStats.TryGetValue(ext, out var stat))
                    {
                        stat = new FileTypeStat { Extension = ext };
                        typeStats[ext] = stat;
                    }
                    stat.Count++;
                    stat.TotalSize += size;

                    if (lastReport.ElapsedMilliseconds >= ProgressIntervalMs)
                    {
                        lastReport.Restart();
                        session.UpdateProgress(totalFiles, foldersScanned, totalSize, file.FullName);
                    }

                    if (topFiles.Count > TopFilesHardLimit)
                    {
                        topFiles.Sort((a, b) => b.Size.CompareTo(a.Size));
                        topFiles.RemoveRange(TopFilesKeep, topFiles.Count - TopFilesKeep);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }

            // ---- 压入子目录 ----
            try
            {
                foreach (var sub in dirInfo.EnumerateDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        // 跳过符号链接/重解析点，避免死循环
                        if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch { continue; }

                    var child = new FolderNode { Name = sub.Name, FullPath = sub.FullName };
                    node.Children.Add(child);
                    stack.Push(child);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
        }

        // ---- 自底向上聚合大小 ----
        Aggregate(root);

        // ---- 最终 Top 排序 ----
        topFiles.Sort((a, b) => b.Size.CompareTo(a.Size));
        if (topFiles.Count > TopFilesFinal)
            topFiles.RemoveRange(TopFilesFinal, topFiles.Count - TopFilesFinal);

        // ---- 扩展名占比 ----
        var fileTypes = typeStats.Values.OrderByDescending(t => t.TotalSize).ToList();
        foreach (var s in fileTypes)
            s.Percent = totalSize > 0 ? s.TotalSize * 100.0 / totalSize : 0;

        return new ScanResult
        {
            Root = root,
            TopFiles = topFiles,
            FileTypes = fileTypes,
            TotalSize = totalSize,
            TotalFiles = totalFiles,
            ElapsedSeconds = sw.Elapsed.TotalSeconds
        };
    }

    private static void Aggregate(FolderNode node)
    {
        long childSize = 0, childFiles = 0;
        foreach (var child in node.Children)
        {
            Aggregate(child);
            childSize += child.Size;
            childFiles += child.FileCount;
        }
        node.Size += childSize;
        node.FileCount += childFiles;
        // 按大小降序，前端渲染更好看
        node.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
    }
}