namespace ReminderApp.Models;

public class Reminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    /// <summary>Исходная строка ввода пользователя.</summary>
    public string OriginalInput { get; set; } = string.Empty;
    public DateTime NextRunAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    /// <summary>Когда уже показали уведомление о наступлении времени (без «Выполнено»).</summary>
    public DateTime? LastNotifiedAt { get; set; }
}
