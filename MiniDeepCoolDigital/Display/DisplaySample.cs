namespace MiniDeepCoolDigital.Display;

internal enum DisplayMode { Cpu, Gpu }

internal readonly record struct DisplaySample(
  DisplayMode Mode,
  float TemperatureC,
  float PowerW,
  float UsagePercent,
  float FrequencyMhz);
