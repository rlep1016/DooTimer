# DooTimer 项目说明

## 项目是什么

DooTimer 是一个 Windows 桌面小工具，用来监控你在电脑上刷抖音的时间。

- 检测抖音客户端（douyin.exe）和抖音网页（浏览器标题含"抖音"或"douyin.com"）
- 只在抖音处于前台（正在看）的时候计时
- 两种运行模式：**限制模式**（设额度，到时间提醒）和**统计模式**（只记录时间，不打扰）
- 默认每天 60 分钟额度，到 80% 和 100% 时弹出提醒
- 超时后每隔 5 分钟再提醒一次
- 不强制关闭抖音，只做提醒和统计
- 统计模式下：不弹额度提醒、不显示剩余额度、不显示进度条
- 统计模式下可开启**休息提醒**：连续刷抖音 N 分钟后弹出提示，建议活动一下

## 技术栈

| 项 | 选型 |
|---|------|
| 语言 | C# (.NET 8) |
| UI 框架 | WPF（Windows 原生桌面框架） |
| 系统托盘 | WinForms NotifyIcon（WPF 不自带托盘功能） |
| 数据存储 | JSON 文件（System.Text.Json，.NET 内置） |
| 进程检测 | System.Diagnostics.Process（.NET 内置，替代 Python 的 psutil） |
| Win32 调用 | P/Invoke（直接调用 Windows API，如获取前台窗口） |
| 外部依赖 | **零个 NuGet 包**，全部用 .NET 自带能力 |

## 项目结构

```
DooTimer-cs/                         # C# 项目根目录
├── DooTimer.csproj                  # 项目配置文件（告诉编译器这是什么项目、用什么框架）
├── App.xaml / App.xaml.cs           # 程序入口：启动、单实例检查、初始化所有服务、旧数据迁移
├── GlobalUsings.cs                  # 全局引用的命名空间
├── DooTimer.ico                     # 程序图标
│
├── Models/                          # 数据模型（定义数据结构）
│   ├── ForegroundTarget.cs          # 检测结果（是不是抖音、来源、进程名、窗口标题）
│   ├── AppConfig.cs                 # 配置模型（上限、提醒比例、检测间隔、进程名列表、主题等）
│   ├── DashboardSnapshot.cs         # UI 状态快照（不可变，线程间安全传递）
│   └── UsageSummary.cs              # 使用数据汇总（今日用时、会话记录、7 天趋势、上周对比）
│
├── Services/                        # 服务层（核心业务逻辑）
│   ├── ForegroundMonitor.cs         # 前台窗口检测（调用 Win32 API 获取前台窗口和进程名）
│   ├── UsageTracker.cs              # 时间跟踪核心（定时器驱动，检测→累加→提醒→生成快照）
│   ├── UsageStorage.cs              # 数据读写（JSON 文件读写、会话记录、CSV 导出）
│   ├── ConfigService.cs             # 配置管理（加载/保存/校验 config.json）
│   ├── Notifier.cs                  # 提醒通知（优先托盘气泡，回退 MessageBox）
│   ├── StartupService.cs            # 开机启动管理（创建/删除启动文件夹快捷方式）
│   ├── SingleInstanceService.cs     # 单实例保护（Mutex，防止重复启动）
│   └── TrayService.cs               # 系统托盘（右键菜单、气泡通知、双击打开面板）
│
├── Views/                           # 界面层（WPF 窗口和页面）
│   ├── MainWindow.xaml/.cs          # 主窗口（侧边栏导航 + 内容区 + 任务栏覆盖标签）
│   └── Pages/
│       ├── OverviewPage.xaml/.cs    # 总览页：检测状态、来源、今日用时、进度条
│       ├── SettingsPage.xaml/.cs    # 设置页：修改配置、开关机启动
│       ├── DataPage.xaml/.cs        # 数据页：今日摘要、7 天趋势图、最近记录、导出 CSV
│       └── AboutPage.xaml/.cs       # 关于页：版本信息、文件状态、快捷入口
│
├── Helpers/                         # 工具类
│   ├── NativeMethods.cs             # 所有 Win32 API 声明（P/Invoke）
│   ├── FormatHelper.cs              # 格式化工具（秒数转文字、文件大小等）
│   └── FileHelper.cs                # 文件操作（打开文件/文件夹）
│
└── Converters/                      # WPF 值转换器 + 配色常量 + 主题切换
    └── Converters.cs                # 数据绑定转换 + 亮/暗双套配色 + ApplyTheme()
```

