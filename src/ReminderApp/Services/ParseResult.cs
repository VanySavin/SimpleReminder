namespace ReminderApp.Services;

public class ParseResult
{
    public bool IsSuccess { get; init; }
    public DateTime? NextRunAt { get; init; }
    public string ReminderText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;

    public static ParseResult Success(DateTime runAt, string reminderText) =>
        new()
        {
            IsSuccess = true,
            NextRunAt = runAt,
            ReminderText = reminderText
        };

    public static ParseResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
