@echo off
chcp 65001 >nul
REM =====================================================
REM  BetterDesktop - 卸载 Shell 扩展（相对路径）
REM =====================================================
cd /d "%~dp0"

set DLL=BetterDesktopHandler.dll

if exist "%DLL%" (
    echo 注销 COM 组件...
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "%DLL%" /unregister
)

reg delete "HKCR\Directory\shellex\ContextMenuHandlers\BetterDesktop" /f 2>nul
reg delete "HKCR\Folder\shellex\InfoTip" /ve /f 2>nul

taskkill /f /im explorer.exe
start explorer.exe

echo.
echo 已卸载 Shell 扩展。
echo 提示：BetterDesktopPopup.exe 不需要卸载，停止运行即可。
pause