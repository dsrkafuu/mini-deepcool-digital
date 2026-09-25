using System.Text.Json;
using System.Text.Json.Serialization;
using MiniDeepCoolDigital.Display;

namespace MiniDeepCoolDigital.Configuration;

internal sealed record AppSettings
{
  public DisplayMode Mode { get; init; } = DisplayMode.Cpu;
  public int UpdateSeconds { get; init; } = 1;
  public bool DisplayEnabled { get; init; } = true;
  public string? DevicePath { get; init; }
  public string? GpuIdentifier { get; init; }
  public bool StartWithWindows { get; init; }

  public AppSettings Validated() => this with
  {
    Mode = Enum.IsDefined(Mode) ? Mode : DisplayMode.Cpu,
    UpdateSeconds = UpdateSeconds is 1 or 3 or 5 or 10 ? UpdateSeconds : 1
  };
}

internal static class SettingsStore
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter<DisplayMode>() }
  };

  public static string FilePath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MiniDeepCoolDigital", "config.json");

  public static AppSettings Load(out string? warning)
  {
    warning = null;
    if (!File.Exists(FilePath)) return new AppSettings();
    try
    {
      return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions)
        ?? new AppSettings()).Validated();
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
    {
      warning = $"配置读取失败，已使用默认值：{error.Message}";
      return new AppSettings();
    }
  }

  public static void Save(AppSettings settings)
  {
    var path = FilePath;
    var directory = Path.GetDirectoryName(path)!;
    Directory.CreateDirectory(directory);
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Validated(), JsonOptions));
      if (File.Exists(path)) File.Replace(temporary, path, null);
      else File.Move(temporary, path);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }
}
