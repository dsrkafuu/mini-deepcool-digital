using System.Buffers.Binary;
using MiniDeepCoolDigital.Display;

namespace MiniDeepCoolDigital.Devices;

internal static class Ch270Protocol
{
  public const int VendorId = 0x3633;
  public const int ProductId = 0x0016;
  public const int ReportLength = 64;

  // CH second-generation mapping. Windows HID acceptance still needs CH270 verification.
  public static byte[] Encode(DisplaySample sample)
  {
    if (!float.IsFinite(sample.TemperatureC) || sample.TemperatureC is < 0 or > 150 ||
        !float.IsFinite(sample.PowerW) || sample.PowerW is < 0 or > ushort.MaxValue ||
        !float.IsFinite(sample.UsagePercent) || sample.UsagePercent is < 0 or > 100 ||
        !float.IsFinite(sample.FrequencyMhz) || sample.FrequencyMhz is < 0 or > ushort.MaxValue)
      throw new ArgumentOutOfRangeException(nameof(sample), "传感器数值超出设备范围");

    var report = new byte[ReportLength];
    new byte[] { 16, 104, 1, 6, 35, 1 }.CopyTo(report, 0);
    var power = checked((ushort)Math.Round(sample.PowerW, MidpointRounding.AwayFromZero));
    var usage = checked((byte)Math.Round(sample.UsagePercent, MidpointRounding.AwayFromZero));
    var clock = checked((ushort)Math.Round(sample.FrequencyMhz, MidpointRounding.AwayFromZero));
    if (sample.Mode == DisplayMode.Cpu)
    {
      report[6] = 2;
      BinaryPrimitives.WriteUInt16BigEndian(report.AsSpan(7, 2), power);
      BinaryPrimitives.WriteSingleBigEndian(report.AsSpan(10, 4), sample.TemperatureC);
      report[14] = usage;
      BinaryPrimitives.WriteUInt16BigEndian(report.AsSpan(15, 2), clock);
    }
    else
    {
      report[6] = 4;
      BinaryPrimitives.WriteUInt16BigEndian(report.AsSpan(19, 2), power);
      BinaryPrimitives.WriteSingleBigEndian(report.AsSpan(21, 4), sample.TemperatureC);
      report[25] = usage;
      BinaryPrimitives.WriteUInt16BigEndian(report.AsSpan(26, 2), clock);
    }
    for (var index = 1; index <= 39; index++) report[40] += report[index];
    report[41] = 22;
    return report;
  }
}
