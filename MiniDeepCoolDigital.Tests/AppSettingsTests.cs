using MiniDeepCoolDigital.Configuration;
using MiniDeepCoolDigital.Display;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniDeepCoolDigital.Tests;

[TestClass]
public sealed class AppSettingsTests
{
  [TestMethod]
  public void Validated_ResetsUnsupportedUpdateInterval()
  {
    Assert.AreEqual(5, new AppSettings().UpdateSeconds);
    Assert.AreEqual(5, new AppSettings { UpdateSeconds = 7 }.Validated().UpdateSeconds);
  }

  [TestMethod]
  public void Validated_KeepsSupportedUpdateIntervals()
  {
    foreach (var seconds in new[] { 1, 3, 5, 10 })
      Assert.AreEqual(seconds, new AppSettings { UpdateSeconds = seconds }.Validated().UpdateSeconds);
  }

  [TestMethod]
  public void Validated_ResetsUnsupportedMode()
  {
    Assert.AreEqual(DisplayMode.Cpu, new AppSettings { Mode = (DisplayMode)99 }.Validated().Mode);
  }
}
