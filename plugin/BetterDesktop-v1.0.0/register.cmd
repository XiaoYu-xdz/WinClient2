@echo off
chcp 65001 >nul
REM =====================================================
REM  BetterDesktop - 安装 Shell 扩展（相对路径，可放任意位置）
REM  与 BetterDesktopHandler.dll 存放在同一目录
REM =====================================================
cd /d "%~dp0"

set DLL=BetterDesktopHandler.dll

if not exist "%DLL%" (
    echo [错误] 找不到 %DLL%，请确认与本脚本在同一目录。
    pause
    exit /b 1
)

echo Step 1: 注册 COM 组件...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "%DLL%" /codebase

echo Step 2: 移除旧 InfoTip 注册...
reg delete "HKCR\Folder\shellex\InfoTip" /ve /f 2>nul

echo Step 3: 注册为文件夹右键菜单处理器...
reg add "HKCR\Directory\shellex\ContextMenuHandlers\BetterDesktop" /ve /d "{B8E2D3F1-5A7C-4E9B-8D1F-3C6A9B2E7F4D}" /f

echo Step 4: 重启资源管理器...
taskkill /f /im explorer.exe
start explorer.exe

echo.
echo 安装完成！使用方法：
echo   1. 双击运行 BetterDesktopPopup.exe（打开即用）
echo   2. 在资源管理器中悬停文件夹 + 按住左 Shift → 弹出内容窗口
echo.   右键文件夹也可触发（需本脚本注册的 Shell 扩展）
pause