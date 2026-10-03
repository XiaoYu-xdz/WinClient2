# BetterDesktop — Windows 11 Shell 扩展

## ⚡ 快速上手（普通用户）

### 打开即用（推荐）

1. 双击运行 **`BetterDesktopPopup.exe`**（无需安装、无需注册）
2. 在文件资源管理器中，将鼠标**悬停在文件夹上**并**按住左 Shift 键**
3. 自动弹出窗口显示该文件夹内容：
   - **悬停**子文件夹 → 继续展开其内容（级联窗口）
   - **双击**文件 → 用默认程序打开
   - **双击**文件夹 → 在当前资源管理器窗口中新增标签页打开
   - 鼠标移开后停留 1.5 秒自动收起，移动到任一窗口上则不消失

### 自定义悬停延迟（可选）

修改程序同目录下的 `config.json`（首次运行自动生成），**保存立即生效，无需重启**：

```json
{
  "lingerMs": 1500,     -- 鼠标移出文件夹后，主窗口延迟关闭(毫秒)
  "childCloseMs": 1500, -- 级联子窗口延迟关闭(毫秒)
  "graceMs": 500        -- 弹窗间缝隙宽限期(毫秒)
}
```

外部程序也可直接改写该文件来控制窗口保持显示的时间。

### 可选：安装 Shell 扩展

想用**右键菜单**也能触发，或需要后台 DLL 协同？运行 `setup.cmd` 选择「安装 Shell 扩展」；卸载运行 `setup.cmd` 选择「卸载」。

### 发布包文件说明

| 文件 | 用途 |
|------|------|
| `BetterDesktopPopup.exe` | 主程序，打开即用，双击运行 |
| `BetterDesktopHandler.dll` | Shell 扩展 DLL（可选，与 exe 同目录） |
| `setup.cmd` | 安装/卸载菜单 |
| `register.cmd` / `unregister.cmd` | 安装/卸载脚本 |
| `src/` | 完整源码工程 |

---

## 功能概述

当鼠标悬停在任意文件夹上并按下 **左 Shift 键** 时，弹出自定义信息窗口，显示该文件夹的内容列表。

### 核心特性

- **Shift + 悬停**：在文件夹上悬停 + 按下左 Shift → 弹出信息窗口
- **文件夹内容浏览**：显示文件夹中的文件和子文件夹（深度 1）
- **悬停展开**：鼠标悬停在子文件夹上 500ms → 自动展开显示其内容（递归）
- **双击打开**：
  - 双击文件 → 用默认程序打开
  - 双击文件夹 → 在资源管理器中打开（Windows 11 中会在已有窗口新增标签页）
- **自动关闭**：鼠标移出窗口区域 150ms 后自动关闭（类似工具提示行为）
- **Win11 风格**：DWM 圆角窗口、Segoe UI 字体、系统 Shell 图标

## 项目结构

```
BetterDesktop/
├── BetterDesktop.sln                          # 解决方案文件
├── BetterDesktopHandler/                      # DLL 项目（SharpShell 扩展）
│   ├── BetterDesktopHandler.csproj            # 项目文件
│   ├── InfoTipHandler.cs                      # 核心：SharpInfoTipHandler 继承类
│   ├── PipeClient.cs                          # 命名管道客户端（发送消息到 EXE）
│   ├── PipeMessage.cs                         # 管道通信消息类型
│   └── Properties/
│       └── AssemblyInfo.cs                    # 程序集信息（ComVisible = true）
├── BetterDesktopPopup/                        # EXE 项目（弹出窗口）
│   ├── BetterDesktopPopup.csproj              # 项目文件
│   ├── Program.cs                             # 入口点（单实例 Mutex）
│   ├── PopupForm.cs                           # 弹出窗口主窗体（TreeView + 交互逻辑）
│   ├── PipeServer.cs                          # 命名管道服务端（接收 DLL 消息）
│   ├── NativeMethods.cs                       # Win32/DWM API 声明
│   ├── PipeMessage.cs                         # 管道通信消息类型
│   └── Properties/
│       └── AssemblyInfo.cs
└── README.md                                  # 本文件
```

## 技术栈

| 组件 | 技术 |
|------|------|
| 语言 | C# 7.3+ |
| 框架 | .NET Framework 4.8 |
| Shell 扩展框架 | SharpShell 2.8.0 |
| 进程间通信 | 命名管道 (Named Pipe) + JSON |
| UI | WinForms (TreeView 自定义绘制) |
| 窗口管理 | DWM API (圆角)、Win32 API (图标) |

## 架构设计

```
┌─────────────────────────────────────────────────────────┐
│                    Explorer.exe 进程                    │
│  ┌──────────────────────────────────────────────────┐   │
│  │           BetterDesktopHandler.dll               │   │
│  │  ┌────────────────────┐    ┌──────────────────┐  │   │
│  │  │  InfoTipHandler    │───→│  PipeClient      │  │   │
│  │  │(SharpInfoTipHandler)│   │ (NamedPipe 客户端)│  │   │
│  │  │  检测 Shift + 悬停  │    │  发送消息到 EXE   │  │   │
│  │  └────────────────────┘    └────────┬─────────┘  │   │
│  └───────────────────────────────────────┼──────────┘   │
└──────────────────────────────────────────┼──────────────┘
                                           │ 命名管道
                                           │
┌──────────────────────────────────────────┼──────────────┐
│          BetterDesktopPopup.exe 进程      │             │
│  ┌──────────────────────┐    ┌───────────┴──────────┐   │
│  │  PopupForm           │←───│  PipeServer          │   │
│  │  (WinForms 弹出窗口)  │    │ (NamedPipe 服务端)   │   │
│  │  TreeView + 交互逻辑  │    │  接收消息 → 更新 UI   │   │
│  └──────────────────────┘    └──────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## 编译方法

### 前提条件

1. **Windows 11**（64位）
2. **Visual Studio 2022** 或 **.NET Framework 4.8 SDK**
3. **PowerShell（管理员权限）** — 用于注册 Shell 扩展

### 编译步骤

```powershell
# 1. 进入项目目录
cd BetterDesktop

