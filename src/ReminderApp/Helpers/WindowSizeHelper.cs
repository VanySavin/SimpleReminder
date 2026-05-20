using System.Windows;

namespace ReminderApp.Helpers;

/// <summary>
/// Стартовые размеры окон относительно рабочей области экрана (без полноэкранного режима).
/// </summary>
public static class WindowSizeHelper
{
    private const double HorizontalMargin = 80;
    private const double VerticalMargin = 100;

    public static void ApplyMainWindowStartup(Window window)
    {
        var wa = SystemParameters.WorkArea;
        window.MinWidth = 720;
        window.MinHeight = 520;

        var w = Math.Min(900, wa.Width - HorizontalMargin);
        var h = Math.Min(680, wa.Height - VerticalMargin);
        window.Width = Math.Max(window.MinWidth, w);
        window.Height = Math.Max(window.MinHeight, h);
    }

    public static void ApplySettingsWindowStartup(Window window)
    {
        var wa = SystemParameters.WorkArea;
        window.MinWidth = 720;
        window.MinHeight = 520;

        var w = Math.Min(860, wa.Width - HorizontalMargin);
        var h = Math.Min(720, wa.Height - VerticalMargin);
        window.Width = Math.Max(window.MinWidth, w);
        window.Height = Math.Max(window.MinHeight, h);
    }

    public static void ApplyAboutWindowStartup(Window window)
    {
        var wa = SystemParameters.WorkArea;
        window.MinWidth = 640;
        window.MinHeight = 480;

        var w = Math.Min(820, wa.Width - HorizontalMargin);
        var h = Math.Min(700, wa.Height - VerticalMargin);
        window.Width = Math.Max(window.MinWidth, w);
        window.Height = Math.Max(window.MinHeight, h);
    }

    public static void ApplyCustomSnoozeWindowStartup(Window window)
    {
        window.MinWidth = 380;
        window.MinHeight = 220;
        window.Width = 420;
        window.Height = 240;
    }
}
