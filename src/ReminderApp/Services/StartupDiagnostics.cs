using System.Diagnostics;
using System.Text;
using WpfMessageBox = System.Windows.MessageBox;

namespace ReminderApp.Services;

/// <summary>
/// Трассировка этапов запуска и понятные сообщения пользователю при сбоях.
/// </summary>
internal static class StartupDiagnostics
{
    public static void TracePhase(string phase, string? detail = null)
    {
        var line = detail is null
            ? $"[SimpleReminder] {phase}"
            : $"[SimpleReminder] {phase}: {detail}";
        Debug.WriteLine(line);
        Trace.WriteLine(line);
    }

    public static void TraceException(string phase, Exception ex)
    {
        TracePhase(phase, ex.ToString());
    }

    public static void ShowStartupWarning(string message)
    {
        WpfMessageBox.Show(
            message,
            "SimpleReminder",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    public static void ShowStartupError(string title, string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        if (ex is not null)
        {
            sb.AppendLine().AppendLine().Append("Техническая информация:").AppendLine().Append(ex.Message);
            if (ex.InnerException is { } inner)
            {
                sb.AppendLine().Append(inner.Message);
            }
        }

        WpfMessageBox.Show(sb.ToString(), title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    public static string FormatPathsFooter()
    {
        return
            $"Папка данных: {PortablePaths.DataDirectory}{Environment.NewLine}" +
            $"Настройки: {PortablePaths.SettingsFilePath}{Environment.NewLine}" +
            $"Напоминания: {PortablePaths.RemindersFilePath}";
    }
}
