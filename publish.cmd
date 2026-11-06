@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo  正在发布 DiskAnalyzer (自包含单文件)
echo ========================================
echo.

if exist publish rmdir /s /q publish

dotnet publish DiskAnalyzer.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o publish

if errorlevel 1 (
    echo.
    echo [错误] 发布失败
    pause
    exit /b 1
)

echo.
echo ========================================
echo  发布完成
echo ========================================
dir /b publish
echo.
echo 输出目录: %~dp0publish
echo.
echo 如果你要发到 GitHub Releases：
echo   把 publish\DiskAnalyzer.exe 重命名成 DiskAnalyzer-v1.0-win-x64.exe
echo   然后上传到 Release 即可
echo.
pause