using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace ReminderApp.Helpers;

public sealed record ScreenPlacementContext(
    string? DeviceName,
    Rect WorkAreaDip,
    double DpiScaleX,
    double DpiScaleY,
    bool IsPrimary,
    bool UsedPrimaryFallback);

public static class ScreenPlacementHelper
{
    private const int MonitorDefaultToNearest = 2;
    private const int MdT_Default = 0;

    public static IReadOnlyList<ScreenPlacementContext> GetScreensInDip() =>
        Screen.AllScreens.Select(s => BuildContext(s, usedPrimaryFallback: false)).ToList();

    public static ScreenPlacementContext ResolvePlacementContext(
        string? screenDeviceName,
        double? customLeft = null,
        double? customTop = null)
    {
        var selected = Screen.AllScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, screenDeviceName, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            return BuildContext(selected, usedPrimaryFallback: false);
        }

        if (customLeft.HasValue
            && customTop.HasValue
            && IsValidPoint(customLeft.Value, customTop.Value))
        {
            var screens = GetScreensInDip();
            var point = new System.Windows.Point(customLeft.Value, customTop.Value);
            var containing = screens.FirstOrDefault(s => s.WorkAreaDip.Contains(point));
            if (containing is not null)
            {
                return containing with { UsedPrimaryFallback = false };
            }

            var nearest = screens
                .Select(s => new
                {
                    Context = s,
                    Distance = DistanceSquaredToRect(customLeft.Value, customTop.Value, s.WorkAreaDip)
                })
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            if (nearest is not null)
            {
                return nearest.Context with { UsedPrimaryFallback = false };
            }
        }

        var primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var usedFallback = !string.IsNullOrWhiteSpace(screenDeviceName)
                           || (customLeft.HasValue && customTop.HasValue);
        return BuildContext(primary, usedPrimaryFallback: usedFallback);
    }

    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static bool IsValidPoint(double left, double top) => IsFinite(left) && IsFinite(top);

    public static bool IsFullyOutsideAllScreens(double left, double top, double width, double height)
    {
        if (!IsValidPoint(left, top) || !IsFinite(width) || !IsFinite(height) || width <= 0 || height <= 0)
        {
            return true;
        }

        var target = new Rect(left, top, width, height);
        return !GetScreensInDip().Any(s => target.IntersectsWith(s.WorkAreaDip));
    }

    public static (double Left, double Top) ClampToWorkArea(
        double left,
        double top,
        double width,
        double height,
        Rect workArea,
        double margin)
    {
        if (!IsFinite(width) || width <= 0)
        {
            width = 1;
        }

        if (!IsFinite(height) || height <= 0)
        {
            height = 1;
        }

        if (!IsValidPoint(left, top))
        {
            left = workArea.Right - margin - width;
            top = workArea.Bottom - margin - height;
        }

        var minLeft = workArea.Left + margin;
        var maxLeft = Math.Max(minLeft, workArea.Right - margin - width);
        var minTop = workArea.Top + margin;
        var maxTop = Math.Max(minTop, workArea.Bottom - margin - height);
        return (Math.Clamp(left, minLeft, maxLeft), Math.Clamp(top, minTop, maxTop));
    }

    public static (double Left, double Top) BottomRight(Rect workArea, double width, double height, double margin)
    {
        if (!IsFinite(width) || width <= 0)
        {
            width = 1;
        }

        if (!IsFinite(height) || height <= 0)
        {
            height = 1;
        }

        var left = workArea.Right - margin - width;
        var top = workArea.Bottom - margin - height;
        return ClampToWorkArea(left, top, width, height, workArea, margin);
    }

    public static string? ResolveScreenDeviceNameByWindowRect(double left, double top, double width, double height)
    {
        if (!IsValidPoint(left, top))
        {
            return null;
        }

        var cx = left + Math.Max(width, 1) / 2;
        var cy = top + Math.Max(height, 1) / 2;
        var best = GetScreensInDip()
            .Select(s => new
            {
                s.DeviceName,
                Distance = DistanceSquaredToRect(cx, cy, s.WorkAreaDip)
            })
            .OrderBy(x => x.Distance)
            .FirstOrDefault();
        return best?.DeviceName;
    }

    private static ScreenPlacementContext BuildContext(Screen screen, bool usedPrimaryFallback)
    {
        var (scaleX, scaleY) = TryGetScreenScale(screen);
        var dipRect = ToDip(screen.WorkingArea, scaleX, scaleY);
        return new ScreenPlacementContext(
            screen.DeviceName,
            dipRect,
            scaleX,
            scaleY,
            screen.Primary,
            usedPrimaryFallback);
    }

    private static Rect ToDip(System.Drawing.Rectangle pxRect, double scaleX, double scaleY)
    {
        var safeX = scaleX <= 0 ? 1.0 : scaleX;
        var safeY = scaleY <= 0 ? 1.0 : scaleY;
        return new Rect(
            pxRect.Left / safeX,
            pxRect.Top / safeY,
            pxRect.Width / safeX,
            pxRect.Height / safeY);
    }

    private static (double ScaleX, double ScaleY) TryGetScreenScale(Screen screen)
    {
        try
        {
            var rect = new NativeRect
            {
                Left = screen.WorkingArea.Left,
                Top = screen.WorkingArea.Top,
                Right = screen.WorkingArea.Right,
                Bottom = screen.WorkingArea.Bottom
            };
            var monitor = MonitorFromRect(ref rect, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MdT_Default, out var dpiX, out var dpiY) == 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch
        {
            // fallback below
        }

        try
        {
            var app = System.Windows.Application.Current;
            var reference = app?.MainWindow ?? app?.Windows.OfType<Window>().FirstOrDefault();
            if (reference is not null)
            {
                var dpi = VisualTreeHelper.GetDpi(reference);
                if (dpi.DpiScaleX > 0 && dpi.DpiScaleY > 0)
                {
                    return (dpi.DpiScaleX, dpi.DpiScaleY);
                }
            }
        }
        catch
        {
            // fallback below
        }

        return (1.0, 1.0);
    }

    private static double DistanceSquaredToRect(double x, double y, Rect rect)
    {
        var dx = x < rect.Left ? rect.Left - x : x > rect.Right ? x - rect.Right : 0;
        var dy = y < rect.Top ? rect.Top - y : y > rect.Bottom ? y - rect.Bottom : 0;
        return dx * dx + dy * dy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect lprc, int dwFlags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
