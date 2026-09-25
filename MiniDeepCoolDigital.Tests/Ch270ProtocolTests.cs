using System.Buffers.Binary;
using MiniDeepCoolDigital.Devices;
using MiniDeepCoolDigital.Display;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniDeepCoolDigital.Tests;

[TestClass]
public sealed class Ch270ProtocolTests
{
  [TestMethod]
  [DataRow(0, (byte)2, 7, 10, 14, 15)]
  [DataRow(1, (byte)4, 19, 21, 25, 26)]
  public void Encode_WritesFourMetricsAndChecksum(
    int modeValue, byte page, int powerOffset, int temperatureOffset, int usageOffset, int frequencyOffset)
  {
    var mode = (DisplayMode)modeValue;
    var report = Ch270Protocol.Encode(new DisplaySample(mode, 42.5f, 88, 73, 4510));

    Assert.AreEqual(64, report.Length);
    Assert.AreEqual((byte)16, report[0]);
    Assert.AreEqual(page, report[6]);
    Assert.AreEqual((ushort)88, BinaryPrimitives.ReadUInt16BigEndian(report.AsSpan(powerOffset, 2)));
    Assert.AreEqual(42.5f, BinaryPrimitives.ReadSingleBigEndian(report.AsSpan(temperatureOffset, 4)));
    Assert.AreEqual((byte)73, report[usageOffset]);
    Assert.AreEqual((ushort)4510, BinaryPrimitives.ReadUInt16BigEndian(report.AsSpan(frequencyOffset, 2)));
    Assert.AreEqual(report.AsSpan(1, 39).ToArray().Aggregate((byte)0, (sum, next) => unchecked((byte)(sum + next))), report[40]);
    Assert.AreEqual((byte)22, report[41]);
  }

  [TestMethod]
  [DataRow(float.NaN)]
  [DataRow(-1f)]
  [DataRow(151f)]
  public void Encode_RejectsInvalidTemperature(float temperature)
  {
    Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
      Ch270Protocol.Encode(new DisplaySample(DisplayMode.Cpu, temperature, 1, 1, 1)));
  }
}
