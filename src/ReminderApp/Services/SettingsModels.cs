namespace ReminderApp.Services;

public enum SnoozeButtonKind
{
    Minutes,
    Custom
}

public sealed class SnoozeButtonSettings
{
    public SnoozeButtonKind Kind { get; set; } = SnoozeButtonKind.Minutes;
    public bool Enabled { get; set; } = true;
    public int? Minutes { get; set; }
}

public enum NotificationPlacementMode
{
    BottomRight,
    TopRight,
    BottomLeft,
    TopLeft,
    BottomCenter,
    Custom
}

public sealed class NotificationPlacementSettings
{
    public NotificationPlacementMode Mode { get; set; } = NotificationPlacementMode.BottomRight;
    public double? CustomLeft { get; set; }
    public double? CustomTop { get; set; }
    public string? ScreenDeviceName { get; set; }
}

public enum CustomSnoozeWindowPlacementMode
{
    NearMainWindow,
    SameScreenAsNotification,
    Custom
}

public sealed class CustomSnoozeWindowPlacementSettings
{
    public CustomSnoozeWindowPlacementMode Mode { get; set; } = CustomSnoozeWindowPlacementMode.NearMainWindow;
    public double? CustomLeft { get; set; }
    public double? CustomTop { get; set; }
    public string? ScreenDeviceName { get; set; }
}

public sealed class MainWindowPlacementSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? WindowState { get; set; } = "Normal";
    public string? ScreenDeviceName { get; set; }
}

public enum CustomSnoozeOpenSource
{
    MainList,
    Notification
}

public sealed record CustomSnoozeOpenContext(
    CustomSnoozeOpenSource Source,
    System.Windows.Window? MainWindow,
    double? NotificationLeft,
    double? NotificationTop,
    double? NotificationWidth,
    double? NotificationHeight,
    string? NotificationScreenDeviceName);
