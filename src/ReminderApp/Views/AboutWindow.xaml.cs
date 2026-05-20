using System.Reflection;
using System.Windows;
using ReminderApp.Helpers;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class AboutWindow : Window
{
    private readonly ThemeService _themeService;
    private EventHandler? _titleBarThemeChangedHandler;

    public AboutWindow(ThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
        WindowSizeHelper.ApplyAboutWindowStartup(this);
        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLine.Text = $"SimpleReminder · версия {v?.Major ?? 0}.{v?.Minor ?? 0}.{v?.Build ?? 0}";
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        _titleBarThemeChangedHandler = (_, _) =>
            WindowTitleBarThemeHelper.Apply(this, _themeService.IsDarkThemeActive);
        _themeService.ThemeChanged += _titleBarThemeChangedHandler;
        WindowTitleBarThemeHelper.Apply(this, _themeService.IsDarkThemeActive);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        if (_titleBarThemeChangedHandler is not null)
        {
            _themeService.ThemeChanged -= _titleBarThemeChangedHandler;
            _titleBarThemeChangedHandler = null;
        }
    }
}
