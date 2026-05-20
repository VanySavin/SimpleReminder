using System.ComponentModel;
using System.Windows;
using ReminderApp.Helpers;
using ReminderApp.Services;
using ReminderApp.ViewModels;

namespace ReminderApp;

public partial class MainWindow : Window
{
    private ThemeService? _titleBarThemeService;
    private EventHandler? _titleBarThemeChangedHandler;

    public MainWindow()
    {
        InitializeComponent();
        WindowSizeHelper.ApplyMainWindowStartup(this);
        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        if (System.Windows.Application.Current is not App app || app.ThemeService is not { } ts)
        {
            return;
        }

        _titleBarThemeService = ts;
        _titleBarThemeChangedHandler = (_, _) =>
            WindowTitleBarThemeHelper.Apply(this, ts.IsDarkThemeActive);
        ts.ThemeChanged += _titleBarThemeChangedHandler;
        WindowTitleBarThemeHelper.Apply(this, ts.IsDarkThemeActive);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        if (_titleBarThemeService is not null && _titleBarThemeChangedHandler is not null)
        {
            _titleBarThemeService.ThemeChanged -= _titleBarThemeChangedHandler;
            _titleBarThemeChangedHandler = null;
            _titleBarThemeService = null;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is MainViewModel vm)
        {
            vm.AttachFocusInput(() =>
            {
                InputBox.Focus();
                System.Windows.Input.Keyboard.Focus(InputBox);
            });
        }

        if (System.Windows.Application.Current is App { SettingsService: { } ss } && ss.Current.AutoFocusInputOnStart)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    InputBox.Focus();
                    System.Windows.Input.Keyboard.Focus(InputBox);
                },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private void InputBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.None)
        {
            return;
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.SubmitReminderOnEnter)
        {
            return;
        }

        e.Handled = true;

        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            return;
        }

        vm.AddReminderCommand.Execute(null);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized
            && System.Windows.Application.Current is App app
            && !app.IsExitRequested)
        {
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            PersistPlacementToSettings();
        }

        if (DataContext is MainViewModel vm && !vm.MinimizeToTrayOnClose)
        {
            e.Cancel = false;
            base.OnClosing(e);
            return;
        }

        if (System.Windows.Application.Current is App app && app.RequestHideToTray(this))
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private void PersistPlacementToSettings()
    {
        if (System.Windows.Application.Current is not App { SettingsService: { } settings })
        {
            return;
        }

        if (WindowState == WindowState.Minimized)
        {
            return;
        }

        settings.Current.MainWindowPlacement = WindowPlacementHelper.CaptureMainWindowPlacement(this);
    }
}
