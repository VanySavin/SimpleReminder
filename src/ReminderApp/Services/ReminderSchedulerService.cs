using System.Windows.Threading;
using ReminderApp.Models;

namespace ReminderApp.Services;

public class ReminderSchedulerService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<Task<List<Reminder>>> _loadReminders;
    private readonly SettingsService _settings;
    private bool _isChecking;

    public event EventHandler<Reminder>? ReminderDue;

    public ReminderSchedulerService(Func<Task<List<Reminder>>> loadReminders, SettingsService settings)
    {
        _loadReminders = loadReminders;
        _settings = settings;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.Current.SchedulerIntervalSeconds, 1, 60))
        };
        _timer.Tick += OnTimerTick;
    }

    public void ApplyIntervalFromSettings()
    {
        var wasRunning = _timer.IsEnabled;
        _timer.Stop();
        _timer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.Current.SchedulerIntervalSeconds, 1, 60));
        if (wasRunning)
        {
            _timer.Start();
        }
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isChecking)
        {
            return;
        }

        _isChecking = true;
        try
        {
            var now = DateTime.Now;
            var reminders = await _loadReminders();
            var due = reminders
                .Where(r => !r.IsCompleted
                            && r.NextRunAt <= now
                            && r.LastNotifiedAt == null)
                .OrderBy(r => r.NextRunAt)
                .ToList();

            foreach (var reminder in due)
            {
                ReminderDue?.Invoke(this, reminder);
            }
        }
        finally
        {
            _isChecking = false;
        }
    }

    public void Dispose()
    {
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
    }
}
