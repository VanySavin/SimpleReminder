using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ReminderApp.Helpers;
using ReminderApp.Services;
using ReminderApp.ViewModels;

namespace ReminderApp.Views;

public partial class SettingsWindow : Window
{
    private readonly ThemeService _themeService;
    private EventHandler? _titleBarThemeChangedHandler;

    public SettingsWindow(SettingsViewModel vm, ThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
        WindowSizeHelper.ApplySettingsWindowStartup(this);
        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;
        DataContext = vm;
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        AboutVersion.Text = $"SimpleReminder · версия {v?.Major ?? 0}.{v?.Minor ?? 0}.{v?.Build ?? 0}";
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

    private async void Save_OnClick(object sender, RoutedEventArgs e) => await SaveCoreAsync();

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void SettingsWindow_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.None)
        {
            return;
        }

        if (AnyComboBoxDropDownOpen())
        {
            return;
        }

        var focused = System.Windows.Input.Keyboard.FocusedElement;

        switch (focused)
        {
            case System.Windows.Controls.TextBox tb when ReferenceEquals(tb, Snooze1TextBox) || ReferenceEquals(tb, Snooze2TextBox) || ReferenceEquals(tb, Snooze3TextBox):
                tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                break;
            case System.Windows.Controls.ComboBox cb when (ReferenceEquals(cb, ThemeComboBox) || ReferenceEquals(cb, PlacementComboBox)) && !cb.IsDropDownOpen:
                break;
            default:
                return;
        }

        e.Handled = true;
        await SaveCoreAsync();
    }

    private static bool AnyComboBoxDropDownOpen(DependencyObject root)
    {
        foreach (var cb in FindVisualChildren<System.Windows.Controls.ComboBox>(root))
        {
            if (cb.IsDropDownOpen)
            {
                return true;
            }
        }

        return false;
    }

    private bool AnyComboBoxDropDownOpen() => AnyComboBoxDropDownOpen(this);

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj)
        where T : DependencyObject
    {
        if (depObj is null)
        {
            yield break;
        }

        var count = VisualTreeHelper.GetChildrenCount(depObj);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private void FlushSnoozeBindings()
    {
        Snooze1TextBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        Snooze2TextBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        Snooze3TextBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
    }

    private async Task SaveCoreAsync()
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        FlushSnoozeBindings();

        var (completed, autoStartError, saveError) = await vm.TrySaveAsync();
        if (!completed)
        {
            if (!string.IsNullOrWhiteSpace(saveError))
            {
                System.Windows.MessageBox.Show(
                    saveError,
                    "SimpleReminder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (!string.IsNullOrWhiteSpace(autoStartError))
            {
                System.Windows.MessageBox.Show(
                    autoStartError,
                    "Автозапуск",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return;
        }

        DialogResult = true;
        Close();
    }

    private void PickNotificationPlacement_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        var pickerWidth = 340d;
        var pickerHeight = 180d;
        var initialLeft = Left + 20;
        var initialTop = Top + 20;
        var custom = vm.GetCustomPlacement();
        if (vm.SelectedPlacement?.Mode == NotificationPlacementMode.Custom &&
            custom.Left.HasValue &&
            custom.Top.HasValue &&
            ScreenPlacementHelper.IsValidPoint(custom.Left.Value, custom.Top.Value) &&
            !ScreenPlacementHelper.IsFullyOutsideAllScreens(custom.Left.Value, custom.Top.Value, pickerWidth, pickerHeight))
        {
            initialLeft = custom.Left.Value;
            initialTop = custom.Top.Value;
        }
        else
        {
            var primary = ScreenPlacementHelper.ResolvePlacementContext(null);
            (initialLeft, initialTop) = ScreenPlacementHelper.BottomRight(primary.WorkAreaDip, pickerWidth, pickerHeight, 12);
        }

        var picker = new NotificationPlacementPickerWindow
        {
            Owner = this,
            Left = initialLeft,
            Top = initialTop
        };

        if (picker.ShowDialog() == true)
        {
            vm.ApplyCustomPlacement(picker.SelectedLeft, picker.SelectedTop, picker.SelectedScreenDeviceName);
        }
    }

    private void ResetNotificationPlacement_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.ResetNotificationPlacement();
    }

    private void PickCustomSnoozePlacement_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        const double pickerWidth = 420d;
        const double pickerHeight = 240d;
        var initialLeft = Left + 20;
        var initialTop = Top + 20;
        var custom = vm.GetCustomSnoozeWindowPlacement();
        if (vm.SelectedSnoozeWindowPlacement?.Mode == CustomSnoozeWindowPlacementMode.Custom &&
            custom.Left.HasValue &&
            custom.Top.HasValue &&
            ScreenPlacementHelper.IsValidPoint(custom.Left.Value, custom.Top.Value) &&
            !ScreenPlacementHelper.IsFullyOutsideAllScreens(custom.Left.Value, custom.Top.Value, pickerWidth, pickerHeight))
        {
            initialLeft = custom.Left.Value;
            initialTop = custom.Top.Value;
        }
        else
        {
            var primary = ScreenPlacementHelper.ResolvePlacementContext(null);
            (initialLeft, initialTop) = ScreenPlacementHelper.BottomRight(primary.WorkAreaDip, pickerWidth, pickerHeight, 12);
        }

        var picker = new CustomSnoozePlacementPickerWindow
        {
            Owner = this,
            Left = initialLeft,
            Top = initialTop
        };

        if (picker.ShowDialog() == true)
        {
            vm.ApplyCustomSnoozeWindowPlacement(picker.SelectedLeft, picker.SelectedTop, picker.SelectedScreenDeviceName);
        }
    }

    private void ResetCustomSnoozePlacement_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.ResetCustomSnoozeWindowPlacement();
    }
}
