using System.Windows;
using ReminderApp.Helpers;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class CustomSnoozeWindow : Window
{
    private readonly ThemeService _themeService;
    private readonly Func<string, ParseResult> _parser;
    private EventHandler? _titleBarThemeChangedHandler;

    public CustomSnoozeWindow(
        ThemeService themeService,
        Func<string, ParseResult> parser,
        string reminderText,
        CustomSnoozeOpenContext openContext,
        CustomSnoozeWindowPlacementSettings placementSettings)
    {
        InitializeComponent();
        _themeService = themeService;
        _parser = parser;
        ReminderTextBlock.Text = string.IsNullOrWhiteSpace(reminderText) ? "—" : reminderText.Trim();

        WindowSizeHelper.ApplyCustomSnoozeWindowStartup(this);

        if (openContext.MainWindow is { IsVisible: true } mainWindow)
        {
            Owner = mainWindow;
        }

        var (left, top, _) = WindowPlacementHelper.ComputeCustomSnoozePosition(
            placementSettings,
            openContext,
            Width,
            Height);
        WindowPlacementHelper.ApplyManualPosition(this, left, top, Width, Height, null);

        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;
        Loaded += OnWindowLoaded;
        Activated += OnWindowActivated;
        ContentRendered += OnContentRendered;
    }

    public DateTime? SelectedRunAt { get; private set; }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        FocusInputField();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        Topmost = true;
        Activate();
        FocusInputField();
        Dispatcher.BeginInvoke(
            () => Topmost = false,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        FocusInputField();
    }

    private void FocusInputField()
    {
        SnoozeInputTextBox.Focus();
        System.Windows.Input.Keyboard.Focus(SnoozeInputTextBox);
        SnoozeInputTextBox.CaretIndex = SnoozeInputTextBox.Text.Length;
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
        Activated -= OnWindowActivated;
        if (_titleBarThemeChangedHandler is not null)
        {
            _themeService.ThemeChanged -= _titleBarThemeChangedHandler;
            _titleBarThemeChangedHandler = null;
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SnoozeButton_OnClick(object sender, RoutedEventArgs e) => TrySubmit();

    private void CustomSnoozeWindow_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            TrySubmit();
        }
    }

    private void TrySubmit()
    {
        var input = SnoozeInputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            ShowValidationError("Напишите срок отложения");
            return;
        }

        var parsed = _parser(input);
        if (!parsed.IsSuccess || parsed.NextRunAt is null)
        {
            ShowValidationError(string.IsNullOrWhiteSpace(parsed.ErrorMessage)
                ? "Не понял срок. Пример: на 5 минут, завтра в 10, через месяц"
                : parsed.ErrorMessage);
            return;
        }

        SelectedRunAt = parsed.NextRunAt.Value;
        DialogResult = true;
        Close();
    }

    private void ShowValidationError(string text)
    {
        ErrorTextBlock.Text = text;
        ErrorTextBlock.Visibility = Visibility.Visible;
        FocusInputField();
        SnoozeInputTextBox.SelectAll();
    }

    private void SnoozeInputTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ErrorTextBlock.Visibility == Visibility.Visible)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = string.Empty;
        }
    }
}
