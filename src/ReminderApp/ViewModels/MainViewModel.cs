using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ReminderApp.Helpers;
using ReminderApp.Models;
using ReminderApp.Services;
using ReminderApp.Views;

namespace ReminderApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ReminderStorageService _storage;
    private readonly ReminderTextParser _parser;
    private readonly ReminderSchedulerService _scheduler;
    private readonly NotificationService _notifications;
    private readonly SettingsService _settings;
    private readonly ThemeService _themeService;
    private Window? _mainWindow;
    private Action? _focusInputAction;
    private string _inputText = string.Empty;
    private string _statusMessage =
        "Формат: через 5 минут, через 1 час 36 минут, завтра в 9, в понедельник, суп через 5 минут — и текст.";
    private string _parseError = string.Empty;
    private List<Reminder> _allReminders = [];
    private readonly HashSet<string> _dueInProgress = [];
    private readonly HashSet<string> _notificationPendingIds = [];
    private const string SnoozeParseErrorMessage = "Не понял срок. Пример: на 5 минут, завтра в 10, через месяц";

    public ObservableCollection<ReminderListItemViewModel> ActiveReminders { get; } = [];

    public RelayCommand AddReminderCommand { get; }
    public RelayCommand TestNotificationCommand { get; }
    public RelayCommand MarkDoneCommand { get; }
    public RelayCommand SnoozeForSlotCommand { get; }
    public RelayCommand CustomSnoozeCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenAboutCommand { get; }

    public bool HasActiveReminders => ActiveReminders.Count > 0;

    public bool ShowEmptyState => ActiveReminders.Count == 0;

    public bool ShowInputExamplesLine => _settings.Current.ShowInputExamples;

    public bool SubmitReminderOnEnter => _settings.Current.SubmitReminderOnEnter;

    public bool MinimizeToTrayOnClose => _settings.Current.MinimizeToTrayOnClose;

    /// <summary>Предупреждение при загрузке данных напоминаний (если было).</summary>
    public string? StartupDataWarning { get; private set; }

    public string InputExamplesText =>
        "через 10 минут проверить · завтра в 9 суп · в понедельник в 23 · суп через 5 минут";

    public MainViewModel(
        ReminderStorageService storage,
        ReminderTextParser parser,
        ReminderSchedulerService scheduler,
        NotificationService notifications,
        SettingsService settings,
        ThemeService themeService)
    {
        _storage = storage;
        _parser = parser;
        _scheduler = scheduler;
        _notifications = notifications;
        _settings = settings;
        _themeService = themeService;
        _scheduler.ReminderDue += OnReminderDue;

        ActiveReminders.CollectionChanged += OnActiveRemindersChanged;

        AddReminderCommand = new RelayCommand(async _ => await AddReminderAsync());
        TestNotificationCommand = new RelayCommand(_ => _notifications.ShowTestNotification());
        MarkDoneCommand = new RelayCommand(async item => await MarkDoneAsync(item as ReminderListItemViewModel));
        SnoozeForSlotCommand = new RelayCommand(async p => await SnoozeForSlotAsync(p as SnoozeSlotRow));
        CustomSnoozeCommand = new RelayCommand(async item => await OpenCustomSnoozeAsync(item as ReminderListItemViewModel));
        DeleteCommand = new RelayCommand(async item => await DeleteAsync(item as ReminderListItemViewModel));
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        OpenAboutCommand = new RelayCommand(_ => OpenAbout());
    }

    public void AttachFocusInput(Action focusAction) => _focusInputAction = focusAction;

    private void OpenSettings()
    {
        if (_mainWindow is null)
        {
            return;
        }

        var vm = new SettingsViewModel(_settings, _themeService, ApplyAfterSettingsSaved);
        var w = new SettingsWindow(vm, _themeService) { Owner = _mainWindow };
        w.ShowDialog();
        FocusInputDeferred();
    }

    private void OpenAbout()
    {
        if (_mainWindow is null)
        {
            return;
        }

        var w = new AboutWindow(_themeService) { Owner = _mainWindow };
        w.ShowDialog();
        FocusInputDeferred();
    }

    private void ApplyAfterSettingsSaved()
    {
        _scheduler.ApplyIntervalFromSettings();
        RefreshActiveList();
        OnPropertyChanged(nameof(ShowInputExamplesLine));
        OnPropertyChanged(nameof(SubmitReminderOnEnter));
        OnPropertyChanged(nameof(MinimizeToTrayOnClose));
    }

    private void FocusInput()
    {
        _focusInputAction?.Invoke();
    }

    private void OnActiveRemindersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasActiveReminders));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    public async Task InitializeAsync()
    {
        StartupDataWarning = null;
        try
        {
            _allReminders = await _storage.LoadAllAsync();
        }
        catch (Exception ex)
        {
            _allReminders = [];
            StartupDataWarning =
                "Не удалось загрузить файл напоминаний (reminders.json).\n" +
                ex.Message + "\n\n" + StartupDiagnostics.FormatPathsFooter();
            StartupDiagnostics.TraceException("Загрузка reminders.json", ex);
        }

        RefreshActiveList();
        _scheduler.Start();
    }

    public void AttachMainWindow(Window window) => _mainWindow = window;

    private async Task AddReminderAsync()
    {
        ParseError = string.Empty;
        if (string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

        var parseResult = _parser.Parse(InputText, DateTime.Now);
        if (!parseResult.IsSuccess)
        {
            ParseError = parseResult.ErrorMessage;
            FocusInputDeferred();
            return;
        }

        var original = InputText.Trim();
        await CreateReminderAsync(parseResult.NextRunAt!.Value, parseResult.ReminderText, original);
    }

    private async Task CreateReminderAsync(DateTime runAt, string reminderText, string originalInput)
    {
        var reminder = new Reminder
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = reminderText,
            OriginalInput = originalInput,
            NextRunAt = runAt,
            CreatedAt = DateTime.Now
        };

        _allReminders.Add(reminder);
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();

        InputText = string.Empty;
        ParseError = string.Empty;
        StatusMessage = $"Добавлено напоминание на {runAt:dd.MM.yyyy HH:mm}.";

        if (_settings.Current.ShowCreatedNotification)
        {
            _notifications.ShowInfoToast(
                "Напоминание создано",
                $"Сработает в {runAt:HH:mm} — {reminderText}");
        }

        FocusInputDeferred();
    }

    private void FocusInputDeferred()
    {
        if (_mainWindow?.Dispatcher is null)
        {
            FocusInput();
            return;
        }

        _mainWindow.Dispatcher.BeginInvoke(
            FocusInput,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Запланировать фокус в поле ввода (в т.ч. после команды второго экземпляра).</summary>
    public void RequestFocusInputDeferred() => FocusInputDeferred();

    private async Task MarkDoneAsync(ReminderListItemViewModel? item)
    {
        if (item is null) return;
        item.Reminder.IsCompleted = true;
        item.Reminder.CompletedAt = DateTime.Now;
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
        DismissReminderNotification(item.Reminder.Id);
    }

    private async Task DeleteAsync(ReminderListItemViewModel? item)
    {
        if (item is null) return;
        var reminderId = item.Reminder.Id;
        _allReminders.RemoveAll(r => r.Id == reminderId);
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
        DismissReminderNotification(reminderId);
    }

    private async Task SnoozeForSlotAsync(SnoozeSlotRow? row)
    {
        if (row is null) return;
        await SnoozeReminderCoreAsync(row.Item.Reminder, row.Minutes);
    }

    private async Task SnoozeReminderCoreAsync(Reminder reminder, int minutes)
    {
        await SnoozeReminderUntilAsync(reminder, DateTime.Now.AddMinutes(minutes), $"Напомню: {SnoozeLabelFormatter.Format(minutes)}", true);
    }

    private async Task SnoozeReminderUntilAsync(Reminder reminder, DateTime runAt, string notificationMessage, bool showNotification)
    {
        reminder.NextRunAt = runAt;
        reminder.LastNotifiedAt = null;
        reminder.IsCompleted = false;
        reminder.CompletedAt = null;
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
        DismissReminderNotification(reminder.Id);
        if (showNotification)
        {
            _notifications.ShowInfoToast(
                "Отложено",
                notificationMessage,
                false);
        }
    }

    private async Task CompleteReminderAfterPopupAsync(Reminder reminder)
    {
        reminder.IsCompleted = true;
        reminder.CompletedAt = DateTime.Now;
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
        ClearNotificationPending(reminder.Id);
    }

    private async Task MarkReminderAsNotifiedAsync(Reminder reminder)
    {
        reminder.LastNotifiedAt = DateTime.Now;
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
    }

    private void DismissReminderNotification(string reminderId)
    {
        _notificationPendingIds.Remove(reminderId);
        _notifications.DismissReminderNotification(reminderId);
    }

    private void ClearNotificationPending(string reminderId) =>
        _notificationPendingIds.Remove(reminderId);

    private void RefreshActiveList()
    {
        var snooze = SnoozeButtonsResolver.GetEnabledMinutes(_settings.Current.SnoozeButtons);
        var activeItems = _allReminders
            .Where(r => !r.IsCompleted)
            .OrderBy(r => r.NextRunAt)
            .Select(r =>
            {
                var vm = new ReminderListItemViewModel { Reminder = r };
                vm.RebuildSnoozeSlots(snooze);
                return vm;
            })
            .ToList();

        ActiveReminders.Clear();
        foreach (var active in activeItems)
        {
            ActiveReminders.Add(active);
        }
    }

    private async void OnReminderDue(object? sender, Reminder reminderFromScheduler)
    {
        if (_dueInProgress.Contains(reminderFromScheduler.Id)
            || _notificationPendingIds.Contains(reminderFromScheduler.Id))
        {
            return;
        }

        _dueInProgress.Add(reminderFromScheduler.Id);
        try
        {
            var local = _allReminders.FirstOrDefault(r =>
                r.Id == reminderFromScheduler.Id && !r.IsCompleted);

            if (local is null)
            {
                _allReminders = await _storage.LoadAllAsync();
                local = _allReminders.FirstOrDefault(r =>
                    r.Id == reminderFromScheduler.Id && !r.IsCompleted);
            }

            if (local is null)
            {
                return;
            }

            _notificationPendingIds.Add(local.Id);
            RequestReminderNotificationFor(local);
        }
        finally
        {
            _dueInProgress.Remove(reminderFromScheduler.Id);
        }
    }

    private void RequestReminderNotificationFor(Reminder reminderRef)
    {
        var snooze = SnoozeButtonsResolver.GetEnabledMinutes(_settings.Current.SnoozeButtons).ToList();
        var customSnoozeEnabled = SnoozeButtonsResolver.IsCustomEnabled(_settings.Current.SnoozeButtons);
        var autoDismissMinutes = _settings.Current.AutoSnoozeOnDismissMinutes;
        _notifications.RequestReminderNotification(
            reminderRef.Id,
            "Напоминание",
            reminderRef.Text,
            snooze,
            customSnoozeEnabled,
            () => _ = MarkReminderAsNotifiedAsync(reminderRef),
            () => _ = CompleteReminderAfterPopupAsync(reminderRef),
            minutes => _ = SnoozeReminderCoreAsync(reminderRef, minutes),
            context => _ = OpenCustomSnoozeForReminderAsync(reminderRef, context),
            () => _ = HandleReminderDismissedWithoutActionAsync(reminderRef, autoDismissMinutes));
    }

    private async Task HandleReminderDismissedWithoutActionAsync(Reminder reminder, int autoDismissMinutes)
    {
        ClearNotificationPending(reminder.Id);
        await SnoozeReminderUntilAsync(
            reminder,
            DateTime.Now.AddMinutes(autoDismissMinutes),
            string.Empty,
            showNotification: false);
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value) return;
            _inputText = value;
            ParseError = string.Empty;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string ParseError
    {
        get => _parseError;
        private set
        {
            if (_parseError == value) return;
            _parseError = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task OpenCustomSnoozeAsync(ReminderListItemViewModel? item)
    {
        if (item is null || _mainWindow is null)
        {
            return;
        }

        var context = new CustomSnoozeOpenContext(
            CustomSnoozeOpenSource.MainList,
            _mainWindow,
            null,
            null,
            null,
            null,
            null);

        await ShowCustomSnoozeDialogAsync(item.Reminder, context);
    }

    private async Task OpenCustomSnoozeForReminderAsync(Reminder reminder, CustomSnoozeOpenContext openContext)
    {
        if (_mainWindow is null)
        {
            return;
        }

        var context = openContext with { MainWindow = _mainWindow };
        await ShowCustomSnoozeDialogAsync(reminder, context);
    }

    private async Task ShowCustomSnoozeDialogAsync(Reminder reminder, CustomSnoozeOpenContext openContext)
    {
        if (_mainWindow is null)
        {
            return;
        }

        var wnd = new CustomSnoozeWindow(
            _themeService,
            ParseSnoozeAsParseResult,
            reminder.Text,
            openContext,
            _settings.Current.CustomSnoozeWindowPlacement);

        if (wnd.ShowDialog() != true || wnd.SelectedRunAt is null)
        {
            if (openContext.Source == CustomSnoozeOpenSource.Notification)
            {
                await RestoreReminderNotificationAfterCustomSnoozeCancelAsync(reminder);
            }

            return;
        }

        await SnoozeReminderUntilAsync(
            reminder,
            wnd.SelectedRunAt.Value,
            $"Напомню: {wnd.SelectedRunAt.Value:dd.MM.yyyy HH:mm}",
            true);
    }

    private async Task RestoreReminderNotificationAfterCustomSnoozeCancelAsync(Reminder reminder)
    {
        reminder.LastNotifiedAt = null;
        await _storage.SaveAllAsync(_allReminders);
        RefreshActiveList();
        ClearNotificationPending(reminder.Id);
        _notificationPendingIds.Add(reminder.Id);
        RequestReminderNotificationFor(reminder);
    }

    private bool TryParseSnoozeDateTime(string userInput, out DateTime nextRunAt, out string error)
    {
        nextRunAt = default;
        error = SnoozeParseErrorMessage;

        var normalized = SnoozeTextNormalizer.Normalize(userInput);
        var parsed = _parser.Parse(normalized, DateTime.Now);
        if (!parsed.IsSuccess || parsed.NextRunAt is null)
        {
            return false;
        }

        nextRunAt = parsed.NextRunAt.Value;
        return true;
    }

    private ParseResult ParseSnoozeAsParseResult(string userInput)
    {
        if (TryParseSnoozeDateTime(userInput, out var nextRunAt, out _))
        {
            return ParseResult.Success(nextRunAt, string.Empty);
        }

        return ParseResult.Failure(SnoozeParseErrorMessage);
    }
}
