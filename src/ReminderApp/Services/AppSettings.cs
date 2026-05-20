using System.Text.Json.Serialization;

namespace ReminderApp.Services;

public sealed class AppSettings
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Auto;

    public List<SnoozeButtonSettings> SnoozeButtons { get; set; } =
    [
        new() { Kind = SnoozeButtonKind.Minutes, Enabled = true, Minutes = 5 },
        new() { Kind = SnoozeButtonKind.Minutes, Enabled = false, Minutes = 10 },
        new() { Kind = SnoozeButtonKind.Minutes, Enabled = true, Minutes = 30 },
        new() { Kind = SnoozeButtonKind.Custom, Enabled = true }
    ];

    public int AutoSnoozeOnDismissMinutes { get; set; } = 20;

    public bool SubmitReminderOnEnter { get; set; } = true;

    public bool AutoFocusInputOnStart { get; set; } = true;

    public bool ShowInputExamples { get; set; } = true;

    public bool ShowCreatedNotification { get; set; } = true;

    public bool PlayNotificationSound { get; set; } = true;

    public int NotificationDurationSeconds { get; set; } = 5;

    public int MaxVisibleNotifications { get; set; } = 4;
    public NotificationPlacementSettings NotificationPlacement { get; set; } = new();
    public CustomSnoozeWindowPlacementSettings CustomSnoozeWindowPlacement { get; set; } = new();
    public MainWindowPlacementSettings MainWindowPlacement { get; set; } = new();

    public bool MinimizeToTrayOnClose { get; set; } = true;

    public bool ShowTrayNoticeOncePerSession { get; set; } = true;

    public int SchedulerIntervalSeconds { get; set; } = 2;

    public bool AutoStartWithWindows { get; set; }

    public bool StartMinimizedOnAutoStart { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? SnoozeOptionsMinutes { get; set; }

    [JsonPropertyName("_descriptions")]
    public Dictionary<string, string> Descriptions { get; set; } = new(StringComparer.Ordinal);

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}
