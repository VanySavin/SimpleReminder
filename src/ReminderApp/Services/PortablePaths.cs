using System.IO;

namespace ReminderApp.Services;

/// <summary>
/// Пути для portable-режима: данные только рядом с exe в папке <c>data</c>.
/// </summary>
public static class PortablePaths
{
    /// <summary>
    /// Каталог, где лежит exe (и распакованные файлы single-file при необходимости).
    /// Используем <see cref="AppContext.BaseDirectory"/>, а не <c>Environment.CurrentDirectory</c>,
    /// чтобы при запуске из ярлыка или другой рабочей папки пути не «поехали».
    /// </summary>
    public static string ApplicationBaseDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>Папка <c>data</c> рядом с exe.</summary>
    public static string DataDirectory => Path.Combine(ApplicationBaseDirectory, "data");

    /// <summary><c>data/reminders.json</c></summary>
    public static string RemindersFilePath => Path.Combine(DataDirectory, "reminders.json");

    /// <summary><c>data/settings.json</c></summary>
    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary><c>data/sounds</c></summary>
    public static string SoundsDirectory => Path.Combine(DataDirectory, "sounds");

    /// <summary>Имя файла звука уведомления.</summary>
    public const string NotificationWavFileName = "notification.wav";

    /// <summary><c>data/sounds/notification.wav</c></summary>
    public static string NotificationWavPath => Path.Combine(SoundsDirectory, NotificationWavFileName);

    /// <summary>
    /// Создаёт <c>data</c> и <c>data/sounds</c>. Файл <c>settings.json</c> создаётся при первой загрузке настроек.
    /// Вызывать при старте приложения (один раз).
    /// </summary>
    public static void EnsurePortableLayout()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SoundsDirectory);
    }

    public static void EnsureDataDirectoryExists() =>
        Directory.CreateDirectory(DataDirectory);

    public static void EnsureSoundsDirectoryExists() =>
        Directory.CreateDirectory(SoundsDirectory);
}
