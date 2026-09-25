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
}
