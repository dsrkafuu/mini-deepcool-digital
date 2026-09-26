using System.Xml.Linq;
using MiniDeepCoolDigital.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniDeepCoolDigital.Tests;

[TestClass]
public sealed class StartupTaskTests
{
  [TestMethod]
  public void TaskRunsForInteractiveUserAtHighestAvailablePrivilege()
  {
    var executable = Path.Combine(Path.GetTempPath(), "Mini DeepCool", "MiniDeepCoolDigital.exe");
    var task = StartupTask.BuildXml(executable, "S-1-5-21-123");
    var ns = (XNamespace)"http://schemas.microsoft.com/windows/2004/02/mit/task";

    Assert.AreEqual("S-1-5-21-123", task.Descendants(ns + "LogonTrigger").Elements(ns + "UserId").Single().Value);
    Assert.AreEqual("InteractiveToken", task.Descendants(ns + "LogonType").Single().Value);
    Assert.AreEqual("HighestAvailable", task.Descendants(ns + "RunLevel").Single().Value);
    Assert.AreEqual("IgnoreNew", task.Descendants(ns + "MultipleInstancesPolicy").Single().Value);
    Assert.AreEqual("PT0S", task.Descendants(ns + "ExecutionTimeLimit").Single().Value);
    Assert.AreEqual(Path.GetFullPath(executable), task.Descendants(ns + "Command").Single().Value);
  }

  [TestMethod]
  public void TaskXmlUsesUtf16AndPreservesChineseDescription()
  {
    var filePath = Path.Combine(Path.GetTempPath(), $"MiniDeepCoolDigital-{Guid.NewGuid():N}.xml");
    try
    {
      StartupTask.WriteXml(filePath, StartupTask.BuildXml(@"C:\MiniDeepCoolDigital.exe", "S-1-5-21-123"));

      var bytes = File.ReadAllBytes(filePath);
      Assert.IsTrue(bytes.Length >= 2);
      Assert.AreEqual(0xFF, bytes[0]);
      Assert.AreEqual(0xFE, bytes[1]);

      var xml = File.ReadAllText(filePath);
      StringAssert.Contains(xml, "encoding=\"utf-16\"");
      var ns = (XNamespace)"http://schemas.microsoft.com/windows/2004/02/mit/task";
      Assert.AreEqual("自动启动 MiniDeepCoolDigital 托盘数显软件。",
        XDocument.Load(filePath).Descendants(ns + "Description").Single().Value);
    }
    finally
    {
      if (File.Exists(filePath)) File.Delete(filePath);
    }
  }
}
