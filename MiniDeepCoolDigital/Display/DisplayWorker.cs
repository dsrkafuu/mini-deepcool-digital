using System.Threading.Channels;
using MiniDeepCoolDigital.Configuration;
using MiniDeepCoolDigital.Devices;
using MiniDeepCoolDigital.Sensors;

namespace MiniDeepCoolDigital.Display;

internal sealed class DisplayWorker
{
  private readonly Channel<WorkerCommand> _commands = Channel.CreateUnbounded<WorkerCommand>(
    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
  private readonly CancellationTokenSource _stop = new();
  private Task? _runTask;
  private AppSettings _settings;
  private string? _lastStatus;

  public event Action<string>? StatusChanged;
  public event Action<IReadOnlyList<DeviceChoice>, IReadOnlyList<GpuChoice>>? ChoicesChanged;

  public DisplayWorker(AppSettings initial)
  {
    _settings = initial.Validated();
  }

  public void Start() => _runTask = Task.Run(RunAsync);

  public void Update(AppSettings settings) => _commands.Writer.TryWrite(new UpdateCommand(settings.Validated()));
  public void Rescan() => _commands.Writer.TryWrite(new RescanCommand());

  public Task<string> DescribeAsync()
  {
    var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    if (!_commands.Writer.TryWrite(new DescribeCommand(result))) result.SetResult("工作者已停止");
    return result.Task;
  }

  public async Task StopAsync()
  {
    _stop.Cancel();
    _commands.Writer.TryComplete();
    if (_runTask is not null) await _runTask;
    _stop.Dispose();
  }

  private async Task RunAsync()
  {
    using var sensors = new HardwareSensors();
    Ch270Device? device = null;
    string? openedPath = null;
    IReadOnlyList<DeviceChoice> devices = [];
    var lastScan = DateTimeOffset.MinValue;
    var forceScan = true;
    try
    {
      while (!_stop.IsCancellationRequested)
      {
        if (forceScan || DateTimeOffset.UtcNow - lastScan >= TimeSpan.FromSeconds(10))
        {
          try
          {
            sensors.Open();
            devices = Ch270Device.Enumerate();
            lastScan = DateTimeOffset.UtcNow;
            forceScan = false;
            ChoicesChanged?.Invoke(devices, sensors.Gpus);
          }
          catch (Exception error)
          {
            Publish($"设备或传感器初始化失败：{error.Message}");
            devices = [];
          }
        }

        var selected = _settings.DevicePath is null
          ? devices.FirstOrDefault()
          : devices.FirstOrDefault(candidate => candidate.Path == _settings.DevicePath);
        if (selected?.Path != openedPath)
        {
          device?.Dispose();
          device = null;
          openedPath = null;
        }

        if (selected is null) Publish("未发现 CH270 DIGITAL（或所选设备已断开）");
        else if (!_settings.DisplayEnabled)
        {
          device?.Dispose();
          device = null;
          openedPath = null;
          Publish("已暂停更新（硬件熄屏命令待实机验证）");
        }
        else
        {
          try
          {
            var sample = sensors.Read(_settings.Mode, _settings.GpuIdentifier);
            ChoicesChanged?.Invoke(devices, sensors.Gpus);
            var report = Ch270Protocol.Encode(sample);
            if (device is null)
            {
              device = Ch270Device.Open(selected.Path);
              openedPath = selected.Path;
            }
            device.Write(report);
            Publish($"CH270 · {_settings.Mode.ToString().ToUpperInvariant()} · {_settings.UpdateSeconds} 秒更新");
          }
          catch (Exception error)
          {
            device?.Dispose();
            device = null;
            openedPath = null;
            Publish($"更新失败：{error.Message}");
          }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.UpdateSeconds));
        WorkerCommand command;
        try { command = await _commands.Reader.ReadAsync(timeout.Token); }
        catch (OperationCanceledException) { continue; }
        if (command is UpdateCommand update) _settings = update.Settings;
        if (command is RescanCommand) forceScan = true;
        if (command is DescribeCommand describe)
        {
          try { describe.Result.TrySetResult(sensors.Describe()); }
          catch (Exception error) { describe.Result.TrySetResult($"传感器诊断失败：{error.Message}"); }
        }
        while (_commands.Reader.TryRead(out var pending))
        {
          if (pending is UpdateCommand latest) _settings = latest.Settings;
          if (pending is RescanCommand) forceScan = true;
          if (pending is DescribeCommand requested)
          {
            try { requested.Result.TrySetResult(sensors.Describe()); }
            catch (Exception error) { requested.Result.TrySetResult($"传感器诊断失败：{error.Message}"); }
          }
        }
      }
    }
    finally
    {
      device?.Dispose();
    }
  }

  private void Publish(string status)
  {
    if (status == _lastStatus) return;
    _lastStatus = status;
    StatusChanged?.Invoke(status);
  }

  private abstract record WorkerCommand;
  private sealed record UpdateCommand(AppSettings Settings) : WorkerCommand;
  private sealed record RescanCommand : WorkerCommand;
  private sealed record DescribeCommand(TaskCompletionSource<string> Result) : WorkerCommand;
}
