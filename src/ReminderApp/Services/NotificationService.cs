using System.Windows;
using System.Diagnostics;
using ReminderApp.Helpers;
using ReminderApp.Views;

namespace ReminderApp.Services;

public sealed class NotificationService
{
    private readonly NotificationSoundService _sound;
    private readonly SettingsService _settings;
    private readonly List<CustomNotificationWindow> _activeReminders = [];
    private readonly Dictionary<string, CustomNotificationWindow> _reminderWindowsById = [];
    private readonly Queue<PendingReminderNotification> _reminderQueue = [];
    private readonly HashSet<string> _queuedReminderIds = [];
    private CustomNotificationWindow? _activeInfoToast;
    private const double EdgeMargin = 12;
    private const double StackGap = 12;
    private const double InfoToastOffset = 80;

    private sealed record PendingReminderNotification(
        string ReminderId,
        string Title,
        string Message,
        IReadOnlyList<int> SnoozeMinutes,
        bool CustomSnoozeEnabled,
        Action OnDisplayed,
        Action OnCompleted,
        Action<int> OnSnooze,
        Action<CustomSnoozeOpenContext> OnCustomSnoozeRequested,
        Action OnDismissWithoutAction);

    public NotificationService(NotificationSoundService sound, SettingsService settings)
    {
        _sound = sound;
        _settings = settings;
    }

