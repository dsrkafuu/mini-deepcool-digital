# Mini DeepCool Digital

适用于 Windows 的 CH270 DIGITAL 数显托盘程序。无需打开主窗口，即可在机箱数显上查看 CPU 或 GPU 的温度、功耗、使用率和频率。

## 功能

- 在托盘切换 CPU、GPU 显示模式，并选择 GPU 来源
- 选择 1、3、5 秒更新频率，默认 1 秒
- 暂停或恢复数显更新，按需开启开机启动
- 设备断开后自动重连

目前仅在 CH270 DIGITAL 上经过实机验证。

## 运行要求

- Windows 10/11 与 .NET 10 Windows Desktop Runtime
- [PawnIO](https://pawnio.eu/) 驱动，用于读取 CPU 传感器数据
- CH270 DIGITAL 数显设备

安装运行依赖后，启动 `MiniDeepCoolDigital.exe`，按提示授予管理员权限。在系统托盘中右键程序图标，即可选择显示模式、更新频率等选项。暂停更新后，CH270 会在等待片刻后自行熄屏。

## 构建

使用 .NET 10 SDK，在仓库根目录运行：

```powershell
dotnet build -c Release
```

程序位于 `MiniDeepCoolDigital/bin/Release/net10.0-windows/`。实现细节见[技术方案](docs/technical-plan.md)。
