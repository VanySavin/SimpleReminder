namespace ReminderApp.Services;

public static class SnoozeButtonsResolver
{
    public static IReadOnlyList<int> GetEnabledMinutes(IReadOnlyList<SnoozeButtonSettings>? buttons) =>
        (buttons ?? [])
        .Where(b => b.Kind == SnoozeButtonKind.Minutes && b.Enabled && b.Minutes is >= 1 and <= 1440)
        .Select(b => b.Minutes!.Value)
        .Distinct()
        .ToList();

    public static bool IsCustomEnabled(IReadOnlyList<SnoozeButtonSettings>? buttons) =>
        (buttons ?? []).Any(b => b.Kind == SnoozeButtonKind.Custom && b.Enabled);
}
