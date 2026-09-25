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

需要 Windows x64 压缩包时，运行 `./package-release.ps1`。脚本会构建 Release，并按项目版本号在 `MiniDeepCoolDigital/bin/` 生成 `MiniDeepCoolDigital-<版本号>-x64.zip`。

## 添加新设备

本项目目前只适配并实机验证了 CH270 DIGITAL。添加其他型号时，可以参考 [deepcool-digital-linux 的设备列表](https://github.com/Nortank12/deepcool-digital-linux/blob/main/device-list/README.md)及其[协议映射表](https://github.com/Nortank12/deepcool-digital-linux/tree/main/device-list/tables)，先确认 VID/PID、报文格式和设备支持的显示模式。上游项目用于协议研究，本项目独立实现 Windows 通信，不直接复制其代码。

1. 参考 `MiniDeepCoolDigital/Devices/Ch270Device.cs` 和 `Ch270Protocol.cs`，为新设备实现识别、连接和报文编码；不要仅凭相同品牌或系列复用 CH270 报文。
2. 在 `MiniDeepCoolDigital/Display/DisplayWorker.cs` 中接入新设备的枚举与写入，并让托盘设备菜单显示正确型号。只有设备确实支持的模式才应发送。
3. 在 `MiniDeepCoolDigital.Tests/` 加入报文字段与校验测试，再用真实设备确认显示内容、模式切换和断线重连。未经实机验证的型号应明确标为“未实机验证”。
