# DooTimer

一个 Windows 桌面小工具，用来监控你在电脑上刷抖音的时间。

- 检测抖音客户端（douyin.exe）和抖音网页（浏览器标题含"抖音"或"douyin.com"）
- 只在抖音处于**前台**（正在看）的时候计时
- 两种模式：**限制模式**（设额度，到时间提醒）和**统计模式**（只记录，不打扰）
- 默认每天 60 分钟额度，到 80% 和 100% 时弹出提醒
- 超时后每隔 5 分钟再提醒一次
- 可选择额度用完后自动关闭抖音客户端
- 在 Windows 任务栏上直接显示计时信息，不占屏幕空间

## 系统要求

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)（运行需要）
- 如果要自己编译，还需要 [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)

## 安装和运行

### 方式一：直接运行（推荐）

从 [Releases](https://github.com/rlep1016/DooTimer/releases) 页面下载最新的 `DooTimer.exe`，放到任意文件夹双击运行即可。

这是一个单文件程序，不需要安装任何东西（.NET Runtime 已打包在内）。

### 方式二：自己编译

```powershell
# 克隆项目
git clone https://github.com/rlep1016/DooTimer.git
cd DooTimer

# 编译
powershell -ExecutionPolicy Bypass -File build.ps1

# 运行
.\bin\Debug\net8.0-windows\win-x64\DooTimer.exe
```

打包为单文件 exe：

```powershell
dotnet publish -c Release -r win-x64
# 输出在 bin\Release\net8.0-windows\win-x64\publish\DooTimer.exe
```

## 界面

程序有四个页面：

| 页面 | 功能 |
|------|------|
| **总览** | 检测状态、来源、今日用时、进度条 |
| **设置** | 修改每日上限、提醒比例、检测间隔、主题、开关机启动等 |
| **数据** | 今日摘要、24 小时分布图、7 天趋势、导出 CSV |
| **关于** | 版本信息、文件状态、快捷入口 |

支持亮色/暗色/跟随系统三种主题。

## 配置

所有用户数据存放在 `%AppData%/DooTimer/`：

| 文件 | 说明 |
|------|------|
| `config.json` | 用户设置（模式、上限、提醒阈值、主题等） |
| `data/usage.json` | 统计数据（每日用时、会话记录） |
| `logs/dootimer.log` | 运行日志 |

配置项说明（`config.json`）：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `daily_limit_minutes` | int | 60 | 每日抖音时间上限（分钟） |
| `mode` | string | `"limit"` | 模式：`"limit"` 限制模式，`"track"` 统计模式 |
| `remind_at_percent` | int | 80 | 在额度 N% 时提醒一次 |
| `over_limit_reminder_minutes` | int | 5 | 超时后每隔 N 分钟重复提醒 |
| `theme` | string | `"light"` | 主题：`"light"` / `"dark"` / `"system"` |
| `force_close_enabled` | bool | false | 额度用完后是否强制关闭抖音客户端 |
| `rest_reminder_minutes` | int | 0 | 统计模式下连续刷 N 分钟后提醒休息，0=关闭 |
| `client_process_names` | string[] | `["douyin.exe"]` | 客户端进程名列表 |
| `browser_process_names` | string[] | `["chrome.exe","msedge.exe","firefox.exe","brave.exe"]` | 浏览器进程名列表 |
| `title_keywords` | string[] | `["抖音","Douyin","douyin.com"]` | 网页标题匹配关键词 |

## 识别规则

- **抖音客户端**：前台进程名匹配 `client_process_names` 中的任意一个（默认 `douyin.exe`）
- **抖音网页**：前台进程是浏览器，且窗口标题包含 `title_keywords` 中的任意关键词

检测间隔：刷抖音时 60ms，未刷时 500ms。

## 技术栈

| 项 | 选型 |
|---|------|
| 语言 | C# (.NET 8) |
| UI 框架 | WPF |
| 系统托盘 | WinForms NotifyIcon |
| 数据存储 | JSON（System.Text.Json） |
| 外部依赖 | **零个 NuGet 包**，全部用 .NET 内置能力 |

## 开源协议

MIT License — 详见 [LICENSE](LICENSE) 文件。
