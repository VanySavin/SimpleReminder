using ReminderApp.Helpers;

namespace ReminderApp.Models;

public class ReminderListItemViewModel
{
    public required Reminder Reminder { get; init; }
    public string Text => Reminder.Text;

    public List<SnoozeSlotRow> SnoozeSlots { get; private set; } = [];

    public void RebuildSnoozeSlots(IReadOnlyList<int> minutes)
    {
        SnoozeSlots = minutes
            .Take(3)
            .Select(m => new SnoozeSlotRow
            {
                Item = this,
                Minutes = m,
                Label = SnoozeLabelFormatter.Format(m)
            })
            .ToList();
    }

    public bool IsFiredPending =>
        !Reminder.IsCompleted
        && Reminder.LastNotifiedAt.HasValue
        && Reminder.NextRunAt <= DateTime.Now;

    public string StatusLine
    {
        get
        {
            var t = Reminder.NextRunAt;
            if (IsFiredPending)
            {
                return "Сработало · " + t.ToString("HH:mm");
            }

            if (t.Date == DateTime.Today)
            {
                return "Сегодня " + t.ToString("HH:mm");
            }

            return t.ToString("dd.MM HH:mm");
        }
    }

    public string SubStatusLine => IsFiredPending ? "ожидает действия" : string.Empty;
}
