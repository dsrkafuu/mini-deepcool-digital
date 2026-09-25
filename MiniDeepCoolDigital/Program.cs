using MiniDeepCoolDigital.Tray;

namespace MiniDeepCoolDigital;

internal static class Program
{
  [STAThread]
  private static void Main()
  {
    using var instance = new Mutex(true, "Local\\MiniDeepCoolDigital", out var firstInstance);
    if (!firstInstance) return;
    ApplicationConfiguration.Initialize();
    try { Application.Run(new TrayAppContext()); }
    catch (Exception error)
    {
      MessageBox.Show($"启动失败：{error.Message}", "mini-deepcool-digital", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }
}
