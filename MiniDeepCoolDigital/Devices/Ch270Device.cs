using HidSharp;

namespace MiniDeepCoolDigital.Devices;

internal sealed record DeviceChoice(string Path, string Name);

internal sealed class Ch270Device : IDisposable
{
  private readonly HidStream _stream;
  private Ch270Device(HidStream stream) => _stream = stream;

  public static IReadOnlyList<DeviceChoice> Enumerate() => DeviceList.Local
    .GetHidDevices(Ch270Protocol.VendorId, Ch270Protocol.ProductId)
    .Where(device => device.GetMaxOutputReportLength() >= Ch270Protocol.ReportLength)
    .Select(device => new DeviceChoice(device.DevicePath, "CH270 DIGITAL"))
    .OrderBy(device => device.Path, StringComparer.Ordinal)
    .ToArray();

  public static Ch270Device Open(string path)
  {
    var device = DeviceList.Local.GetHidDevices(Ch270Protocol.VendorId, Ch270Protocol.ProductId)
      .FirstOrDefault(candidate => candidate.DevicePath == path);
    if (device is null) throw new IOException("CH270 已断开或 HID 接口不可用");
    if (device.GetMaxOutputReportLength() < Ch270Protocol.ReportLength)
      throw new IOException("CH270 HID 输出报告长度不足");
    if (!device.TryOpen(out HidStream stream)) throw new IOException("无法打开 CH270 HID 接口");
    return new Ch270Device(stream);
  }

  public void Write(byte[] report)
  {
    if (report.Length != Ch270Protocol.ReportLength) throw new ArgumentException("报告长度错误", nameof(report));
    _stream.Write(report, 0, report.Length);
  }

  public void Dispose() => _stream.Dispose();
}
