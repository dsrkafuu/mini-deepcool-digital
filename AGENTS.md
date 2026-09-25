# 项目约定

- 日常构建只需直接构建 Debug 或 Release，成功且产物出现即可，不移动产物、不另行发布
- 用户明确要求生产打包时，运行仓库根目录的 `package-release.ps1`：构建 Windows x64 Release，并在应用项目的 `bin/` 中生成带项目版本号的 ZIP；不使用 `dotnet publish`
- 为验证本项目的 CPU 传感器数据，可以按需以管理员权限启动本仓库构建的程序或测试，并读取诊断结果
