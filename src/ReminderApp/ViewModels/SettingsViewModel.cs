using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReminderApp.Services;

namespace ReminderApp.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly Action _onSaved;
    private readonly AutoStartService _autoStartService;

    private string _snooze1Text = "5";
    private string _snooze2Text = "10";
    private string _snooze3Text = "30";
    private string _snoozeError = string.Empty;
    private ThemeModeItem? _selectedTheme;
    private bool _autoStartWithWindows;
    private bool _startMinimizedOnAutoStart = true;
    private NotificationPlacementModeItem? _selectedPlacement;
    private CustomSnoozeWindowPlacementModeItem? _selectedSnoozeWindowPlacement;
    private bool _snooze1Enabled = true;
    private bool _snooze2Enabled;
    private bool _snooze3Enabled = true;
    private bool _customSnoozeEnabled = true;
    private double? _customPlacementLeft;
    private double? _customPlacementTop;
    private string? _customPlacementScreenDeviceName;
    private double? _customSnoozeWindowLeft;
    private double? _customSnoozeWindowTop;
    private string? _customSnoozeWindowScreenDeviceName;
    private int _notificationDurationSeconds;
    private int _maxVisibleNotifications;
    private int _schedulerIntervalSeconds;
    private int _autoSnoozeOnDismissMinutes;

    public IReadOnlyList<ThemeModeItem> ThemeModeItems { get; } =
    [
        new ThemeModeItem { DisplayName = "Авто", Mode = ThemeMode.Auto },
        new ThemeModeItem { DisplayName = "Светлая", Mode = ThemeMode.Light },
        new ThemeModeItem { DisplayName = "Тёмная", Mode = ThemeMode.Dark }
    ];

    public IReadOnlyList<NotificationPlacementModeItem> PlacementModeItems { get; } =
    [
        new NotificationPlacementModeItem { DisplayName = "Справа снизу", Mode = NotificationPlacementMode.BottomRight },
        new NotificationPlacementModeItem { DisplayName = "Справа сверху", Mode = NotificationPlacementMode.TopRight },
        new NotificationPlacementModeItem { DisplayName = "Слева снизу", Mode = NotificationPlacementMode.BottomLeft },
        new NotificationPlacementModeItem { DisplayName = "Слева сверху", Mode = NotificationPlacementMode.TopLeft },
        new NotificationPlacementModeItem { DisplayName = "Центр снизу", Mode = NotificationPlacementMode.BottomCenter },
        new NotificationPlacementModeItem { DisplayName = "Пользовательская позиция", Mode = NotificationPlacementMode.Custom }
    ];

    public IReadOnlyList<CustomSnoozeWindowPlacementModeItem> SnoozeWindowPlacementModeItems { get; } =
    [
        new CustomSnoozeWindowPlacementModeItem { DisplayName = "Рядом с основным окном", Mode = CustomSnoozeWindowPlacementMode.NearMainWindow },
        new CustomSnoozeWindowPlacementModeItem { DisplayName = "На экране уведомления", Mode = CustomSnoozeWindowPlacementMode.SameScreenAsNotification },
        new CustomSnoozeWindowPlacementModeItem { DisplayName = "Пользовательская позиция", Mode = CustomSnoozeWindowPlacementMode.Custom }
    ];

    public SettingsViewModel(
        SettingsService settingsService,
        ThemeService themeService,
        Action onSaved,
        AutoStartService? autoStartService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _onSaved = onSaved;
        _autoStartService = autoStartService ?? new AutoStartService();
        var d = settingsService.CloneCurrent();
        _selectedTheme = ThemeModeItems.First(i => i.Mode == d.ThemeMode);
        var buttons = d.SnoozeButtons.ToList();
        var minuteButtons = buttons.Where(b => b.Kind == SnoozeButtonKind.Minutes).Take(3).ToList();
        while (minuteButtons.Count < 3)
        {
            minuteButtons.Add(new SnoozeButtonSettings { Kind = SnoozeButtonKind.Minutes, Enabled = false, Minutes = 5 });
        }

        _snooze1Text = (minuteButtons[0].Minutes ?? 5).ToString();
        _snooze2Text = (minuteButtons[1].Minutes ?? 10).ToString();
        _snooze3Text = (minuteButtons[2].Minutes ?? 30).ToString();
        _snooze1Enabled = minuteButtons[0].Enabled;
        _snooze2Enabled = minuteButtons[1].Enabled;
        _snooze3Enabled = minuteButtons[2].Enabled;
        _customSnoozeEnabled = buttons.FirstOrDefault(b => b.Kind == SnoozeButtonKind.Custom)?.Enabled ?? true;
        _selectedPlacement = PlacementModeItems.FirstOrDefault(i => i.Mode == d.NotificationPlacement.Mode) ?? PlacementModeItems[0];
        _customPlacementLeft = d.NotificationPlacement.CustomLeft;
        _customPlacementTop = d.NotificationPlacement.CustomTop;
        _customPlacementScreenDeviceName = d.NotificationPlacement.ScreenDeviceName;
        _selectedSnoozeWindowPlacement = SnoozeWindowPlacementModeItems.FirstOrDefault(i => i.Mode == d.CustomSnoozeWindowPlacement.Mode)
                                         ?? SnoozeWindowPlacementModeItems[0];
        _customSnoozeWindowLeft = d.CustomSnoozeWindowPlacement.CustomLeft;
        _customSnoozeWindowTop = d.CustomSnoozeWindowPlacement.CustomTop;
        _customSnoozeWindowScreenDeviceName = d.CustomSnoozeWindowPlacement.ScreenDeviceName;

        SubmitReminderOnEnter = d.SubmitReminderOnEnter;
        AutoFocusInputOnStart = d.AutoFocusInputOnStart;
        ShowInputExamples = d.ShowInputExamples;
        ShowCreatedNotification = d.ShowCreatedNotification;
        PlayNotificationSound = d.PlayNotificationSound;
        NotificationDurationSeconds = d.NotificationDurationSeconds;
        MaxVisibleNotifications = d.MaxVisibleNotifications;
        MinimizeToTrayOnClose = d.MinimizeToTrayOnClose;
        ShowTrayNoticeOncePerSession = d.ShowTrayNoticeOncePerSession;
        SchedulerIntervalSeconds = d.SchedulerIntervalSeconds;
        _autoStartWithWindows = d.AutoStartWithWindows;
        _startMinimizedOnAutoStart = d.StartMinimizedOnAutoStart;
        AutoSnoozeOnDismissMinutes = d.AutoSnoozeOnDismissMinutes;
    }
    public NotificationPlacementModeItem? SelectedPlacement
    {
        get => _selectedPlacement;
        set
        {
            if (value is null || ReferenceEquals(_selectedPlacement, value))
            {
                return;
            }

            _selectedPlacement = value;
            OnPropertyChanged();
        }
    }

    public CustomSnoozeWindowPlacementModeItem? SelectedSnoozeWindowPlacement
    {
        get => _selectedSnoozeWindowPlacement;
        set
        {
            if (value is null || ReferenceEquals(_selectedSnoozeWindowPlacement, value))
            {
                return;
            }

            _selectedSnoozeWindowPlacement = value;
            OnPropertyChanged();
        }
    }


    public ThemeModeItem? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (value is null || ReferenceEquals(_selectedTheme, value))
            {
                return;
            }

            _selectedTheme = value;
            OnPropertyChanged();
        }
    }

    public string Snooze1Text
    {
        get => _snooze1Text;
        set
        {
            if (_snooze1Text == value) return;
            _snooze1Text = value;
            OnPropertyChanged();
        }
    }

    public string Snooze2Text
    {
        get => _snooze2Text;
        set
        {
            if (_snooze2Text == value) return;
            _snooze2Text = value;
            OnPropertyChanged();
        }
    }

    public string Snooze3Text
    {
        get => _snooze3Text;
        set
        {
            if (_snooze3Text == value) return;
            _snooze3Text = value;
            OnPropertyChanged();
        }
    }

    public string SnoozeError
    {
        get => _snoozeError;
        private set
        {
            if (_snoozeError == value) return;
            _snoozeError = value;
            OnPropertyChanged();
        }
    }

    public bool Snooze1Enabled
    {
        get => _snooze1Enabled;
        set
        {
            if (_snooze1Enabled == value) return;
            _snooze1Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool Snooze2Enabled
    {
        get => _snooze2Enabled;
        set
        {
            if (_snooze2Enabled == value) return;
            _snooze2Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool Snooze3Enabled
    {
        get => _snooze3Enabled;
        set
        {
            if (_snooze3Enabled == value) return;
            _snooze3Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool CustomSnoozeEnabled
    {
        get => _customSnoozeEnabled;
        set
        {
            if (_customSnoozeEnabled == value) return;
            _customSnoozeEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool SubmitReminderOnEnter { get; set; }
    public bool AutoFocusInputOnStart { get; set; }
    public bool ShowInputExamples { get; set; }
    public bool ShowCreatedNotification { get; set; }
    public bool PlayNotificationSound { get; set; }

    public int NotificationDurationSeconds
    {
        get => _notificationDurationSeconds;
        set
        {
            if (_notificationDurationSeconds == value) return;
            _notificationDurationSeconds = value;
            OnPropertyChanged();
        }
    }

    public int MaxVisibleNotifications
    {
        get => _maxVisibleNotifications;
        set
        {
            if (_maxVisibleNotifications == value) return;
            _maxVisibleNotifications = value;
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTrayOnClose { get; set; }
    public bool ShowTrayNoticeOncePerSession { get; set; }

    public int SchedulerIntervalSeconds
    {
        get => _schedulerIntervalSeconds;
        set
        {
            if (_schedulerIntervalSeconds == value) return;
            _schedulerIntervalSeconds = value;
            OnPropertyChanged();
        }
    }

    public int AutoSnoozeOnDismissMinutes
    {
        get => _autoSnoozeOnDismissMinutes;
        set
        {
            if (_autoSnoozeOnDismissMinutes == value) return;
            _autoSnoozeOnDismissMinutes = value;
            OnPropertyChanged();
        }
    }

    public bool AutoStartWithWindows
    {
        get => _autoStartWithWindows;
        set
        {
            if (_autoStartWithWindows == value)
            {
                return;
            }

            _autoStartWithWindows = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartMinimizedOnAutoStartEnabled));
        }
    }

    public bool StartMinimizedOnAutoStartEnabled => _autoStartWithWindows;

    public bool StartMinimizedOnAutoStart
    {
        get => _startMinimizedOnAutoStart;
        set
        {
            if (_startMinimizedOnAutoStart == value)
            {
                return;
            }

            _startMinimizedOnAutoStart = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Возвращает (успех и можно закрыть окно, ошибка автозапуска, ошибка записи настроек).
    /// </summary>
    public async Task<(bool Completed, string? AutoStartError, string? SaveError)> TrySaveAsync()
    {
        SnoozeError = string.Empty;
        if (!TryBuildSnoozeButtons(out var buttons, out var err))
        {
            SnoozeError = err;
            return (false, null, null);
        }

        var snap = _settingsService.CloneCurrent();
        snap.ThemeMode = SelectedTheme?.Mode ?? ThemeMode.Auto;
        snap.SnoozeButtons = buttons;
        snap.SubmitReminderOnEnter = SubmitReminderOnEnter;
        snap.AutoFocusInputOnStart = AutoFocusInputOnStart;
        snap.ShowInputExamples = ShowInputExamples;
        snap.ShowCreatedNotification = ShowCreatedNotification;
        snap.PlayNotificationSound = PlayNotificationSound;
        snap.NotificationDurationSeconds = Math.Clamp(NotificationDurationSeconds, 2, 30);
        snap.MaxVisibleNotifications = Math.Clamp(MaxVisibleNotifications, 1, 8);
        snap.AutoSnoozeOnDismissMinutes = Math.Clamp(AutoSnoozeOnDismissMinutes, 1, 1440);
        if (SelectedPlacement is not null)
        {
            snap.NotificationPlacement.Mode = SelectedPlacement.Mode;
        }
        snap.NotificationPlacement.CustomLeft = _customPlacementLeft;
        snap.NotificationPlacement.CustomTop = _customPlacementTop;
        snap.NotificationPlacement.ScreenDeviceName = _customPlacementScreenDeviceName;
        if (SelectedSnoozeWindowPlacement is not null)
        {
            snap.CustomSnoozeWindowPlacement.Mode = SelectedSnoozeWindowPlacement.Mode;
        }
        snap.CustomSnoozeWindowPlacement.CustomLeft = _customSnoozeWindowLeft;
        snap.CustomSnoozeWindowPlacement.CustomTop = _customSnoozeWindowTop;
        snap.CustomSnoozeWindowPlacement.ScreenDeviceName = _customSnoozeWindowScreenDeviceName;
        snap.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        snap.ShowTrayNoticeOncePerSession = ShowTrayNoticeOncePerSession;
        snap.SchedulerIntervalSeconds = Math.Clamp(SchedulerIntervalSeconds, 1, 60);
        snap.AutoStartWithWindows = AutoStartWithWindows;
        snap.StartMinimizedOnAutoStart = StartMinimizedOnAutoStart;

        var (saveOk, saveError) = await _settingsService.TrySaveCurrentAsync(snap);
        if (!saveOk)
        {
            return (false, null, saveError);
        }

        _themeService.InitializeFromCurrentSettings();
        _onSaved();

        var ar = await _autoStartService.EnsureAutoStartMatchesSettingsAsync(_settingsService.Current);
        if (!ar.Success)
        {
            return (false, ar.ErrorMessage, null);
        }

        return (true, null, null);
    }

    private bool TryBuildSnoozeButtons(out List<SnoozeButtonSettings> buttons, out string error)
    {
        buttons = [];
        error = string.Empty;
        var values = new[] { Snooze1Text.Trim(), Snooze2Text.Trim(), Snooze3Text.Trim() };
        var enabled = new[] { Snooze1Enabled, Snooze2Enabled, Snooze3Enabled };
        var enabledMinutes = new HashSet<int>();

        for (var i = 0; i < values.Length; i++)
        {
            if (!int.TryParse(values[i], out var v) || v is < 1 or > 1440)
            {
                error = "Введите три целых числа от 1 до 1440 (минуты).";
                return false;
            }

            if (enabled[i] && !enabledMinutes.Add(v))
            {
                error = "Среди включённых кнопок отложения не должно быть одинаковых минут.";
                return false;
            }

            buttons.Add(new SnoozeButtonSettings
            {
                Kind = SnoozeButtonKind.Minutes,
                Enabled = enabled[i],
                Minutes = v
            });
        }

        buttons.Add(new SnoozeButtonSettings
        {
            Kind = SnoozeButtonKind.Custom,
            Enabled = CustomSnoozeEnabled
        });

        if (!enabled.Any(x => x) && !CustomSnoozeEnabled)
        {
            error = "Включите хотя бы одну кнопку отложения или кнопку «Другой срок…».";
            return false;
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyCustomPlacement(double left, double top, string? screenDeviceName)
    {
        _customPlacementLeft = left;
        _customPlacementTop = top;
        _customPlacementScreenDeviceName = screenDeviceName;
        SelectedPlacement = PlacementModeItems.FirstOrDefault(i => i.Mode == NotificationPlacementMode.Custom) ?? SelectedPlacement;
    }

    public void ResetNotificationPlacement()
    {
        _customPlacementLeft = null;
        _customPlacementTop = null;
        _customPlacementScreenDeviceName = null;
        SelectedPlacement = PlacementModeItems.FirstOrDefault(i => i.Mode == NotificationPlacementMode.BottomRight) ?? SelectedPlacement;
    }

    public (double? Left, double? Top, string? ScreenDeviceName) GetCustomPlacement() =>
        (_customPlacementLeft, _customPlacementTop, _customPlacementScreenDeviceName);

    public void ApplyCustomSnoozeWindowPlacement(double left, double top, string? screenDeviceName)
    {
        _customSnoozeWindowLeft = left;
        _customSnoozeWindowTop = top;
        _customSnoozeWindowScreenDeviceName = screenDeviceName;
        SelectedSnoozeWindowPlacement = SnoozeWindowPlacementModeItems.FirstOrDefault(i => i.Mode == CustomSnoozeWindowPlacementMode.Custom)
                                        ?? SelectedSnoozeWindowPlacement;
    }

    public void ResetCustomSnoozeWindowPlacement()
    {
        _customSnoozeWindowLeft = null;
        _customSnoozeWindowTop = null;
        _customSnoozeWindowScreenDeviceName = null;
        SelectedSnoozeWindowPlacement = SnoozeWindowPlacementModeItems.FirstOrDefault(i => i.Mode == CustomSnoozeWindowPlacementMode.NearMainWindow)
                                        ?? SnoozeWindowPlacementModeItems[0];
    }

    public (double? Left, double? Top, string? ScreenDeviceName) GetCustomSnoozeWindowPlacement() =>
        (_customSnoozeWindowLeft, _customSnoozeWindowTop, _customSnoozeWindowScreenDeviceName);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
