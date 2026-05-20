using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ReminderApp.Helpers;

/// <summary>
/// Системная шапка окна (title bar) в светлом/тёмном режиме через DWM, без кастомного chrome.
/// </summary>
public static class WindowTitleBarThemeHelper
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Включает или выключает тёмный режим нативной шапки. При ошибке API ничего не делает.
    /// </summary>
    public static void Apply(Window window, bool useDark)
    {
        if (window is null)
        {
            return;
        }

        try
        {
            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.Invoke(() => Apply(window, useDark));
                return;
            }

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var value = useDark ? 1 : 0;
            if (TrySetAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value))
            {
                return;
            }

            TrySetAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref value);
        }
        catch
        {
            // Не ломаем окно на ОС без поддержки или при сбое interop.
        }
    }

    private static bool TrySetAttribute(IntPtr hwnd, int attribute, ref int value)
    {
        try
        {
            return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }
}
