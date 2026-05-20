namespace ReminderApp.Models;

/// <summary>Кнопка «отложить на N» в строке списка напоминаний.</summary>
public sealed class SnoozeSlotRow
{
    public required ReminderListItemViewModel Item { get; init; }
    public int Minutes { get; init; }
    public string Label { get; init; } = string.Empty;
}
