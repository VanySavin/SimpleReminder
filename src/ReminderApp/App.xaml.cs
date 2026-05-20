using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ReminderApp.Helpers;
using ReminderApp.Services;
using ReminderApp.ViewModels;

namespace ReminderApp;

public partial class App : System.Windows.Application
{
    private TrayService? _trayService;
    private NotificationSoundService? _notificationSoundService;
    private NotificationService? _notificationService;
    private SettingsService? _settingsService;
    private ThemeService? _themeService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;
    private SingleInstanceService? _singleInstanceService;

    private bool _cmdLineAutostart;
    private bool _cmdLineMinimized;

    private static bool _trayHintShownInSession;

    public bool IsExitRequested { get; private set; }

    public SettingsService? SettingsService => _settingsService;

    public ThemeService? ThemeService => _themeService;

    /// <summary>Не показывать подсказку «работает в трее» при старте с --autostart --minimized.</summary>
    public bool SuppressTrayNoticeForAutostartMinimized =>
        _cmdLineAutostart && _cmdLineMinimized;

    private bool SuppressStartupModals =>
        _cmdLineAutostart && _cmdLineMinimized;

    public App()
    {
        SessionEnding += OnSessionEnding;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledExceptionNonUi;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ParseStartupCommandLine(e.Args, out _cmdLineAutostart, out _cmdLineMinimized);
        StartupDiagnostics.TracePhase(
            "Аргументы командной строки",
            $"--autostart={_cmdLineAutostart}, --minimized={_cmdLineMinimized}");

        if (!SingleInstanceService.TryEnterAsPrimaryOrExitSecondary(
                _cmdLineAutostart,
                _cmdLineMinimized,
                Dispatcher,
                OnSecondInstanceShowMainWindowRequest,
                out _singleInstanceService))
        {
            Shutdown(0);
            return;
        }

        try
        {

            StartupDiagnostics.TracePhase("Старт", PortablePaths.ApplicationBaseDirectory);

            StartupDiagnostics.TracePhase("Создание папок data и data\\sounds");
            try
            {
                PortablePaths.EnsurePortableLayout();
            }
            catch (Exception ex)
            {
                StartupDiagnostics.TraceException("EnsurePortableLayout", ex);
                StartupDiagnostics.ShowStartupError(
                    "SimpleReminder — ошибка запуска",
                    "Не удалось создать папку data или data\\sounds рядом с программой.\n\n" +
                    StartupDiagnostics.FormatPathsFooter(),
                    ex);
                Shutdown(1);
                return;
            }

            _settingsService = new SettingsService();
            StartupDiagnostics.TracePhase("Загрузка settings.json");
            var settingsWarning = await _settingsService.LoadAsync();

            var autoStartService = new AutoStartService();
            StartupDiagnostics.TracePhase("Синхронизация автозапуска с настройками");
            var autoStartSync = await autoStartService.EnsureAutoStartMatchesSettingsAsync(_settingsService.Current);
            if (!autoStartSync.Success)
            {
                StartupDiagnostics.TracePhase(
                    "Автозапуск: не удалось синхронизировать ярлык",
                    autoStartSync.ErrorMessage ?? string.Empty);
            }

            _themeService = new ThemeService(_settingsService);
            StartupDiagnostics.TracePhase("Инициализация темы");
            try
            {
                _themeService.InitializeFromCurrentSettings();
            }
            catch (Exception ex)
            {
                StartupDiagnostics.TraceException("ThemeService.InitializeFromCurrentSettings", ex);
                StartupDiagnostics.ShowStartupError(
                    "SimpleReminder — ошибка запуска",
                    "Не удалось применить тему оформления.", ex);
                DisposePartialStartup();
                Shutdown(1);
                return;
            }

            StartupDiagnostics.TracePhase("Иконка в трее");
            try
            {
                _trayService = new TrayService();
            }
            catch (Exception ex)
            {
                StartupDiagnostics.TraceException("TrayService", ex);
                StartupDiagnostics.ShowStartupError(
                    "SimpleReminder — ошибка запуска",
                    "Не удалось создать значок в области уведомлений.", ex);
                DisposePartialStartup();
                Shutdown(1);
                return;
            }

            _notificationSoundService = new NotificationSoundService();
            _notificationService = new NotificationService(_notificationSoundService, _settingsService);

            var storage = new ReminderStorageService();
            var parser = new ReminderTextParser();
            var scheduler = new ReminderSchedulerService(storage.LoadAllAsync, _settingsService);

            _mainViewModel = new MainViewModel(storage, parser, scheduler, _notificationService, _settingsService, _themeService);
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };
            _mainViewModel.AttachMainWindow(_mainWindow);

            WindowPlacementHelper.ApplyMainWindowPlacement(
                _mainWindow,
                _settingsService.Current.MainWindowPlacement,
                useFallbackWhenInvalid: !_cmdLineMinimized);

            _mainWindow.Closed += (_, _) =>
            {
                if (IsExitRequested)
                {
                    return;
                }

                if (!_settingsService!.Current.MinimizeToTrayOnClose)
                {
                    IsExitRequested = true;
                    _trayService?.Dispose();
                    _themeService?.Dispose();
                    Shutdown();
                }
            };

            _trayService.OpenClicked += (_, _) => ShowMainWindow();
            _trayService.TestNotificationClicked += (_, _) => _notificationService.ShowTestNotification();
            _trayService.ExitClicked += (_, _) => ExitApplication();

            StartupDiagnostics.TracePhase("Загрузка напоминаний и планировщик");
            await _mainViewModel.InitializeAsync();

            var combinedWarnings = CombineWarnings(settingsWarning, _mainViewModel.StartupDataWarning);
            if (combinedWarnings is not null && !SuppressStartupModals)
            {
                StartupDiagnostics.ShowStartupWarning(combinedWarnings);
            }
            else if (combinedWarnings is not null)
            {
                StartupDiagnostics.TracePhase("Startup warning (suppressed)", combinedWarnings);
            }

            if (_cmdLineMinimized)
            {
                StartupDiagnostics.TracePhase("Главное окно", "скрыто (запуск в трей)");
                _mainWindow.ShowInTaskbar = false;
            }
            else
            {
                StartupDiagnostics.TracePhase("Главное окно");
                _mainWindow.ShowInTaskbar = true;
                _mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.TraceException("Критическая ошибка запуска", ex);
            StartupDiagnostics.ShowStartupError(
                "SimpleReminder — ошибка запуска",
                "Приложение не смогло полностью инициализироваться.\n\n" +
                StartupDiagnostics.FormatPathsFooter(),
                ex);
            DisposePartialStartup();
            Shutdown(1);
        }
    }

