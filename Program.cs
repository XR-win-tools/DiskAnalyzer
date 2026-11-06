using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using DiskAnalyzer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ============ 启动参数 ============
var port = GetFreePort();
var token = Guid.NewGuid().ToString("N");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.AddSingleton<ScanSessionManager>();
builder.Services.AddSingleton<DiskScanner>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.MaxDepth = 256;
});

var app = builder.Build();

// ============ 静态资源：外部优先，否则嵌入资源 ============
var asm = typeof(ScanSession).Assembly;
var externalWwwRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var useExternal = Directory.Exists(externalWwwRoot);

app.Use(async (ctx, next) =>
{
    // API 请求直接放行
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var path = ctx.Request.Path.Value ?? "/";
    if (string.IsNullOrEmpty(path) || path == "/") path = "/index.html";

    // 简易目录穿越防护
    if (path.Contains(".."))
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    // 1) 优先用 exe 旁边的 wwwroot（方便用户覆盖 UI，无需重编译）
    if (useExternal)
    {
        var rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(externalWwwRoot, rel);
        if (File.Exists(filePath))
        {
            ctx.Response.ContentType = GetContentType(path);
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            await ctx.Response.SendFileAsync(filePath);
            return;
        }
    }

    // 2) 回退到嵌入资源
    var resourceName = "DiskAnalyzer.wwwroot" + path.Replace('/', '.');
    var stream = asm.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
        await next();
        return;
    }

    ctx.Response.ContentType = GetContentType(path);
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    using (stream)
    {
        await stream.CopyToAsync(ctx.Response.Body);
    }
});

// ============ Token 校验（仅 /api） ============
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        var t = ctx.Request.Query["t"].ToString();
        if (string.IsNullOrEmpty(t))
            t = ctx.Request.Headers["X-Token"].ToString();
        if (t != token)
        {
            ctx.Response.StatusCode = 401;
            return;
        }
    }
    await next();
});

// ============ API ============

app.MapGet("/api/drives", () =>
{
    var drives = DriveInfo.GetDrives()
        .Where(d => d.IsReady)
        .Select(d => new
        {
            name = d.Name,
            label = d.VolumeLabel,
            totalSize = d.TotalSize,
            freeSpace = d.TotalFreeSpace,
            format = d.DriveFormat,
            type = d.DriveType.ToString()
        });
    return Results.Ok(drives);
});

app.MapPost("/api/scan", (ScanRequest req, ScanSessionManager mgr, DiskScanner scanner) =>
{
    if (string.IsNullOrWhiteSpace(req.Path) || !Directory.Exists(req.Path))
        return Results.BadRequest(new { error = "目录不存在或无权访问" });

    var session = mgr.Create();
    session.Path = req.Path;

    _ = Task.Run(() =>
    {
        try
        {
            session.Result = scanner.Scan(session);
            session.IsDone = true;
        }
        catch (OperationCanceledException)
        {
            session.IsCancelled = true;
            session.IsDone = true;
        }
        catch (Exception ex)
        {
            session.Error = ex.Message;
            session.IsDone = true;
        }
    });

    return Results.Ok(new { id = session.Id });
});

app.MapGet("/api/scan/{id}", (string id, ScanSessionManager mgr) =>
{
    var session = mgr.Get(id);
    if (session == null) return Results.NotFound();

    lock (session)
    {
        return Results.Ok(new
        {
            id = session.Id,
            filesScanned = session.FilesScanned,
            foldersScanned = session.FoldersScanned,
            totalSize = session.TotalSize,
            currentPath = session.CurrentPath,
            isDone = session.IsDone,
            isCancelled = session.IsCancelled,
            error = session.Error,
            result = session.IsDone && !session.IsCancelled && session.Error == null
                ? session.Result
                : null
        });
    }
});

app.MapPost("/api/scan/{id}/cancel", (string id, ScanSessionManager mgr) =>
{
    mgr.Get(id)?.Cts.Cancel();
    return Results.Ok();
});

app.MapPost("/api/open", (OpenRequest req) =>
{
    try
    {
        if (req.Reveal && File.Exists(req.Path))
            Process.Start("explorer.exe", $"/select,\"{req.Path}\"");
        else if (Directory.Exists(req.Path))
            Process.Start("explorer.exe", $"\"{req.Path}\"");
        else if (File.Exists(req.Path))
            Process.Start(new ProcessStartInfo(req.Path) { UseShellExecute = true });
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ============ 打开浏览器 ============
var url = $"http://127.0.0.1:{port}/?t={token}";
try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
catch { /* 无浏览器时忽略 */ }

Console.WriteLine("================================");
Console.WriteLine("  磁盘空间分析器已启动");
Console.WriteLine($"  {url}");
Console.WriteLine("  按 Ctrl+C 或关闭本窗口即可退出");
Console.WriteLine("================================");

app.Run();

// ============ 工具函数 ============
static int GetFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static string GetContentType(string path)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();
    return ext switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".map" => "application/json; charset=utf-8",
        _ => "application/octet-stream"
    };
}

// ============ 请求 DTO ============
public record ScanRequest(string Path);
public record OpenRequest(string Path, bool Reveal);