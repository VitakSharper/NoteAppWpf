using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace NoteApp.Views;

// A shortcut that works from any application (RegisterHotKey), delivered to the main window
// as WM_HOTKEY — its handle lives on while the window is hidden in the notification area.
// Registration fails when another program (or a second NoteApp) holds the same keys.
public sealed class GlobalHotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const int Id = 0x4E41; // "NA"

    private readonly HwndSource _source;
    private readonly IntPtr _handle;
    private readonly Action _pressed;

    public bool IsRegistered { get; }

    public GlobalHotKey(Window window, ModifierKeys modifiers, Key key, Action pressed)
    {
        _pressed = pressed;
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(Hook);

        var mods = ModNoRepeat
            | (modifiers.HasFlag(ModifierKeys.Control) ? ModControl : 0)
            | (modifiers.HasFlag(ModifierKeys.Alt) ? ModAlt : 0)
            | (modifiers.HasFlag(ModifierKeys.Shift) ? ModShift : 0);
        IsRegistered = RegisterHotKey(_handle, Id, mods, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == Id)
        {
            handled = true;
            _pressed();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (IsRegistered)
            UnregisterHotKey(_handle, Id);
        _source.RemoveHook(Hook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
