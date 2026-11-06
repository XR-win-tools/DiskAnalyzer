# 磁盘空间分析器 (DiskAnalyzer)

一个轻量的 Windows 磁盘空间分析工具，类似 WinDirStat / TreeSize，但 **UI 用 HTML 写** —— 启动后自动打开浏览器，界面简洁清爽，源码友好、易改。

## ⬇️ 下载即用（推荐）

从 [Releases](https://github.com/你的用户名/DiskAnalyzer/releases) 页面下载 `DiskAnalyzer-win-x64.zip`，解压后**双击 `DiskAnalyzer.exe`** 即可，无需安装任何依赖。

> **首次运行被 SmartScreen 拦截？** 因为我们没有花钱做代码签名，Windows 会提示「Windows 已保护你的电脑」。点「更多信息」→「仍要运行」即可。

启动后会自动打开浏览器，关掉黑框控制台窗口 = 退出程序。

## ✨ 功能

- 🔍 递归扫描任意目录，实时进度
- 🌲 文件夹树（懒加载，双击名字在资源管理器中打开）
- 📄 Top 500 最大文件（单击定位到资源管理器）
- 📊 文件类型分布（扩展名聚合 + 占比条）
- 🛡️ 跳过符号链接/重解析点，避免死循环
- 🔒 只监听 `127.0.0.1`，随机端口 + 一次性 token，其他网站无法跨站访问
- ⚡ 无 npm、无框架，原生 HTML/JS

## 🎨 想改 UI？

两种方式，任选其一：

**方式 A（不用重新编译）**：在 `DiskAnalyzer.exe` 同目录下放一个 `wwwroot/` 文件夹，里面包含 `index.html` / `style.css` / `app.js`。程序会**优先使用外部文件**，改完刷新浏览器即可看到效果。

**方式 B（改源码）**：克隆仓库，改 `wwwroot/` 里的文件，然后 `dotnet run` 开发调试。

## 🧑‍💻 从源码构建

环境要求：**Windows 10/11 + .NET 8 SDK**

```bash
git clone https://github.com/你的用户名/DiskAnalyzer.git
cd DiskAnalyzer
dotnet run
或者双击 run.cmd