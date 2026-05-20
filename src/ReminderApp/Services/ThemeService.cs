using Microsoft.Win32;
using WpfApp = System.Windows.Application;
using WpfResourceDictionary = System.Windows.ResourceDictionary;

namespace ReminderApp.Services;

public sealed class ThemeService : IDisposable
{
    private readonly SettingsService _settingsService;
    private WpfResourceDictionary? _themeDictionary;
    private bool _systemEventsHooked;
    private bool _disposed;

    public ThemeService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ThemeMode GetCurrentMode() => _settingsService.Current.ThemeMode;

    /// <summary>
    /// Фактическая тёмная/светлая тема UI с учётом режима Auto и системы Windows.
    /// </summary>
    public bool IsDarkThemeActive => ResolveIsDarkThemeActive();

    /// <summary>
    /// Вызывается после смены словаря темы (включая реакцию на системную тему в режиме Auto).
    /// </summary>
    public event EventHandler? ThemeChanged;

    public void InitializeFromCurrentSettings()
    {
        ApplyResolvedTheme();
        UpdateSystemEventsSubscription();
    }

    private void UpdateSystemEventsSubscription()
    {
        if (_settingsService.Current.ThemeMode == ThemeMode.Auto)
        {
            if (!_systemEventsHooked)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _systemEventsHooked = true;
            }
        }
        else if (_systemEventsHooked)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _systemEventsHooked = false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_settingsService.Current.ThemeMode != ThemeMode.Auto)
        {
            return;
        }

        var app = WpfApp.Current;
        if (app?.Dispatcher is null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            ApplyResolvedTheme();
        }
        else
        {
            app.Dispatcher.BeginInvoke(new Action(ApplyResolvedTheme));
        }
    }

    public static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                writable: false);
            var raw = key?.GetValue("AppsUseLightTheme");
            return raw is int v ? v == 1 : true;
        }
        catch
        {
            return true;
        }
    }

    private bool ResolveIsDarkThemeActive()
    {
        var mode = _settingsService.Current.ThemeMode;
        return mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            ThemeMode.Auto => !IsWindowsLightTheme(),
            _ => false
        };
    }

    private void ApplyResolvedTheme()
    {
        var useDark = ResolveIsDarkThemeActive();

        var app = WpfApp.Current;
        if (app is null)
        {
            return;
        }

        var path = useDark
            ? "pack://application:,,,/SimpleReminder;component/Themes/DarkTheme.xaml"
            : "pack://application:,,,/SimpleReminder;component/Themes/LightTheme.xaml";

        var source = new Uri(path, UriKind.Absolute);

        if (_themeDictionary is not null)
        {
            app.Resources.MergedDictionaries.Remove(_themeDictionary);
            _themeDictionary = null;
        }

        _themeDictionary = new WpfResourceDictionary { Source = source };
        app.Resources.MergedDictionaries.Insert(0, _themeDictionary);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_systemEventsHooked)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _systemEventsHooked = false;
        }
    }
}
