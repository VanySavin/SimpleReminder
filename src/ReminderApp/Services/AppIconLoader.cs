using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace ReminderApp.Services;

/// <summary>
/// Загрузка иконки приложения для WinForms (трей) без внешних файлов и абсолютных путей.
/// </summary>
public static class AppIconLoader
{
    private const string EmbeddedIcoPackUri = "pack://application:,,,/Assets/reminder_icon.ico";

    /// <summary>
    /// Иконка для <see cref="System.Windows.Forms.NotifyIcon"/>. Вызывающий обязан <see cref="Icon.Dispose"/>,
    /// кроме случая <see cref="SystemIcons.Application"/> (не освобождать).
    /// </summary>
    public static Icon GetNotifyIcon()
    {
        var fromResource = TryLoadIconFromPackUri(EmbeddedIcoPackUri);
        if (fromResource is not null)
        {
            return fromResource;
        }

        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch
        {
            // игнорируем
        }

        return SystemIcons.Application;
    }

    /// <summary>
    /// Нужно ли вызывать <see cref="Icon.Dispose"/> для результата <see cref="GetNotifyIcon"/>.
    /// </summary>
    public static bool ShouldDisposeNotifyIcon(Icon icon) =>
        !ReferenceEquals(icon, SystemIcons.Application);

    private static Icon? TryLoadIconFromPackUri(string packUri)
    {
        try
        {
            StreamResourceInfo? sri = System.Windows.Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
            if (sri?.Stream is null)
            {
                return null;
            }

            using (sri.Stream)
            using (var ms = new MemoryStream())
            {
                sri.Stream.CopyTo(ms);
                ms.Position = 0;
                return new Icon(ms);
            }
        }
        catch
        {
            return null;
        }
    }
}
