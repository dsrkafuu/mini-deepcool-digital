# mini-deepcool-digital 技术方案

> 2026-09-25 重新制定。目标平台：Windows 10/11 x64；首个实测设备：CH270 DIGITAL。基础实现与实机验证范围见[已归档的更新记录](archive/update-260925.md)。

## 1. 目标与范围

实现一个常驻托盘的轻量数显程序，替代九州风神官方软件的常用数据显示功能。首版只有两种统一模式：

| 模式 | 显示的四项指标 |
| --- | --- |
| CPU | 温度、功耗、使用率、频率 |
| GPU | 温度、功耗、使用率、频率 |

CH270 DIGITAL 第四块固定显示当前模式的频率。没有 PSU 模式、风扇转速页、单独的第四块轮播或模式自动轮播。托盘可开启/关闭数显，并选择 **1、3、5、10 秒**更新频率，默认 5 秒。每个周期采集一次当前模式的数据并写入一次设备，不设另一套刷新计时。

首版只承诺 CH270 DIGITAL。维护者目前只有这台设备；其他型号即使后续完成协议适配，没有实机证据前也必须标为“未实机验证”。程序不控制风扇、RGB、固件或散热安全策略。

## 2. 技术栈

- **应用**：单个 .NET 10 Windows 进程，以仓库 `global.json` 固定的 SDK 构建；目标框架 `net10.0-windows`，当前以 Release 构建目录交付。
- **托盘**：Windows Forms `ApplicationContext` 和 `NotifyIcon` 管理生命周期与通知图标；右键菜单通过 Win32 弹出菜单 API 绘制，使用 Windows 系统样式。常驻时没有可见主窗口；诊断窗口按需打开。菜单事件仅更新状态并通知后台工作者，不等待硬件 I/O。
- **硬件数据**：进程内直接调用 `LibreHardwareMonitorLib`，首版固定 NuGet 稳定版 `0.9.6`。不启动其图形程序，也不依赖 RTSS 运行。用户现有 RTSS Overlay 的 Internal HAL 仅作本机数值对照。
- **设备通信**：使用 `HidSharp` `2.6.4` 枚举和访问 Windows HID，CH270 报文在本仓库独立编码。报告 ID、长度和写入方式以 CH270 Windows 实机探针为准。
- **构建**：运行 `dotnet build -c Release`，直接使用 `MiniDeepCoolDigital/bin/Release/net10.0-windows/` 中的产物，不另行执行 `publish` 或打 ZIP。测量整个进程的 CPU 与私有内存，不假定 .NET 必然比官方软件占用更少。

不再保留 Rust、跨进程传感器辅助程序或两套运行时。

## 3. 代码结构与工作者

应用项目和测试项目分别放在根目录下的同级文件夹。根目录保留解决方案、SDK 配置、文档等通用文件；测试使用 MSTest，由 `dotnet test` 运行：

```text
MiniDeepCoolDigital.slnx
global.json
docs/
MiniDeepCoolDigital/
  MiniDeepCoolDigital.csproj
  Program.cs          启动与单实例
  Tray/               菜单与 UI 线程调度
  Configuration/      当前用户配置与保存
  Sensors/            LibreHardwareMonitor 初始化、采样和映射
  Devices/            HID 枚举、CH270 协议与连接
  Display/            CPU/GPU 通用样本、校验、定时工作者
MiniDeepCoolDigital.Tests/
  MiniDeepCoolDigital.Tests.csproj
```

核心边界为 `ISensorProvider.Read(DisplayMode)` 和 `IDisplayDevice.Write(DisplaySample)`。设备适配器只处理设备识别及 CPU/GPU 四项的编码写入，不增加设备专属菜单层或第四块策略。样本保存模式、四个数值、采样时间和传感器标识；必需项缺失、过期、非有限数或越界时整帧不可写入，不能补零。

同一设备只有一个工作者顺序执行“采样 → 映射 → 校验 → 编码 → HID 写入”。模式、频率或数显开关变化时取消旧等待、按新配置立即处理；禁止异步定时回调重叠及旧模式帧覆盖新选择。

## 4. 传感器来源与映射

`LibreHardwareMonitorLib` 的 `Computer` 只启用 CPU、GPU 相关硬件组，启动时打开一次，退出时关闭。每周期更新相关硬件及其子硬件，从同一次采样组成四项。选择传感器时核对硬件类型、传感器类型、名称、单位与稳定标识，不依赖列表顺序。

| 指标 | 首选语义 | 不可混淆的值 |
| --- | --- | --- |
| CPU 温度 | CPU Package 温度 | 主板温度、单核心温度 |
| CPU 功耗 | CPU Package 功耗，W | 整机功耗、功耗百分比 |
| CPU 使用率 | CPU Total 负载，0–100% | 单核心负载 |
| CPU 频率 | 可代表整颗 CPU 的时钟，MHz | 总线频率、未经说明的单核峰值 |
| GPU 温度 | 所选 GPU 的核心温度 | 热点或显存温度 |
| GPU 功耗 | 所选 GPU 的板卡/芯片功耗，W | 功耗百分比；实际口径需记录 |
| GPU 使用率 | 所选 GPU 的核心负载 | 显存占用、编解码引擎负载 |
| GPU 频率 | 所选 GPU 的核心时钟，MHz | 显存时钟 |

