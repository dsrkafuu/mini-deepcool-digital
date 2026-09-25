using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using MiniDeepCoolDigital.Display;
using System.Security.Principal;

namespace MiniDeepCoolDigital.Sensors;

internal sealed record GpuChoice(string Identifier, string Name);

internal sealed class HardwareSensors : IDisposable
{
  private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true };
  private bool _opened;

  public void Open()
  {
    if (_opened) return;
    _computer.Open();
    _opened = true;
  }

  public IReadOnlyList<GpuChoice> Gpus => _computer.Hardware
    .Where(IsGpu)
    .OrderBy(GpuRank)
    .ThenBy(h => h.Identifier.ToString(), StringComparer.Ordinal)
    .Select(h => new GpuChoice(h.Identifier.ToString(), h.Name))
    .ToArray();

  public DisplaySample Read(DisplayMode mode, string? gpuIdentifier)
  {
    Open();
    var hardware = mode == DisplayMode.Cpu
      ? _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)
      : SelectGpu(gpuIdentifier);
    if (hardware is null) throw new InvalidOperationException(mode == DisplayMode.Cpu ? "未发现 CPU" : "未发现所选 GPU");

    UpdateTree(hardware);
    var sensors = EnumerateSensors(hardware)
      .Where(s => s.Value is float value && float.IsFinite(value))
      .ToArray();

    if (mode == DisplayMode.Cpu)
    {
      try
      {
        return new DisplaySample(mode,
          Require(sensors, SensorType.Temperature, "CPU 温度", "CPU Package", "CPU (Tctl/Tdie)", "Core (Tctl/Tdie)"),
          Require(sensors, SensorType.Power, "CPU 功耗", "CPU Package", "Package"),
          Require(sensors, SensorType.Load, "CPU 使用率", "CPU Total"),
          CpuClock(sensors));
      }
      catch (InvalidOperationException error)
      {
        var hint = CpuAccessHint();
        if (hint is null) throw;
        throw new InvalidOperationException($"{error.Message}；{hint}", error);
      }
    }

    return new DisplaySample(mode,
      Require(sensors, SensorType.Temperature, "GPU 温度", "GPU Core", "GPU Temperature"),
      Require(sensors, SensorType.Power, "GPU 功耗", "GPU Power", "GPU Board Power", "GPU Package"),
      Require(sensors, SensorType.Load, "GPU 使用率", "GPU Core", "GPU Load"),
      Require(sensors, SensorType.Clock, "GPU 频率", "GPU Core", "GPU Clock"));
  }

  public string Describe()
  {
    Open();
    var lines = new List<string>
    {
      PawnIo.IsInstalled ? $"PawnIO：已安装 {PawnIo.Version}" : "PawnIO：未安装",
      IsElevated() ? "权限：管理员" : "权限：普通用户"
    };
    foreach (var hardware in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu || IsGpu(h)))
    {
      UpdateTree(hardware);
      lines.Add($"{hardware.HardwareType}：{hardware.Name} · {hardware.Identifier}");
      foreach (var sensor in EnumerateSensors(hardware))
        lines.Add($"  {sensor.SensorType}: {sensor.Name} = {sensor.Value?.ToString("0.##") ?? "不可用"}");
    }
    return string.Join(Environment.NewLine, lines);
  }

  private static string? CpuAccessHint()
  {
    if (!PawnIo.IsInstalled) return "未安装 PawnIO";
    if (!IsElevated()) return "需要管理员权限";
    return null;
  }

  private static bool IsElevated()
  {
    using var identity = WindowsIdentity.GetCurrent();
    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
  }

  private IHardware? SelectGpu(string? identifier)
  {
    var candidates = _computer.Hardware.Where(IsGpu)
      .OrderBy(GpuRank)
      .ThenBy(h => h.Identifier.ToString(), StringComparer.Ordinal);
    return identifier is null
      ? candidates.FirstOrDefault()
      : candidates.FirstOrDefault(h => h.Identifier.ToString() == identifier);
  }

  private static bool IsGpu(IHardware hardware) => hardware.HardwareType is
    HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

  private static int GpuRank(IHardware hardware) => hardware.HardwareType switch
  {
    HardwareType.GpuNvidia => 0,
    HardwareType.GpuAmd when hardware.Name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) => 2,
    HardwareType.GpuAmd => 1,
    HardwareType.GpuIntel => 3,
    _ => 4
  };

  private static void UpdateTree(IHardware hardware)
  {
    hardware.Update();
    foreach (var sensor in hardware.Sensors)
      if (sensor.ValuesTimeWindow != TimeSpan.Zero)
        sensor.ValuesTimeWindow = TimeSpan.Zero;
    foreach (var child in hardware.SubHardware) UpdateTree(child);
  }

  private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
  {
    foreach (var sensor in hardware.Sensors) yield return sensor;
    foreach (var child in hardware.SubHardware)
      foreach (var sensor in EnumerateSensors(child)) yield return sensor;
  }

  private static float Require(ISensor[] sensors, SensorType type, string label, params string[] names)
  {
    foreach (var name in names)
    {
      var sensor = sensors.FirstOrDefault(s => s.SensorType == type &&
        string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
      if (sensor?.Value is float value && (type == SensorType.Load || value > 0)) return value;
    }
    throw new InvalidOperationException($"{label}不可用");
  }

  private static float CpuClock(ISensor[] sensors)
  {
    foreach (var name in new[] { "CPU Core Average", "Core Average", "Cores (Average)" })
    {
      var match = sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock &&
        string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
      if (match?.Value is float value && value > 0) return value;
    }
    var cores = sensors.Where(s => s.SensorType == SensorType.Clock &&
      (s.Name.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase) ||
       s.Name.StartsWith("Core #", StringComparison.OrdinalIgnoreCase)) &&
      !s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase))
      .Select(s => s.Value!.Value).Where(value => value > 0).ToArray();
    if (cores.Length > 0) return cores.Average();
    throw new InvalidOperationException("缺少 CPU 核心频率传感器");
  }

  public void Dispose()
  {
    if (!_opened) return;
    _computer.Close();
    _opened = false;
  }
}
