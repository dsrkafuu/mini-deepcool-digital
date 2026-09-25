# mini-deepcool-digital

面向 Windows 的 CH270 DIGITAL 托盘数显程序。目前提供 CPU、GPU 两种显示模式，均显示温度、功耗、使用率、频率；采样与设备更新频率可选 1、3、5、10 秒。

## 运行依赖

- Windows 10/11 与 .NET 10 Windows Desktop Runtime。
- **PawnIO 系统驱动**：程序通过 `LibreHardwareMonitorLib` 读取硬件数据。CPU 温度、功耗和频率等底层传感器需要 PawnIO；只安装 NuGet 包并不能代替驱动。请从 [PawnIO 官网](https://pawnio.eu/) 获取，亦可查看其[官方发布仓库](https://github.com/namazso/PawnIO.Setup/releases)。本程序不内置或自动安装驱动。
- CH270 DIGITAL 数显设备。维护者目前只有这台设备，其他型号未实机验证。

PawnIO 安装后，某些 CPU 底层传感器仍需**以管理员权限运行程序**才能读取。托盘的“诊断信息…”会显示 PawnIO 版本、进程权限和原始传感器数值；如果 CPU 温度、功耗或频率仍为零，程序会停止写入该模式，避免把零值当成真实读数。当前用户开机启动项不会自动提升权限。

## 开发

使用仓库 `global.json` 指定的 .NET SDK，从根目录运行：

```powershell
dotnet build
dotnet test
dotnet run --project MiniDeepCoolDigital/MiniDeepCoolDigital.csproj
```

根目录的 `MiniDeepCoolDigital.slnx` 包含应用与 MSTest 测试项目。技术细节和当前验证范围见[技术方案](docs/technical-plan.md)及[实现记录](docs/update-260925.md)。

协议资料参考 [deepcool-digital-linux](https://github.com/Nortank12/deepcool-digital-linux)；本仓库独立实现 Windows 版本，没有复制上游源码。