CPU 频率的聚合口径、不同 CPU/GPU 的传感器名称先通过只读探针核实，再写成可解释的选择规则。多 GPU 时默认优先独显；若有歧义，诊断列出候选并允许保存明确选择，不混用不同 GPU 的数值。

部分传感器可能需要管理员权限，或受硬件和驱动限制。LibreHardwareMonitor 的底层 CPU 读数依赖 PawnIO，安装地址见 [PawnIO 官网](https://pawnio.eu/)。本机安装 PawnIO 2.2.0.0 后，普通权限进程读取 9800X3D 的温度、功耗和频率仍为零；管理员权限下四项均已读到，并成功发送一次 CPU HID 帧。应用清单请求管理员权限；缺失时托盘说明指标、PawnIO 状态和进程权限，该模式停止写入。登录启动由当前用户的交互式最高权限计划任务负责；运行时无需额外服务或辅助进程。

## 5. CH270 适配与上游关系

[deepcool-digital-linux](https://github.com/Nortank12/deepcool-digital-linux) 的[设备表](https://github.com/Nortank12/deepcool-digital-linux/blob/main/device-list/README.md)和[CH 第二代映射](https://github.com/Nortank12/deepcool-digital-linux/blob/main/device-list/tables/ch-series-gen2.md)是协议研究线索。资料中的 CH270 DIGITAL VID/PID 为 `0x3633/0x0016`；最终以本机枚举的 HID 接口、usage、设备路径和实际报文响应为准。

适配器依次验证：正确 HID 接口与设备实例、CPU 频率页和 GPU 页的字段/单位/字节序/校验、报告 ID 与写入方式。设备占用、拔插及睡眠恢复时关闭旧句柄并退避重连，不强制结束官方软件。关闭持续更新后停止周期采样与写入；本机 CH270 等待片刻会自行熄屏。重新开启时立即发送一帧。

上游仓库只作为可追溯资料，不复制整个项目，不引入 submodule；Windows 实现独立编写，记录参考的上游提交与本机验证结果。若日后复制或改写其 GPL-3.0 代码，发布前处理许可证和署名要求。第三方 NuGet 依赖的许可也须核对。

## 6. 托盘菜单与配置

```text
状态：CH270 DIGITAL · 已连接 / 无设备 / 指标缺失 / 写入失败
设备 → 自动选择 / 已发现设备 / 重新扫描
数字显示 → 开启 / 关闭
显示模式 → CPU / GPU
更新频率 → 1 秒 / 3 秒 / 5 秒 / 10 秒
开机启动
诊断信息…
退出
```

菜单结构对未来设备保持一致，只按设备能力和数据可用性调整条目。配置保存在当前用户目录，包含设备选择、模式、频率和数显开关；原子替换。开机启动状态由实际计划任务判定，不在配置中另存一份。用户点击菜单启用时，创建仅面向当前用户登录的计划任务，使用 `InteractiveToken` 与 `HighestAvailable`，启动同一个托盘进程；关闭时删除任务。旧版同名 Run 项在设置时清理。日志限量滚动、不逐帧记录，诊断隐藏设备序列号和用户路径。

## 7. 生命周期与异常

启动顺序：读取配置 → 创建单实例互斥与托盘 → 初始化传感器 → 枚举 CH270 → 首次有效采样和写入。任一步失败都保留托盘并说明原因。

运行周期按选定的 1/3/5/10 秒串行执行，传感器采集与数显写入同步。设备重连或用户操作可提前触发一帧。采样失败时不发送旧值或伪造零值；设备用低频兜底扫描，不在每秒完整枚举。退出时取消并等待工作者，释放 HID、`Computer` 和托盘资源，保存配置。与 RTSS 同时运行时检查低层传感器驱动冲突，并保留诊断信息。

## 8. 验证和交付顺序

| 阶段 | 交付物 | 完成条件 |
| --- | --- | --- |
| P0 只读探针 | CPU/GPU 硬件及候选传感器清单，权限、单位、空值和采样耗时 | 八项指标在本机的来源与口径明确，可与 RTSS Internal HAL 对照。 |
| P1 最小闭环 | .NET 托盘、配置、LibreHardwareMonitorLib、CH270 适配器及四档更新 | CH270 实机显示两种模式的四项；菜单和开关生效；缺失数据时不写入。 |
| P2 稳定性 | 拔插、睡眠恢复、Explorer 重启、设备占用、诊断和性能测量 | 记录连续运行的空闲 CPU、私有工作集、启动时间，并与官方软件同机比较。 |
| P3 Release 构建 | Release 产物、运行依赖说明和设备兼容标记 | `dotnet build -c Release` 与测试通过；只有 CH270 标为维护者实机验证。 |

协议编码和传感器选择规则用脱敏样本测试；工作者用假传感器和假设备验证不重叠、切换无旧帧、缺失不写入。HID 报文被接受、页面正确和开关生效必须由 CH270 实机测试证明。其他型号的社区反馈与维护者实测分开记录。

## 9. 资料

- [Windows Forms NotifyIcon](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms)
- [LibreHardwareMonitor 项目与集成示例](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- [LibreHardwareMonitorLib 0.9.6](https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.6)
- [HidSharp 2.6.4](https://www.nuget.org/packages/HidSharp/2.6.4)
- [MSTest 与 dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-running-tests)
- [deepcool-digital-linux](https://github.com/Nortank12/deepcool-digital-linux)
