namespace ReminderApp.Services;

public sealed class NotificationPlacementModeItem
{
    public required string DisplayName { get; init; }
    public NotificationPlacementMode Mode { get; init; }
}
