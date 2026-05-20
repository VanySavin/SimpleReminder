using System.IO;
using System.Runtime.InteropServices;

namespace ReminderApp.Services;

/// <summary>
/// Автозапуск через ярлык в папке «Автозагрузка» пользователя (не данные приложения).
/// </summary>
public sealed class AutoStartService
{
    public const string ShortcutFileName = "SimpleReminder.lnk";

    public const string ShortcutDescription = "SimpleReminder — локальные напоминания";

    public static string GetStartupFolderPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");

    public static string GetStartupShortcutPath() =>
        Path.Combine(GetStartupFolderPath(), ShortcutFileName);

    /// <summary>
    /// Есть ли ярлык автозапуска и указывает ли он на текущий exe.
    /// </summary>
    public bool IsAutoStartEnabled()
    {
        var path = GetStartupShortcutPath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var target = ReadShortcutTargetPath(path);
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            var current = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(current))
            {
                return true;
            }

            return PathsEqual(target.Trim(), current.Trim());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Включить или выключить автозапуск (создать/обновить или удалить ярлык).
    /// </summary>
    public Task<AutoStartApplyResult> SetAutoStartEnabledAsync(bool enabled, bool startMinimized) =>
        Task.Run(() => SetAutoStartEnabledCore(enabled, startMinimized));

    /// <summary>
    /// Привести ярлык в соответствие с настройками (восстановить при пропаже, убрать при выключении).
    /// </summary>
    public Task<AutoStartApplyResult> EnsureAutoStartMatchesSettingsAsync(AppSettings settings) =>
        SetAutoStartEnabledAsync(settings.AutoStartWithWindows, settings.StartMinimizedOnAutoStart);

    private static AutoStartApplyResult SetAutoStartEnabledCore(bool enabled, bool startMinimized)
    {
        var shortcutPath = GetStartupShortcutPath();

        try
        {
            if (!enabled)
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }

                return AutoStartApplyResult.Ok;
            }

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return AutoStartApplyResult.Fail(
                    "Не удалось определить путь к программе (Environment.ProcessPath). " +
                    "Запустите SimpleReminder из файла SimpleReminder.exe в папке программы.");
            }

            if (string.Equals(Path.GetFileName(exePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return AutoStartApplyResult.Fail(
                    "Автозапуск настраивается только при запуске из SimpleReminder.exe " +
                    "(например из папки publish после сборки), а не через команду «dotnet run».");
            }

            var workDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(workDir))
            {
                return AutoStartApplyResult.Fail("Не удалось определить рабочую папку рядом с программой.");
            }

            Directory.CreateDirectory(GetStartupFolderPath());

            var arguments = startMinimized ? "--autostart --minimized" : "--autostart";

            CreateOrUpdateShortcut(shortcutPath, exePath, workDir, arguments, ShortcutDescription);
            return AutoStartApplyResult.Ok;
        }
        catch (Exception ex)
        {
            return AutoStartApplyResult.Fail(
                "Не удалось изменить автозапуск (ярлык в папке «Автозагрузка»).\n\n" +
                "Возможные причины: антивирус, права доступа, политики Windows.\n\n" +
                ex.Message);
        }
    }

    private static void CreateOrUpdateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string arguments,
        string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            throw new InvalidOperationException(
                "Компонент Windows Script Host (WScript.Shell) недоступен. Ярлык создать нельзя.");
        }

        object? shellObj = null;
        object? shortcutObj = null;
        try
        {
            shellObj = Activator.CreateInstance(shellType)
                       ?? throw new InvalidOperationException("Не удалось создать WScript.Shell.");

            dynamic shell = shellObj;
            shortcutObj = shell.CreateShortcut(shortcutPath);
            dynamic shortcut = shortcutObj;
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Arguments = arguments;
            shortcut.Description = description;
            shortcut.Save();
        }
        finally
        {
            if (shortcutObj is not null)
            {
                try
                {
                    Marshal.FinalReleaseComObject(shortcutObj);
                }
                catch
                {
                    // ignore
                }
            }

            if (shellObj is not null)
            {
                try
                {
                    Marshal.FinalReleaseComObject(shellObj);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static string? ReadShortcutTargetPath(string shortcutPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return null;
        }

        object? shellObj = null;
        object? shortcutObj = null;
        try
        {
            shellObj = Activator.CreateInstance(shellType);
            if (shellObj is null)
            {
                return null;
            }

            dynamic shell = shellObj;
            shortcutObj = shell.CreateShortcut(shortcutPath);
            dynamic shortcut = shortcutObj;
            return (string)shortcut.TargetPath;
        }
        finally
        {
            if (shortcutObj is not null)
            {
                try
                {
                    Marshal.FinalReleaseComObject(shortcutObj);
                }
                catch
                {
                    // ignore
                }
            }

            if (shellObj is not null)
            {
                try
                {
                    Marshal.FinalReleaseComObject(shellObj);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a),
                Path.GetFullPath(b),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}

public readonly record struct AutoStartApplyResult(bool Success, string? ErrorMessage)
{
    public static AutoStartApplyResult Ok => new(true, null);

    public static AutoStartApplyResult Fail(string message) => new(false, message);
}