## 核心架构

### 线程模型

```
UI 线程（主线程）
├── WPF 渲染 + 数据绑定
├── DispatcherTimer（100ms）：从 Tracker 拉快照 → 更新 UI
└── 托盘图标

Timer 线程（后台线程池）
├── ForegroundMonitor.Inspect()      → 检测前台窗口
├── UsageTracker 累加时间、检查提醒
└── 生成 DashboardSnapshot（不可变对象）

快照传递：Tracker 写入快照 → UI 读取快照。因为快照是 record 类型（不可变），读取不需要加锁。
```

### 检测流程

1. 定时器每 60ms（刷抖音时）或 500ms（未刷时）触发
2. `GetForegroundWindow()` 获取前台窗口句柄
3. `GetWindowThreadProcessId()` 获取进程 ID
4. `Process.GetProcessById()` 获取进程名（带缓存，每 2 秒全量刷新）
5. 进程名匹配（同时匹配带和不带 `.exe`，因为 .NET 返回不带后缀的进程名）
6. 如果是浏览器进程，再检查窗口标题是否包含关键词

### 额度管理与强制关闭

- 每日额度用完时弹提醒（80% 提醒一次，100% 提醒一次，之后每 N 分钟重复提醒）
- 设置页可开启"每日额度用完时强制关闭抖音客户端"
- **首次达到 100%** 时 Kill，**之后每次超限重复提醒（默认每 5 分钟）也会再次 Kill**——防止用户重新打开抖音
- Kill 逻辑：枚举**所有运行中进程**，用 `Contains` 模糊匹配进程名（如配置 `douyin.exe` 会杀 `douyin.exe`、`douyin_tray.exe`、`douyin_widget.exe` 等所有含 douyin 的进程）
- **不杀浏览器标签页**（`WM_CLOSE` 会关掉整个浏览器窗口，丢失所有标签页）
- 默认关闭，需手动在设置页开启

### 任务栏覆盖标签（替代悬浮窗）

悬浮窗已删除，改为在 Windows 任务栏上直接显示计时信息。

**实现原理**（参考 TrafficMonitor）：
```
Shell_TrayWnd（任务栏外壳）
  └─ ReBarWindow32（工具栏容器）
       ├─ MSTaskSwWClass  ← 用 SetWindowPos 缩小宽度，腾出空间
       ├─ [WPF 透明置顶窗口] ← 放在腾出的空间里
       └─ TrayNotifyWnd（通知区域）
```

1. `MainWindow.PositionOverlay()` 找到 `Shell_TrayWnd → ReBarWindow32 → MSTaskSwWClass`，缩小 `MSTaskSwWClass` 210px 来腾空间
2. 创建一个 `WindowStyle=None`、`Topmost=True`、`ShowInTaskbar=False` 的透明 WPF 窗口
3. 用 100ms `DispatcherTimer` 持续调用 `SetWindowPos(HWND_TOPMOST)` 防止被任务栏遮盖
4. 标签显示两行文字：上排「抖音 X 小时 X 分钟」、下排「剩余 X 分钟」
5. 文字颜色：绿色（正常）/ 红色（超限）/ 灰色（未刷），根据系统亮暗主题自动适配
6. 退出时恢复 `MSTaskSwWClass` 原始大小

**注意**：`SetWindowPos` 需要 `SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW` 标志，只改变 Z 轴位置不改变窗口大小。

### 通知方式

- 提醒优先使用**托盘图标气泡通知**（`NotifyIcon.ShowBalloonTip`），5 秒自动消失，不抢占焦点
- 如果托盘图标未就绪，回退到 `MessageBox`
- `Notifier.SetNotificationAction()` 支持注入自定义通知动作
- 入口：`App.xaml.cs` 启动时将 `TrayService.ShowBalloonTip` 注入 `Notifier`

