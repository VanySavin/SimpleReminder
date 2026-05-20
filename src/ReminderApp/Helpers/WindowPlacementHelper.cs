using System.Windows;
using ReminderApp.Services;

namespace ReminderApp.Helpers;

public static class WindowPlacementHelper
{
    private const double EdgeMargin = 12;
    private const double MainWindowOffsetX = 16;
    private const double MainWindowOffsetY = 48;
    private const double NotificationGap = 12;

    public static MainWindowPlacementSettings CaptureMainWindowPlacement(Window window)
    {
        var bounds = window.WindowState == WindowState.Maximized
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        string? screenDeviceName = null;
        try
        {
            screenDeviceName = ScreenPlacementHelper.ResolveScreenDeviceNameByWindowRect(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height);
        }
        catch
        {
            // ignore
        }

        return new MainWindowPlacementSettings
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = window.WindowState == WindowState.Maximized ? "Maximized" : "Normal",
            ScreenDeviceName = screenDeviceName
        };
    }

    public static void ApplyMainWindowPlacement(
        Window window,
        MainWindowPlacementSettings placement,
        bool useFallbackWhenInvalid = true)
    {
        var hasValidBounds = placement.Left.HasValue
                             && placement.Top.HasValue
                             && placement.Width.HasValue
                             && placement.Height.HasValue
                             && ScreenPlacementHelper.IsValidPoint(placement.Left.Value, placement.Top.Value)
                             && !ScreenPlacementHelper.IsFullyOutsideAllScreens(
                                 placement.Left.Value,
                                 placement.Top.Value,
                                 placement.Width.Value,
                                 placement.Height.Value);

        if (!hasValidBounds)
        {
            if (useFallbackWhenInvalid)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowSizeHelper.ApplyMainWindowStartup(window);
            }

            return;
        }

        var context = ScreenPlacementHelper.ResolvePlacementContext(
            placement.ScreenDeviceName,
            placement.Left,
            placement.Top);

        var width = Math.Max(window.MinWidth, placement.Width!.Value);
        var height = Math.Max(window.MinHeight, placement.Height!.Value);
        var (left, top) = ScreenPlacementHelper.ClampToWorkArea(
            placement.Left!.Value,
            placement.Top!.Value,
            width,
            height,
            context.WorkAreaDip,
            EdgeMargin);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;

        if (string.Equals(placement.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase))
        {
            window.Loaded += OnMainWindowLoadedMaximize;
        }
    }

    private static void OnMainWindowLoadedMaximize(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.Loaded -= OnMainWindowLoadedMaximize;
        window.WindowState = WindowState.Maximized;
    }

    public static (double Left, double Top, string? ScreenDeviceName) ComputeCustomSnoozePosition(
        CustomSnoozeWindowPlacementSettings placementSettings,
        CustomSnoozeOpenContext openContext,
        double windowWidth,
        double windowHeight)
    {
        if (placementSettings.Mode == CustomSnoozeWindowPlacementMode.Custom
            && placementSettings.CustomLeft.HasValue
            && placementSettings.CustomTop.HasValue
            && ScreenPlacementHelper.IsValidPoint(placementSettings.CustomLeft.Value, placementSettings.CustomTop.Value))
        {
            return ClampSnoozePosition(
                placementSettings.CustomLeft.Value,
                placementSettings.CustomTop.Value,
                windowWidth,
                windowHeight,
                placementSettings.ScreenDeviceName);
        }

        if (placementSettings.Mode == CustomSnoozeWindowPlacementMode.SameScreenAsNotification
            && openContext.Source == CustomSnoozeOpenSource.Notification
            && TryGetNotificationAnchor(openContext, out var notifLeft, out var notifTop, out var notifWidth, out var notifHeight, out var notifScreen))
        {
            return ComputeNearNotification(
                notifLeft,
                notifTop,
                notifWidth,
                notifHeight,
                notifScreen,
                windowWidth,
                windowHeight);
        }

        return ComputeNearMainWindow(openContext.MainWindow, windowWidth, windowHeight);
    }

    public static void ApplyManualPosition(
        Window window,
        double left,
        double top,
        double width,
        double height,
        string? screenDeviceName)
    {
        var (clampedLeft, clampedTop, screen) = ClampSnoozePosition(left, top, width, height, screenDeviceName);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;
        window.Left = clampedLeft;
        window.Top = clampedTop;
        _ = screen;
    }

    private static (double Left, double Top, string? ScreenDeviceName) ComputeNearMainWindow(
        Window? mainWindow,
        double windowWidth,
        double windowHeight)
    {
        if (mainWindow is not null)
        {
            var anchor = mainWindow.WindowState == WindowState.Maximized
                ? mainWindow.RestoreBounds
                : new Rect(mainWindow.Left, mainWindow.Top, mainWindow.Width, mainWindow.Height);

            if (ScreenPlacementHelper.IsValidPoint(anchor.Left, anchor.Top))
            {
                var left = anchor.Left + MainWindowOffsetX;
                var top = anchor.Top + MainWindowOffsetY;
                var screen = ScreenPlacementHelper.ResolveScreenDeviceNameByWindowRect(
                    anchor.Left,
                    anchor.Top,
                    Math.Max(anchor.Width, 1),
                    Math.Max(anchor.Height, 1));
                return ClampSnoozePosition(left, top, windowWidth, windowHeight, screen);
            }
        }

        var primary = ScreenPlacementHelper.ResolvePlacementContext(null);
        var fallbackLeft = primary.WorkAreaDip.Left + MainWindowOffsetX;
        var fallbackTop = primary.WorkAreaDip.Top + MainWindowOffsetY;
        return ClampSnoozePosition(fallbackLeft, fallbackTop, windowWidth, windowHeight, primary.DeviceName);
    }

    private static (double Left, double Top, string? ScreenDeviceName) ComputeNearNotification(
        double notifLeft,
        double notifTop,
        double notifWidth,
        double notifHeight,
        string? notifScreen,
        double windowWidth,
        double windowHeight)
    {
        var context = ScreenPlacementHelper.ResolvePlacementContext(notifScreen, notifLeft, notifTop);
        var work = context.WorkAreaDip;
        var safeNotifWidth = Math.Max(notifWidth, 1);
        var safeNotifHeight = Math.Max(notifHeight, 1);

        var rightCandidate = notifLeft + safeNotifWidth + NotificationGap;
        var leftCandidate = notifLeft - windowWidth - NotificationGap;
        var top = notifTop;

        var rightFits = rightCandidate + windowWidth <= work.Right - EdgeMargin;
        var leftFits = leftCandidate >= work.Left + EdgeMargin;
        var left = rightFits
            ? rightCandidate
            : leftFits
                ? leftCandidate
                : notifLeft;

        return ClampSnoozePosition(left, top, windowWidth, windowHeight, notifScreen ?? context.DeviceName);
    }

    private static bool TryGetNotificationAnchor(
        CustomSnoozeOpenContext context,
        out double left,
        out double top,
        out double width,
        out double height,
        out string? screenDeviceName)
    {
        left = context.NotificationLeft ?? double.NaN;
        top = context.NotificationTop ?? double.NaN;
        width = context.NotificationWidth ?? 0;
        height = context.NotificationHeight ?? 0;
        screenDeviceName = context.NotificationScreenDeviceName;

        return ScreenPlacementHelper.IsValidPoint(left, top)
               && width > 0
               && height > 0;
    }

    private static (double Left, double Top, string? ScreenDeviceName) ClampSnoozePosition(
        double left,
        double top,
        double windowWidth,
        double windowHeight,
        string? screenDeviceName)
    {
        if (!ScreenPlacementHelper.IsValidPoint(left, top)
            || ScreenPlacementHelper.IsFullyOutsideAllScreens(left, top, windowWidth, windowHeight))
        {
            var primary = ScreenPlacementHelper.ResolvePlacementContext(null);
            left = primary.WorkAreaDip.Left + MainWindowOffsetX;
            top = primary.WorkAreaDip.Top + MainWindowOffsetY;
            screenDeviceName = primary.DeviceName;
        }

        var context = ScreenPlacementHelper.ResolvePlacementContext(screenDeviceName, left, top);
        var (clampedLeft, clampedTop) = ScreenPlacementHelper.ClampToWorkArea(
            left,
            top,
            windowWidth,
            windowHeight,
            context.WorkAreaDip,
            EdgeMargin);
        return (clampedLeft, clampedTop, context.DeviceName);
    }
}
