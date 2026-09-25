using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MiniDeepCoolDigital.Tray;

internal static class NativeTrayMenu
{
  private const uint MfString = 0x0000;
  private const uint MfGray = 0x0001;
  private const uint MfChecked = 0x0008;
  private const uint MfPopup = 0x0010;
  private const uint MfSeparator = 0x0800;
  private const uint TpmRightButton = 0x0002;
  private const uint TpmNoNotify = 0x0080;
  private const uint TpmReturnCommand = 0x0100;

  public static void Show(IntPtr owner, Point position, ToolStripItemCollection items)
  {
    var root = CreatePopupMenu();
    if (root == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

    ToolStripMenuItem? chosen = null;
    try
    {
      var commands = new Dictionary<uint, ToolStripMenuItem>();
      uint nextId = 1;
      AppendItems(root, items, commands, ref nextId);
      SetForegroundWindow(owner);
      var id = TrackPopupMenuEx(root,
        TpmRightButton | TpmNoNotify | TpmReturnCommand,
        position.X, position.Y, owner, IntPtr.Zero);
      PostMessageW(owner, 0, IntPtr.Zero, IntPtr.Zero);
      commands.TryGetValue(id, out chosen);
    }
    finally
    {
      DestroyMenu(root);
    }

    chosen?.PerformClick();
  }

  private static void AppendItems(IntPtr menu, ToolStripItemCollection items,
    Dictionary<uint, ToolStripMenuItem> commands, ref uint nextId)
  {
    foreach (ToolStripItem item in items)
    {
      if (item is ToolStripSeparator)
      {
        Append(menu, MfSeparator, 0, null);
        continue;
      }
      if (item is not ToolStripMenuItem choice) continue;

      var state = choice.Enabled ? MfString : MfGray;
      if (choice.Checked) state |= MfChecked;
      var title = (choice.Text ?? string.Empty).Replace("&", "&&");
      if (choice.DropDownItems.Count > 0)
      {
        var child = CreatePopupMenu();
        if (child == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
          AppendItems(child, choice.DropDownItems, commands, ref nextId);
          Append(menu, state | MfPopup, (nuint)child, title);
        }
        catch
        {
          DestroyMenu(child);
          throw;
        }
      }
      else
      {
        var id = nextId++;
        commands.Add(id, choice);
        Append(menu, state, id, title);
      }
    }
  }

  private static void Append(IntPtr menu, uint flags, nuint id, string? title)
  {
    if (!AppendMenuW(menu, flags, id, title))
      throw new Win32Exception(Marshal.GetLastWin32Error());
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr CreatePopupMenu();

  [DllImport("user32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool DestroyMenu(IntPtr menu);

  [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool AppendMenuW(IntPtr menu, uint flags, nuint id, string? title);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y,
    IntPtr owner, IntPtr parameters);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetForegroundWindow(IntPtr window);

  [DllImport("user32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