### 暗色模式 / 主题切换

XAML 中所有主题色都通过 `{DynamicResource XxxBrush}` 绑定，不是 `{x:Static}`。切换主题时：

1. `AppColors.ApplyTheme()` 创建**全新** `SolidColorBrush`（不能改旧画笔的 Color，因为 WPF 会 Freeze 它们导致闪退）
2. 同时更新 `Application.Current.Resources`（XAML 感知）和 `AppColors` 静态字段（C# 代码感知）
3. 所有 `DynamicResource` 引用自动刷新

主题选项：`light`（亮色）、`dark`（暗色）、`system`（读注册表 `AppsUseLightTheme` 判断）

### 设置页 UI 细节

- 切换到"统计模式"时，限制相关输入框变灰，并在下方显示提示文字
- 统计模式提示通过 `TrackModeHint.Visibility` 控制显示/隐藏
- 所有设置行的标签文字和输入框都使用 `VerticalAlignment="Center"` 对齐

### 数据页增强统计

数据页展示以下统计区：

**今日摘要（6 个卡片）**
- 今日累计 / 今日次数 / 剩余额度
- **较上周同期**：显示 ↑↓ 箭头和百分比变化（红涨绿降）
- **今日最长单次**：当天最长一段连续刷抖音的时长
- **今日平均每次**：总时长 ÷ 会话次数

**24 小时分布图**
- 24 根柱子，每根代表一小时的累计用时
- 颜色按时段区分：深夜蓝 / 上午绿 / 下午黄 / 晚上红
- 顶部显示今日总量和最活跃时段
- 实现：`UsageStorage.GetHourlyBreakdown()` 将每条会话按小时边界拆分计算

### 休息提醒（仅统计模式）

统计模式下，如果连续刷抖音达到 `rest_reminder_minutes` 分钟（默认 0 = 关闭），弹出托盘气泡提醒用户休息。切走抖音时累计归零重新算。

实现：`UsageTracker._restAccumulatedSeconds` 在 `AddUsageSeconds` 中累加，`CheckRestReminder` 检查是否达标并重置。设置页填入 0 即可关闭。

## 图标

应用图标在 `DooTimer-cs/DooTimer.ico` 和 `assets/DooTimer.png`。

- 设计：深青底圆角方形 + 白色八分音符 + 青绿色进度环
- 同时作为 exe 文件图标、窗口标题栏图标、托盘图标
- `.csproj` 中通过 `<ApplicationIcon>DooTimer.ico</ApplicationIcon>` 嵌入
- 托盘图标通过 `TrayService.LoadIcon()` 从 exe 提取
- 侧边栏只显示纯文字 "DooTimer"，无图标

## 数据存储位置

所有用户数据统一存放在 `%AppData%/DooTimer/` 下：

| 文件 | 路径 | 说明 |
|------|------|------|
| config.json | `%AppData%/DooTimer/config.json` | 用户设置（模式、上限、提醒阈值、主题等） |
| usage.json | `%AppData%/DooTimer/data/usage.json` | 统计数据（每日用时、会话记录） |
| dootimer.log | `%AppData%/DooTimer/logs/dootimer.log` | 运行日志 |

这样 Debug 版和 Release 版共享同一份数据，打包也不会丢失。

### JSON 格式兼容

usage.json 的字段名统一使用 **snake_case**（和 Python 版一致）：

- `total_seconds`、`started_at`、`ended_at`、`process_name`、`window_title`
- C# 内部数据类通过 `[JsonPropertyName]` 属性映射到 PascalCase 属性名
- config.json 直接以 `Dictionary<string, object>` 读写，同样使用 snake_case key
- 支持的配置字段：`daily_limit_minutes`、`mode`（limit/track）、`theme`（light/dark/system）、`remind_at_percent`、`over_limit_reminder_minutes`、`poll_interval_seconds`、`client_process_names`、`browser_process_names`、`title_keywords`、`force_close_enabled`、`rest_reminder_minutes`

**注意：** `ReadData()` 反序列化时不传 `JsonOptions`，默认大小写敏感。因此 **每个属性都必须加 `[JsonPropertyName]`**，否则 JSON 小写 key 匹配不到 C# PascalCase 属性，数据会丢失。

