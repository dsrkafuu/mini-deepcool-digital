using Microsoft.Win32;
using MiniDeepCoolDigital.Configuration;
using MiniDeepCoolDigital.Devices;
using MiniDeepCoolDigital.Display;
using MiniDeepCoolDigital.Sensors;

namespace MiniDeepCoolDigital.Tray;

internal sealed class TrayAppContext : ApplicationContext
{
  private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
  private const string RunName = "MiniDeepCoolDigital";
  private readonly Control _dispatcher = new();
  private readonly NotifyIcon _icon;
  private readonly ContextMenuStrip _menu = new();
  private readonly DisplayWorker _worker;
  private readonly ToolStripMenuItem _status = new("正在初始化…") { Enabled = false };
  private readonly ToolStripMenuItem _devices = new("设备");
  private readonly ToolStripMenuItem _gpus = new("GPU 来源");
  private readonly ToolStripMenuItem _display = new("持续更新数显");
  private readonly ToolStripMenuItem _cpu = new("CPU");
  private readonly ToolStripMenuItem _gpu = new("GPU");
  private readonly ToolStripMenuItem _startup = new("开机启动");
  private readonly Dictionary<int, ToolStripMenuItem> _intervals = [];
  private AppSettings _settings;
  private bool _exiting;

  public TrayAppContext()
  {
    _ = _dispatcher.Handle;
    _settings = SettingsStore.Load(out var warning);
    _worker = new DisplayWorker(_settings);
    _worker.StatusChanged += value => Post(() => _status.Text = "状态：" + value);
    _worker.ChoicesChanged += (devices, gpus) => Post(() => UpdateChoices(devices, gpus));

    _menu.Items.Add(_status);
    _menu.Items.Add(new ToolStripSeparator());
    _menu.Items.Add(_devices);
    _menu.Items.Add(_gpus);
    _menu.Items.Add(new ToolStripSeparator());
    _menu.Items.Add(_display);
    _display.Click += (_, _) => Change(_settings with { DisplayEnabled = !_settings.DisplayEnabled });

    var modes = new ToolStripMenuItem("显示模式");
    modes.DropDownItems.AddRange([_cpu, _gpu]);
    _cpu.Click += (_, _) => Change(_settings with { Mode = DisplayMode.Cpu });
    _gpu.Click += (_, _) => Change(_settings with { Mode = DisplayMode.Gpu });
    _menu.Items.Add(modes);

    var frequencies = new ToolStripMenuItem("更新频率");
    foreach (var seconds in new[] { 1, 3, 5, 10 })
    {
      var item = new ToolStripMenuItem($"{seconds} 秒");
      item.Click += (_, _) => Change(_settings with { UpdateSeconds = seconds });
      _intervals.Add(seconds, item);
      frequencies.DropDownItems.Add(item);
    }
    _menu.Items.Add(frequencies);
    _menu.Items.Add(new ToolStripSeparator());
    _startup.Click += (_, _) => ToggleStartup();
    _menu.Items.Add(_startup);
    _menu.Items.Add(new ToolStripMenuItem("诊断信息…", null, async (_, _) => await ShowDiagnosticsAsync()));
    _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitAsync()));
    SyncChecks();

    _icon = new NotifyIcon
    {
      Icon = SystemIcons.Application,
      Text = "mini-deepcool-digital",
      ContextMenuStrip = _menu,
      Visible = true
    };
    if (warning is not null) _status.Text = "状态：" + warning;
    _worker.Start();
    _worker.Rescan();
  }

  private void UpdateChoices(IReadOnlyList<DeviceChoice> devices, IReadOnlyList<GpuChoice> gpus)
  {
    _devices.DropDownItems.Clear();
    var automatic = new ToolStripMenuItem("自动选择") { Checked = _settings.DevicePath is null };
    automatic.Click += (_, _) => Change(_settings with { DevicePath = null });
    _devices.DropDownItems.Add(automatic);
    for (var index = 0; index < devices.Count; index++)
    {
      var device = devices[index];
      var item = new ToolStripMenuItem($"{device.Name} · {index + 1}")
      {
        Checked = _settings.DevicePath == device.Path,
        Tag = device.Path
      };
      item.Click += (_, _) => Change(_settings with { DevicePath = device.Path });
      _devices.DropDownItems.Add(item);
    }
    _devices.DropDownItems.Add(new ToolStripSeparator());
    _devices.DropDownItems.Add(new ToolStripMenuItem("重新扫描", null, (_, _) => _worker.Rescan()));

    _gpus.DropDownItems.Clear();
    var autoGpu = new ToolStripMenuItem("自动选择") { Checked = _settings.GpuIdentifier is null };
    autoGpu.Click += (_, _) => Change(_settings with { GpuIdentifier = null });
    _gpus.DropDownItems.Add(autoGpu);
    foreach (var gpu in gpus)
    {
      var item = new ToolStripMenuItem(gpu.Name)
      {
        Checked = _settings.GpuIdentifier == gpu.Identifier,
        Tag = gpu.Identifier
      };
      item.Click += (_, _) => Change(_settings with { GpuIdentifier = gpu.Identifier });
      _gpus.DropDownItems.Add(item);
    }
  }

  private void Change(AppSettings settings)
  {
    _settings = settings.Validated();
    SyncChecks();
    foreach (var item in _devices.DropDownItems.OfType<ToolStripMenuItem>())
      if (item.Text == "自动选择" || item.Tag is string)
        item.Checked = item.Tag is string path ? path == _settings.DevicePath : _settings.DevicePath is null;
    foreach (var item in _gpus.DropDownItems.OfType<ToolStripMenuItem>())
      item.Checked = item.Tag is string id ? id == _settings.GpuIdentifier : _settings.GpuIdentifier is null;
    try { SettingsStore.Save(_settings); }
    catch (Exception error) { _status.Text = "配置保存失败：" + error.Message; }
    _worker.Update(_settings);
  }

  private void SyncChecks()
  {
    _display.Checked = _settings.DisplayEnabled;
    _cpu.Checked = _settings.Mode == DisplayMode.Cpu;
    _gpu.Checked = _settings.Mode == DisplayMode.Gpu;
    _startup.Checked = _settings.StartWithWindows;
    foreach (var (seconds, item) in _intervals) item.Checked = _settings.UpdateSeconds == seconds;
  }

  private void ToggleStartup()
  {
    try
    {
      using var key = Registry.CurrentUser.CreateSubKey(RunKey);
      if (key is null) throw new InvalidOperationException("无法打开当前用户启动项");
      if (_settings.StartWithWindows) key.DeleteValue(RunName, false);
      else key.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
      Change(_settings with { StartWithWindows = !_settings.StartWithWindows });
    }
    catch (Exception error) { MessageBox.Show(_dispatcher, error.Message, "开机启动设置失败"); }
  }

  private async Task ShowDiagnosticsAsync()
  {
    var details = await _worker.DescribeAsync();
    Post(() => MessageBox.Show(_dispatcher, details, "CPU/GPU 传感器诊断"));
  }

  private async void ExitAsync()
  {
    if (_exiting) return;
    _exiting = true;
    _icon.Visible = false;
    try { await _worker.StopAsync(); }
    finally
    {
      _icon.Dispose();
      _menu.Dispose();
      _dispatcher.Dispose();
      ExitThread();
    }
  }

  private void Post(Action action)
  {
    if (_dispatcher.IsDisposed || !_dispatcher.IsHandleCreated) return;
    try { _dispatcher.BeginInvoke(action); }
    catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException) { /* UI is closing. */ }
  }
}