    public void ShowInfoToast(string title, string message, bool? playSoundOverride = null)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is null)
        {
            return;
        }

        var playSound = playSoundOverride ?? _settings.Current.PlayNotificationSound;

        if (app.Dispatcher.CheckAccess())
        {
            ShowInfoToastInternal(title, message, playSound);
        }
        else
        {
            app.Dispatcher.BeginInvoke(() => ShowInfoToastInternal(title, message, playSound));
        }
    }

    public void ShowNotification(string title, string message, bool? playSoundOverride = null) =>
        ShowInfoToast(title, message, playSoundOverride);

    public void RequestReminderNotification(
        string reminderId,
        string title,
        string message,
        IReadOnlyList<int> snoozeMinutes,
        bool customSnoozeEnabled,
        Action onDisplayed,
        Action onCompleted,
        Action<int> onSnooze,
        Action<CustomSnoozeOpenContext> onCustomSnoozeRequested,
        Action onDismissWithoutAction)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is null)
        {
            return;
        }

        void Request()
        {
            if (_reminderWindowsById.ContainsKey(reminderId) || _queuedReminderIds.Contains(reminderId))
            {
                return;
            }

            var pending = new PendingReminderNotification(
                reminderId,
                title,
                message,
                snoozeMinutes,
                customSnoozeEnabled,
                onDisplayed,
                onCompleted,
                onSnooze,
                onCustomSnoozeRequested,
                onDismissWithoutAction);

            if (HasFreeReminderSlot())
            {
                ShowReminderWindowInternal(pending);
            }
            else
            {
                _reminderQueue.Enqueue(pending);
                _queuedReminderIds.Add(reminderId);
            }
        }

        if (app.Dispatcher.CheckAccess())
        {
            Request();
        }
        else
        {
            app.Dispatcher.BeginInvoke(Request);
        }
    }

    public void CloseReminderNotification(string reminderId, bool suppressAutoSnooze = true)
    {
        RunOnUiThread(() =>
        {
            if (_reminderWindowsById.TryGetValue(reminderId, out var win))
            {
                if (suppressAutoSnooze)
                {
                    win.CloseSilently();
                }
                else
                {
                    win.BeginAnimatedClose();
                }
            }
        });
    }

    public void RemoveReminderFromQueue(string reminderId)
    {
        RunOnUiThread(() =>
        {
            if (!_queuedReminderIds.Contains(reminderId))
            {
                return;
            }

            _queuedReminderIds.Remove(reminderId);
            if (_reminderQueue.Count == 0)
            {
                return;
            }

            var kept = new Queue<PendingReminderNotification>();
            while (_reminderQueue.Count > 0)
            {
                var item = _reminderQueue.Dequeue();
                if (item.ReminderId != reminderId)
                {
                    kept.Enqueue(item);
                }
            }

            while (kept.Count > 0)
            {
                _reminderQueue.Enqueue(kept.Dequeue());
            }
        });
    }

    public void DismissReminderNotification(string reminderId)
    {
        RunOnUiThread(() =>
        {
            RemoveReminderFromQueue(reminderId);
            CloseReminderNotification(reminderId, suppressAutoSnooze: true);
        });
    }

    public void CloseAllReminders(bool suppressAutoSnooze = true)
    {
        RunOnUiThread(() =>
        {
            _reminderQueue.Clear();
            _queuedReminderIds.Clear();

            var reminders = _activeReminders.ToList();
            foreach (var win in reminders)
            {
                if (suppressAutoSnooze)
                {
                    win.CloseSilently();
                }
                else
                {
                    win.BeginAnimatedClose();
                }
            }
        });
    }

    public void ShowTestNotification() =>
        ShowInfoToast("Напоминание", "Это тестовое уведомление SimpleReminder");

    private void ShowInfoToastInternal(string title, string message, bool playSound)
    {
        if (playSound)
        {
            _sound.PlayNotificationSound();
        }

        if (_activeInfoToast is not null)
        {
            _activeInfoToast.CloseSilently();
            _activeInfoToast = null;
        }

        var win = new CustomNotificationWindow(title, message, _settings.Current.NotificationDurationSeconds);
        RegisterInfoToast(win);
    }

    private void ShowReminderWindowInternal(PendingReminderNotification pending)
    {
        if (_settings.Current.PlayNotificationSound)
        {
            _sound.PlayNotificationSound();
        }

        var win = new CustomNotificationWindow(
            pending.ReminderId,
            pending.Title,
            pending.Message,
            pending.SnoozeMinutes,
            pending.CustomSnoozeEnabled,
            pending.OnCompleted,
            pending.OnSnooze,
            pending.OnCustomSnoozeRequested,
            pending.OnDismissWithoutAction);

        RegisterReminderWindow(win, pending.OnDisplayed);
    }

    private void RegisterInfoToast(CustomNotificationWindow win)
    {
        SizeChangedEventHandler sizeHandler = (_, _) => RepositionAll();
        win.SizeChanged += sizeHandler;

        void OnContentRendered(object? sender, EventArgs e)
        {
            win.ContentRendered -= OnContentRendered;
            RepositionAll();
        }

        win.ContentRendered += OnContentRendered;

        win.Closed += (_, _) =>
        {
            win.SizeChanged -= sizeHandler;
            if (ReferenceEquals(_activeInfoToast, win))
            {
                _activeInfoToast = null;
            }

            RepositionAll();
        };

        _activeInfoToast = win;
        RepositionAll();
        win.Show();
    }

    private void RegisterReminderWindow(CustomNotificationWindow win, Action onDisplayed)
    {
        SizeChangedEventHandler sizeHandler = (_, _) => RepositionAll();
        win.SizeChanged += sizeHandler;
        var displayedNotified = false;

        void NotifyDisplayedOnce()
        {
            if (displayedNotified)
            {
                return;
            }

            displayedNotified = true;
            win.MarkAsDisplayed();
            onDisplayed();
        }

        void OnContentRendered(object? sender, EventArgs e)
        {
            win.ContentRendered -= OnContentRendered;
            NotifyDisplayedOnce();
            RepositionAll();
        }

        win.ContentRendered += OnContentRendered;

        win.Closed += (_, _) =>
        {
            win.SizeChanged -= sizeHandler;
            _activeReminders.Remove(win);
            if (win.ReminderId is not null)
            {
                _reminderWindowsById.Remove(win.ReminderId);
            }

            RepositionAll();
            TryDequeueNext();
        };

        _activeReminders.Add(win);
        if (win.ReminderId is not null)
        {
            _reminderWindowsById[win.ReminderId] = win;
        }

        RepositionAll();
        win.Show();
        NotifyDisplayedOnce();
    }

    private void TryDequeueNext()
    {
        while (_reminderQueue.Count > 0 && HasFreeReminderSlot())
        {
            var next = _reminderQueue.Dequeue();
            _queuedReminderIds.Remove(next.ReminderId);
            ShowReminderWindowInternal(next);
        }
    }

    private bool HasFreeReminderSlot()
    {
        var max = Math.Clamp(_settings.Current.MaxVisibleNotifications, 1, 8);
        return _activeReminders.Count < max;
    }

    private void RepositionAll()
    {
        var placement = _settings.Current.NotificationPlacement;
        var mode = placement.Mode;
        var selectedContext = mode == NotificationPlacementMode.Custom
            ? ScreenPlacementHelper.ResolvePlacementContext(
                placement.ScreenDeviceName,
                placement.CustomLeft,
                placement.CustomTop)
            : ScreenPlacementHelper.ResolvePlacementContext(null);
        var primaryContext = ScreenPlacementHelper.ResolvePlacementContext(null);
        var work = selectedContext.WorkAreaDip;
        var fallbackUsed = selectedContext.UsedPrimaryFallback;
        var customPlacementValid = mode == NotificationPlacementMode.Custom
            && placement.CustomLeft.HasValue
            && placement.CustomTop.HasValue
            && ScreenPlacementHelper.IsValidPoint(placement.CustomLeft.Value, placement.CustomTop.Value);
        if (mode == NotificationPlacementMode.Custom && !customPlacementValid)
        {
            mode = NotificationPlacementMode.BottomRight;
            work = primaryContext.WorkAreaDip;
            fallbackUsed = true;
        }

        var fromTop = mode is NotificationPlacementMode.TopLeft or NotificationPlacementMode.TopRight;
        var nextY = fromTop ? work.Top + EdgeMargin : work.Bottom - EdgeMargin;
        if (mode == NotificationPlacementMode.Custom && customPlacementValid)
        {
            fromTop = true;
            nextY = placement.CustomTop!.Value;
        }

        foreach (var w in _activeReminders)
        {
            if (!w.IsLoaded || w.ActualHeight <= 0 || w.ActualWidth <= 0)
            {
                continue;
            }

            var left = ResolveLeft(mode, work, w.ActualWidth, placement.CustomLeft);
            var top = fromTop ? nextY : nextY - w.ActualHeight;
            var localFallback = fallbackUsed;
            var targetWork = work;

            if (ScreenPlacementHelper.IsFullyOutsideAllScreens(left, top, w.ActualWidth, w.ActualHeight))
            {
                localFallback = true;
                targetWork = primaryContext.WorkAreaDip;
                (left, top) = ScreenPlacementHelper.BottomRight(targetWork, w.ActualWidth, w.ActualHeight, EdgeMargin);
            }
            else
            {
                (left, top) = ScreenPlacementHelper.ClampToWorkArea(left, top, w.ActualWidth, w.ActualHeight, targetWork, EdgeMargin);
            }

            Debug.WriteLine(
                $"[NotificationPlacement] mode={placement.Mode}, calcMode={mode}, screen={selectedContext.DeviceName ?? "null"}, " +
                $"work=({targetWork.Left:F2},{targetWork.Top:F2},{targetWork.Width:F2},{targetWork.Height:F2}), " +
                $"dpi=({selectedContext.DpiScaleX:F2},{selectedContext.DpiScaleY:F2}), left={left:F2}, top={top:F2}, " +
                $"size=({w.ActualWidth:F2},{w.ActualHeight:F2}), fallback={localFallback}");

            w.Left = left;
            w.Top = top;
            nextY = fromTop ? top + w.ActualHeight + StackGap : top - StackGap;
        }

        RepositionInfoToast(mode, work, placement, primaryContext, fallbackUsed, fromTop, nextY);
    }

    private void RepositionInfoToast(
        NotificationPlacementMode mode,
        Rect work,
        NotificationPlacementSettings placement,
        ScreenPlacementContext primaryContext,
        bool fallbackUsed,
        bool fromTop,
        double reminderStackEdgeY)
    {
        if (_activeInfoToast is null || !_activeInfoToast.IsLoaded
            || _activeInfoToast.ActualHeight <= 0 || _activeInfoToast.ActualWidth <= 0)
        {
            return;
        }

        var toast = _activeInfoToast;
        var left = ResolveLeft(mode, work, toast.ActualWidth, placement.CustomLeft);
        var top = fromTop
            ? reminderStackEdgeY + InfoToastOffset
            : reminderStackEdgeY - toast.ActualHeight - InfoToastOffset;
        var targetWork = work;

        if (ScreenPlacementHelper.IsFullyOutsideAllScreens(left, top, toast.ActualWidth, toast.ActualHeight))
        {
            targetWork = primaryContext.WorkAreaDip;
            (left, top) = ScreenPlacementHelper.BottomRight(
                targetWork,
                toast.ActualWidth,
                toast.ActualHeight,
                EdgeMargin);
        }
        else
        {
            (left, top) = ScreenPlacementHelper.ClampToWorkArea(
                left, top, toast.ActualWidth, toast.ActualHeight, targetWork, EdgeMargin);
        }

        toast.Left = left;
        toast.Top = top;
    }

    private static double ResolveLeft(NotificationPlacementMode mode, Rect work, double width, double? customLeft)
    {
        return mode switch
        {
            NotificationPlacementMode.TopLeft or NotificationPlacementMode.BottomLeft => work.Left + EdgeMargin,
            NotificationPlacementMode.BottomCenter => work.Left + (work.Width - width) / 2,
            NotificationPlacementMode.Custom when customLeft.HasValue => customLeft.Value,
            _ => work.Right - EdgeMargin - width
        };
    }

    private void RunOnUiThread(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.BeginInvoke(action);
        }
    }
}