## 已知问题和注意事项

- `.NET` 的 `Process.ProcessName` 不带 `.exe` 后缀（返回 `douyin`，不是 `douyin.exe`），和 Python 的 `psutil` 不同。匹配时做了兼容处理。
- **模式切换后已超过额度不杀进程**：如果用户先在统计模式刷超了额度，再切到限制模式，`_reminderLimitSent` 已经是 `true`，首次 Kill 分支被跳过。但超限重复提醒分支（每 5 分钟）仍会执行 Kill，所以最多等 5 分钟。如需立即 Kill，需要重启程序。
- `DateTime.Parse` 在 `CheckReminders` 中已改为 `TryParse`，防止 usage.json 手动编辑后格式损坏导致 Timer 静默停止。
- `UseWindowsForms` 会和 WPF 产生类型名冲突（`Application`、`Button` 等），代码中用完全限定名解决。
- WPF 的 `Button` 不支持直接设置 `CornerRadius`，需要用自定义 `ControlTemplate`（见 App.xaml 中的 `RoundedButton` 样式）。
- ComboBox 统一使用 `ThemedComboBox` 样式（定义在 App.xaml），带圆角边框、自定义下拉箭头、主题色弹出背景。还有全局 `ComboBoxItem` 样式，让下拉列表项适配亮/暗主题。
- 滚动条有全局主题样式（App.xaml）：窄细圆角（8px），轨道用 `SurfaceAltBrush`，滑块用 `Muted2Brush`，适配亮/暗主题，垂直和水平方向均生效。
- `TrayService.LoadIcon()` 统一加载图标：优先 `Icon.ExtractAssociatedIcon(exe)`（单文件打包兼容），回退加载独立 .ico 文件。
- 侧边栏按钮图标用 `Path` 绘制。总览、数据、关于的图标有 `z`（闭合路径），用 `Fill` 填充正常。设置的图标是线段（滑块图案），必须用 `Stroke` 描边才能显示。全部统一用 `Stroke` 了。
- `System.Timers.Timer` 运行在 ThreadPool 线程，Elapsed 事件中的 `GetForegroundWindow()` 调用是线程安全的。
- **单文件打包不能加 PublishReadyToRun**，会导致启动时 `DllNotFoundException` 崩溃。

## 开发和运行

### 编译

项目根目录的 `build.ps1` 脚本会自动关闭旧进程再编译，推荐使用：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

手动编译（需先退出所有 DooTimer 进程）：

```powershell
cd DooTimer-cs
dotnet build
```

### 运行（调试版）

```powershell
# config.json 和 usage.json 都在 %AppData%/DooTimer/ 下，无需额外准备
.\bin\Debug\net8.0-windows\win-x64\DooTimer.exe
```

### 打包（单文件 exe）

```powershell
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true /p:SelfContained=true -o publish
# 输出在 publish\DooTimer.exe
# 约 162 MB（包含完整 .NET 运行时，不需要安装任何东西）
# 注意：不要加 PublishReadyToRun=true，会导致启动时 DllNotFoundException 崩溃
```

### 安装 .NET SDK

```powershell
winget install Microsoft.DotNet.SDK.8
```

### 运行测试

```powershell
dotnet test DooTimer-cs/DooTimer.Tests/DooTimer.Tests.csproj
```

测试框架：xUnit。测试覆盖：
- `FormatHelper` — 格式化工具（13 条）
- `ConfigService` — 配置加载/保存/校验（8 条）
- `UsageStorage` — 数据读写/会话存储/提醒（10 条）

### CI/CD（GitHub Actions）

推送代码到 GitHub 后自动触发：

| Workflow | 触发条件 | 行为 |
|----------|---------|------|
| `build.yml` | push / PR 到 main | 编译 + 运行测试 |
| `release.yml` | 推送 `v*` tag | 编译 + 打包单文件 exe + 发布 Release |

**注意**：主项目 `DooTimer.csproj` 中需要排除测试目录（`<Compile Remove="DooTimer.Tests\**" />`），否则 WPF 临时编译机制会把测试文件当成主项目代码导致编译失败。