    private static string? CombineWarnings(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
        {
            return string.IsNullOrWhiteSpace(b) ? null : b;
        }

        if (string.IsNullOrWhiteSpace(b))
        {
            return a;
        }

        return a + "\n\n────────────\n\n" + b;
    }

    private static void ParseStartupCommandLine(string[]? args, out bool autostart, out bool minimized)
    {
        autostart = false;
        minimized = false;
        if (args is null || args.Length == 0)
        {
            return;
        }

        foreach (var a in args)
        {
            if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase))
            {
                autostart = true;
            }
            else if (string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                minimized = true;
            }
        }
    }

    private void OnSecondInstanceShowMainWindowRequest()
    {
        if (IsExitRequested)
        {
            return;
        }

        ShowMainWindow();
        _mainViewModel?.RequestFocusInputDeferred();
    }

    private void DisposePartialStartup()
    {
        try
        {
            _trayService?.Dispose();
        }
        catch
        {
            // ignore
        }

        _trayService = null;

        try
        {
            _themeService?.Dispose();
        }
        catch
        {
            // ignore
        }

        _themeService = null;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.TraceException("Необработанное исключение (UI)", e.Exception);
        e.Handled = true;
        try
        {
            StartupDiagnostics.ShowStartupError(
                "SimpleReminder — ошибка",
                "Произошла необработанная ошибка в интерфейсе. Сообщение записано в отладочный вывод (Debug).",
                e.Exception);
        }
        catch
        {
            // окно сообщения недоступно
        }

        try
        {
            Shutdown(1);
        }
        catch
        {
            // ignore
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        foreach (var inner in e.Exception.InnerExceptions)
        {
            StartupDiagnostics.TraceException("Неотслеженная задача (Task)", inner);
        }
    }

    private static void OnUnhandledExceptionNonUi(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        var label = e.IsTerminating
            ? "Фатальная ошибка (завершение процесса)"
            : "Необработанное исключение (не UI-поток)";
        StartupDiagnostics.TraceException(label, ex);
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e) =>
        SaveSettingsBestEffort();

    private void SaveSettingsBestEffort()
    {
        try
        {
            _settingsService?.TrySaveCurrentOnShutdown();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.TraceException("SaveSettingsBestEffort", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SaveSettingsBestEffort();

        try
        {
            _singleInstanceService?.Dispose();
        }
        catch
        {
            // ignore
        }

        _singleInstanceService = null;

        try
        {
            _trayService?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            _notificationService?.CloseAllReminders(suppressAutoSnooze: true);
        }
        catch
        {
            // ignore
        }

        _themeService?.Dispose();
        base.OnExit(e);
    }

    public bool RequestHideToTray(Window window)
    {
        if (IsExitRequested)
        {
            return false;
        }

        window.Hide();
        if (_settingsService!.Current.ShowTrayNoticeOncePerSession
            && !_trayHintShownInSession
            && !SuppressTrayNoticeForAutostartMinimized)
        {
            _trayHintShownInSession = true;
            _notificationService?.ShowInfoToast(
                "SimpleReminder",
                "Приложение продолжает работать в трее");
        }

        return true;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = true;
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        IsExitRequested = true;
        try
        {
            _notificationService?.CloseAllReminders(suppressAutoSnooze: true);
        }
        catch
        {
            // ignore
        }

        _trayService?.Dispose();
        _themeService?.Dispose();
        _mainWindow?.Close();
        Shutdown();
    }
}
