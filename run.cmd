@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 正在启动 磁盘空间分析器...
dotnet run -c Release --project DiskAnalyzer.csproj
if errorlevel 1 pause