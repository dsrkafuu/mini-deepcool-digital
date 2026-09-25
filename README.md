# mini-deepcool-digital

面向 Windows 的 CH270 DIGITAL 托盘数显程序。目前提供 CPU、GPU 两种显示模式，均显示温度、功耗、使用率、频率；采样与设备更新频率可选 1、3、5、10 秒，默认 5 秒。

程序图标提取自本机安装的九州风神软件，生成包含 16–256 像素帧的 ICO，供 EXE、Windows 快捷方式和托盘使用。

## 运行依赖

- Windows 10/11 与 .NET 10 Windows Desktop Runtime。
- **PawnIO 系统驱动**：程序通过 `LibreHardwareMonitorLib` 读取硬件数据。CPU 温度、功耗和频率等底层传感器需要 PawnIO；只安装 NuGet 包并不能代替驱动。请从 [PawnIO 官网](https://pawnio.eu/) 获取，亦可查看其[官方发布仓库](https://github.com/namazso/PawnIO.Setup/releases)。本程序不内置或自动安装驱动。
- CH270 DIGITAL 数显设备。维护者目前只有这台设备，其他型号未实机验证。

PawnIO 安装后，本机 9800X3D 的部分 CPU 传感器仍需**以管理员权限运行程序**才能读取。程序启动时请求管理员权限；托盘的“诊断信息…”会显示 PawnIO 版本、进程权限和原始传感器数值。如果 CPU 温度、功耗或频率仍为零，程序会停止写入该模式，避免把零值当成真实读数。

托盘“开机启动”会为当前用户创建登录时运行的 Windows 计划任务，以交互会话和最高可用权限启动程序。此选项默认关闭，点击菜单可启用或关闭；设置时会清理旧版同名 Run 启动项。关闭“持续更新数显”后，程序停止采样和写入，本机 CH270 DIGITAL 等待片刻会自动熄屏。

## 开发

使用仓库 `global.json` 指定的 .NET SDK，从根目录运行：

```powershell
dotnet build
dotnet test
dotnet run --project MiniDeepCoolDigital/MiniDeepCoolDigital.csproj
```

运行命令请在管理员终端执行，或直接以管理员身份启动构建产物；应用清单会在直接启动时请求提权。

根目录的 `MiniDeepCoolDigital.slnx` 包含应用与 MSTest 测试项目。技术细节和当前验证范围见[技术方案](docs/technical-plan.md)及[已归档的实现记录](docs/archive/update-260925.md)。

## Release 构建

从仓库根目录运行 `dotnet build -c Release`。构建产物位于 `MiniDeepCoolDigital/bin/Release/net10.0-windows/`，运行其中的 `MiniDeepCoolDigital.exe`。程序仍需 .NET 10 Windows Desktop Runtime 与 PawnIO；CPU 传感器需要管理员权限。首次启动默认每 5 秒更新，已有配置沿用原值。目前仅 CH270 DIGITAL 经过维护者实机验证。

协议资料参考 [deepcool-digital-linux](https://github.com/Nortank12/deepcool-digital-linux)；本仓库独立实现 Windows 版本，没有复制上游源码。
