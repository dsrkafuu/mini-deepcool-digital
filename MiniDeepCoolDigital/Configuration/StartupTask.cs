using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace MiniDeepCoolDigital.Configuration;

internal static class StartupTask
{
  private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

  public static string Name
  {
    get
    {
      using var identity = WindowsIdentity.GetCurrent();
      return @"\MiniDeepCoolDigital-" + (identity.User?.Value
        ?? throw new InvalidOperationException("无法确定当前用户 SID"));
    }
  }

  public static bool IsEnabled() => Run("/Query", "/TN", Name) == 0;

  public static void Enable(string executablePath)
  {
    RequireAdministrator();
    var path = Path.GetFullPath(executablePath);
    if (!File.Exists(path)) throw new FileNotFoundException("应用程序文件不存在", path);

    using var identity = WindowsIdentity.GetCurrent();
    var userId = identity.User?.Value ?? throw new InvalidOperationException("无法确定当前用户 SID");
    var temporary = Path.Combine(Path.GetTempPath(), $"MiniDeepCoolDigital-{Guid.NewGuid():N}.xml");
    try
    {
      File.WriteAllText(temporary, BuildXml(path, userId).ToString(), new UTF8Encoding(false));
      RunRequired("/Create", "/TN", Name, "/XML", temporary, "/F");
      RemoveLegacyRunEntry();
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }

  public static void Disable()
  {
    RequireAdministrator();
    if (IsEnabled()) RunRequired("/Delete", "/TN", Name, "/F");
    RemoveLegacyRunEntry();
  }

  internal static XDocument BuildXml(string executablePath, string userId)
  {
    var path = Path.GetFullPath(executablePath);
    return new XDocument(new XElement(TaskNamespace + "Task",
      new XAttribute("version", "1.2"),
      new XElement(TaskNamespace + "RegistrationInfo",
        new XElement(TaskNamespace + "Description", "启动 mini-deepcool-digital 托盘数显")),
      new XElement(TaskNamespace + "Triggers",
        new XElement(TaskNamespace + "LogonTrigger",
          new XElement(TaskNamespace + "Enabled", true),
          new XElement(TaskNamespace + "UserId", userId))),
      new XElement(TaskNamespace + "Principals",
        new XElement(TaskNamespace + "Principal",
          new XAttribute("id", "CurrentUser"),
          new XElement(TaskNamespace + "UserId", userId),
          new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
          new XElement(TaskNamespace + "RunLevel", "HighestAvailable"))),
      new XElement(TaskNamespace + "Settings",
        new XElement(TaskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
        new XElement(TaskNamespace + "DisallowStartIfOnBatteries", false),
        new XElement(TaskNamespace + "StopIfGoingOnBatteries", false),
        new XElement(TaskNamespace + "ExecutionTimeLimit", "PT0S")),
      new XElement(TaskNamespace + "Actions",
        new XAttribute("Context", "CurrentUser"),
        new XElement(TaskNamespace + "Exec",
          new XElement(TaskNamespace + "Command", path),
          new XElement(TaskNamespace + "WorkingDirectory", Path.GetDirectoryName(path))))));
  }

  private static void RequireAdministrator()
  {
    using var identity = WindowsIdentity.GetCurrent();
    if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
      throw new InvalidOperationException("请先以管理员身份运行程序，再设置开机启动");
  }

  private static void RemoveLegacyRunEntry()
  {
    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
    key?.DeleteValue("MiniDeepCoolDigital", throwOnMissingValue: false);
  }

  private static void RunRequired(params string[] arguments)
  {
    var exitCode = Run(arguments);
    if (exitCode != 0) throw new InvalidOperationException($"计划任务操作失败，错误码 {exitCode}");
  }

  private static int Run(params string[] arguments)
  {
    var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
    {
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 Windows 任务计划程序工具");
    process.WaitForExit();
    return process.ExitCode;
  }
}
