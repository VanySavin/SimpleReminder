namespace ReminderApp.Services;

public sealed class ThemeModeItem
{
    public required string DisplayName { get; init; }
    public ThemeMode Mode { get; init; }
}
