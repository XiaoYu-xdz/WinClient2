@echo off
chcp 65001 >nul
cd /d "%~dp0"

title BetterDesktop 安装程序

echo ============================================
echo   BetterDesktop v1.0.0 - 安装程序
echo ============================================
echo.
echo   1) 基本使用（推荐）: 运行 BetterDesktopPopup.exe
echo      悬停文件夹 + 按住左 Shift 弹出内容窗口
echo.
echo   2) 安装 Shell 扩展（可选）: 右键文件夹也能触发
echo   3) 卸载 Shell 扩展
echo   4) 退出
echo.
echo ============================================
set /p choice="请输入选项 (1-4): "

if "%choice%"=="1" (
    if exist "BetterDesktopPopup.exe" (
        start "" "BetterDesktopPopup.exe"
        echo 已启动 BetterDesktopPopup.exe
    ) else (
        echo [错误] 找不到 BetterDesktopPopup.exe
    )
    pause
) else if "%choice%"=="2" (
    call register.cmd
) else if "%choice%"=="3" (
    call unregister.cmd
) else if "%choice%"=="4" (
    exit /b
) else (
    echo 无效选项！
    pause
)