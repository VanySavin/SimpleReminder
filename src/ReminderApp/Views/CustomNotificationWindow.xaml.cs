using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WpfButton = System.Windows.Controls.Button;
using ReminderApp.Helpers;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class CustomNotificationWindow : Window
{
    public string TitleText { get; }
    public string MessageText { get; }
    public string? ReminderId { get; }
    public NotificationWindowKind Kind { get; }

    private readonly bool _isSimpleAutoClose;
    private readonly int _autoCloseSeconds;
    private readonly Action? _onCompleted;
    private readonly Action<int>? _onSnoozeMinutes;
    private readonly Action? _onDismissWithoutAction;
    private readonly Action<CustomSnoozeOpenContext>? _onCustomSnoozeRequested;
    private readonly bool _customSnoozeEnabled;
    private readonly IReadOnlyList<int> _snoozeMinutes;
    private bool _closingAnimated;
    private bool _suppressDismissCallback;
    private bool _wasDisplayed;
    private NotificationFinalAction _finalAction = NotificationFinalAction.None;
    private EventHandler? _outroCompletedHandler;
    private bool _styleInitialized;

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;

    private enum NotificationFinalAction
    {
        None,
        Completed,
        Snoozed,
        CustomSnoozeRequested,
        DismissedNoAction
    }

    /// <summary>Короткий info-тост с таймером закрытия.</summary>
    public CustomNotificationWindow(string titleText, string messageText, int autoCloseSeconds)
    {
        TitleText = titleText;
        MessageText = messageText;
        Kind = NotificationWindowKind.InfoToast;
        ReminderId = null;
        _isSimpleAutoClose = true;
        _autoCloseSeconds = Math.Clamp(autoCloseSeconds, 2, 30);
        _snoozeMinutes = [];
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    /// <summary>Напоминание с кнопками «Выполнено» и отложить (до трёх вариантов).</summary>
    public CustomNotificationWindow(
        string reminderId,
        string titleText,
        string messageText,
        IReadOnlyList<int> snoozeMinutes,
        bool customSnoozeEnabled,
        Action onCompleted,
        Action<int> onSnoozeMinutes,
        Action<CustomSnoozeOpenContext> onCustomSnoozeRequested,
        Action onDismissWithoutAction)
    {
        ReminderId = reminderId;
        TitleText = titleText;
        MessageText = messageText;
        Kind = NotificationWindowKind.ReminderNotification;
        _isSimpleAutoClose = false;
        _autoCloseSeconds = 5;
        _onCompleted = onCompleted;
        _onSnoozeMinutes = onSnoozeMinutes;
        _onCustomSnoozeRequested = onCustomSnoozeRequested;
        _onDismissWithoutAction = onDismissWithoutAction;
        _customSnoozeEnabled = customSnoozeEnabled;
        _snoozeMinutes = snoozeMinutes.Take(3).ToList();
        InitializeComponent();
        DataContext = this;
        ActionsPanel.Visibility = Visibility.Visible;
        ProgressRow.Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;
    }

    public void MarkAsDisplayed() => _wasDisplayed = true;

    public void CloseSilently()
    {
        _suppressDismissCallback = true;
        BeginAnimatedClose();
    }

    private void BuildSnoozeButtons()
    {
        SnoozeButtonsPanel.Children.Clear();
        foreach (var m in _snoozeMinutes)
        {
            var btn = new WpfButton
            {
                Content = SnoozeLabelFormatter.FormatCompact(m),
                Margin = new Thickness(0, 0, 8, 4),
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = m,
                BorderThickness = new Thickness(1)
            };
            btn.SetResourceReference(WpfButton.BackgroundProperty, "NotificationSecondaryButtonBgBrush");
            btn.SetResourceReference(WpfButton.ForegroundProperty, "NotificationSecondaryButtonFgBrush");
            btn.SetResourceReference(WpfButton.BorderBrushProperty, "NotificationSecondaryButtonBorderBrush");
            btn.Click += SnoozeMinute_OnClick;
            SnoozeButtonsPanel.Children.Add(btn);
        }

        CustomSnoozeToggleButton.Visibility = _customSnoozeEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SnoozeMinute_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton b || b.Tag is not int minutes)
        {
            return;
        }

        if (!TryFinalize(NotificationFinalAction.Snoozed))
        {
            return;
        }

        _onSnoozeMinutes?.Invoke(minutes);
        BeginAnimatedClose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        EnsureNoActivateStyle();
        if (!_isSimpleAutoClose)
        {
            BuildSnoozeButtons();
        }

        if (Resources["IntroStoryboard"] is Storyboard intro)
        {
            if (_isSimpleAutoClose)
            {
                intro.Completed += (_, _) => StartAutoCloseSequence();
            }

            intro.Begin(this);
        }
        else
        {
            RootBorder.Opacity = 1;
            if (_isSimpleAutoClose)
            {
                StartAutoCloseSequence();
            }
        }
    }

    private void StartAutoCloseSequence()
    {
        if (Resources["ProgressStoryboard"] is not Storyboard progress)
        {
            Dispatcher.BeginInvoke(BeginAnimatedClose);
            return;
        }

        var dur = new Duration(TimeSpan.FromSeconds(_autoCloseSeconds));
        progress.Duration = dur;
        foreach (var t in progress.Children)
        {
            if (t is DoubleAnimation da)
            {
                da.Duration = dur;
            }
        }

        progress.Completed += Progress_OnCompleted;
        progress.Begin(this, true);
    }

    private void Progress_OnCompleted(object? sender, EventArgs e)
    {
        if (sender is Storyboard sb)
        {
            sb.Completed -= Progress_OnCompleted;
        }

        BeginAnimatedClose();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isSimpleAutoClose)
        {
            CloseSilently();
            return;
        }

        CloseAsDismissWithoutAction();
    }

    private void Done_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryFinalize(NotificationFinalAction.Completed))
        {
            return;
        }

        _onCompleted?.Invoke();
        BeginAnimatedClose();
    }

    private void CustomSnoozeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryFinalize(NotificationFinalAction.CustomSnoozeRequested))
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : Math.Max(Width, MinWidth);
        var height = ActualHeight > 0 ? ActualHeight : Math.Max(Height, MinHeight);
        string? screenDeviceName = null;
        try
        {
            screenDeviceName = ScreenPlacementHelper.ResolveScreenDeviceNameByWindowRect(Left, Top, width, height);
        }
        catch
        {
            // ignore
        }

        _onCustomSnoozeRequested?.Invoke(new CustomSnoozeOpenContext(
            CustomSnoozeOpenSource.Notification,
            null,
            Left,
            Top,
            width,
            height,
            screenDeviceName));
        BeginAnimatedClose();
    }

    public void BeginAnimatedClose()
    {
        if (_closingAnimated)
        {
            return;
        }

        _closingAnimated = true;
        if (Resources["ProgressStoryboard"] is Storyboard progress)
        {
            progress.Completed -= Progress_OnCompleted;
            progress.Stop(this);
        }

        if (Resources["OutroStoryboard"] is Storyboard outro)
        {
            if (_outroCompletedHandler is not null)
            {
                outro.Completed -= _outroCompletedHandler;
            }

            _outroCompletedHandler = (_, _) =>
            {
                if (_outroCompletedHandler is not null)
                {
                    outro.Completed -= _outroCompletedHandler;
                    _outroCompletedHandler = null;
                }

                try { Close(); } catch { }
            };
            outro.Completed += _outroCompletedHandler;
            outro.Begin(this, true);
        }
        else
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ShouldInvokeDismissWithoutAction())
        {
            _finalAction = NotificationFinalAction.DismissedNoAction;
            _onDismissWithoutAction?.Invoke();
        }

        base.OnClosed(e);
    }

    private bool ShouldInvokeDismissWithoutAction() =>
        Kind == NotificationWindowKind.ReminderNotification
        && !_suppressDismissCallback
        && _wasDisplayed
        && _finalAction == NotificationFinalAction.None;

    private void CloseAsDismissWithoutAction()
    {
        if (!TryFinalize(NotificationFinalAction.DismissedNoAction))
        {
            return;
        }

        _onDismissWithoutAction?.Invoke();
        BeginAnimatedClose();
    }

    private bool TryFinalize(NotificationFinalAction action)
    {
        if (_finalAction != NotificationFinalAction.None)
        {
            return false;
        }

        _finalAction = action;
        return true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnsureNoActivateStyle();
    }

    private void EnsureNoActivateStyle()
    {
        if (_styleInitialized)
        {
            return;
        }

        _styleInitialized = true;
        try
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source?.Handle is null || source.Handle == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLongPtr(source.Handle, GwlExStyle).ToInt64();
            SetWindowLongPtr(source.Handle, GwlExStyle, new IntPtr(style | WsExNoActivate));
            SetWindowPos(source.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize | SwpNoZOrder);
        }
        catch
        {
            // fallback: ShowActivated=false remains active.
        }
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
}