# 2. 还原 NuGet 包
dotnet restore

# 3. 编译 DLL（x64, Debug）
dotnet build BetterDesktopHandler\BetterDesktopHandler.csproj -c Debug -p:Platform=x64

# 4. 编译 EXE（x64, Debug）
dotnet build BetterDesktopPopup\BetterDesktopPopup.csproj -c Debug -p:Platform=x64

# 5. （可选）编译 Release 版本
dotnet build BetterDesktopHandler\BetterDesktopHandler.csproj -c Release -p:Platform=x64
dotnet build BetterDesktopPopup\BetterDesktopPopup.csproj -c Release -p:Platform=x64
```

### 编译产物

```
BetterDesktopHandler\bin\x64\Debug\BetterDesktopHandler.dll
BetterDesktopPopup\bin\x64\Debug\BetterDesktopPopup.exe
```

## 注册 Shell 扩展

> ⚠️ **高风险操作**：以下操作涉及注册表修改和重启资源管理器，请仔细确认。

### 步骤 1：注册 DLL 到 COM

```powershell
# 以管理员身份打开 PowerShell，进入 DLL 输出目录
cd BetterDesktop\BetterDesktopHandler\bin\x64\Debug

# 使用 regasm 注册 COM 组件
# /codebase: 注册代码库路径（允许从非 GAC 位置加载）
# /tlb: 同时生成类型库文件
.\regasm.exe BetterDesktopHandler.dll /codebase /tlb

# 如果系统提示找不到 regasm.exe，请使用完整路径：
# C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe BetterDesktopHandler.dll /codebase
```

### 步骤 2：注册 Shell 扩展到资源管理器

```powershell
# 使用 SharpShell 的 ServerManager 工具注册
# 下载地址：https://github.com/dwmkerr/sharpshell/releases
.\ServerManager.exe install BetterDesktopHandler.dll

# 或者使用 regasm 的完整注册方式（已包含 SharpShell 注册）：
# 上面步骤 1 的 regasm /codebase 已足够
```

### 步骤 3：重启资源管理器

```powershell
# ⚠️ 此操作会关闭并重启文件资源管理器，请先保存所有打开的文件夹
# 方法一：使用任务管理器结束 explorer.exe 然后重新运行
# 方法二：使用命令
taskkill /f /im explorer.exe
start explorer.exe
```

### 步骤 4：验证安装

1. 打开任意文件资源管理器窗口
2. 将鼠标悬停在一个文件夹上，按下左 Shift 键
3. 应弹出信息窗口显示文件夹内容

## 卸载 Shell 扩展

```powershell
# 以管理员身份运行 PowerShell

# 1. 卸载 DLL 注册
cd BetterDesktopHandler\bin\x64\Debug
regasm BetterDesktopHandler.dll /unregister

# 2. 使用 ServerManager 卸载
.\ServerManager.exe uninstall BetterDesktopHandler.dll

# 3. 重启资源管理器
taskkill /f /im explorer.exe
start explorer.exe
```

## 调试方法

### 使用 DebugView 查看调试日志

1. 下载 [DebugView](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview)（Sysinternals 工具）
2. 以管理员身份运行 DebugView.exe
3. 勾选菜单 **Capture → Capture Win32**（或按 Ctrl+W）
4. 在资源管理器中触发功能（Shift + 悬停在文件夹上）
5. DebugView 中会显示所有以 `[BetterDesktop]` 开头的调试日志

### 常见调试日志

```
[BetterDesktop] InfoTipHandler 已创建
[BetterDesktop] GetInfoTip 被调用: C:\Users\...
[BetterDesktop] Shift 未按下，返回默认提示
[BetterDesktop] 已发送 ShowPopup 消息: C:\Users\...
[BetterDesktop] 启动 Popup EXE: ...\BetterDesktopPopup.exe
[BetterDesktopPipe] 管道服务已启动
[BetterDesktopPipe] 收到消息: Action=ShowPopup, Path=...
[BetterDesktopPopup] 显示窗口: C:\Users\...
[BetterDesktopPopup] 鼠标移出，自动关闭
```

## 常见问题

### Q: 弹出窗口没有出现
A: 检查以下步骤：
1. 是否以管理员身份注册了 DLL？
2. DebugView 中是否有 `[BetterDesktop]` 日志？
3. DLL 和 EXE 是否在同一目录下？
4. 是否已重启资源管理器？

### Q: 注册后无反应
A: 尝试重新注册：
```powershell
regasm BetterDesktopHandler.dll /unregister
regasm BetterDesktopHandler.dll /codebase
taskkill /f /im explorer.exe
start explorer.exe
```

### Q: 编译错误 "找不到 SharpShell"
A: 确保已执行 `dotnet restore` 还原 NuGet 包。如果网络受限，可手动下载 SharpShell 2.8.0 NuGet 包。

### Q: 弹出窗口无法点击交互
A: 确保 `BetterDesktopPopup.exe` 正在运行（可在任务管理器中查看）。如果未运行，重启资源管理器即可。

## 许可证

本项目仅供学习和个人使用。