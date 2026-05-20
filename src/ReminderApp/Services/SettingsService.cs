using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReminderApp.Services;

public sealed class SettingsService
{
    private const int WriteMaxRetries = 5;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService()
    {
        _filePath = PortablePaths.SettingsFilePath;
    }

    public AppSettings Current { get; private set; } = AppSettingsDefaults.CreateDefault();

    /// <summary>
    /// Загрузка настроек. При ошибках чтения возвращает предупреждение для пользователя.
    /// Ошибки записи при load/migration только логируются (без модального окна).
    /// </summary>
    public async Task<string?> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            try
            {
                PortablePaths.EnsureDataDirectoryExists();
            }
            catch (Exception ex)
            {
                Current = AppSettingsDefaults.CreateDefault();
                return
                    "Не удалось создать или открыть папку data рядом с программой.\n" +
                    "Проверьте права доступа и не синхронизируется ли каталог с облаком в момент запуска.\n\n" +
                    ex.Message + "\n\n" + StartupDiagnostics.FormatPathsFooter();
            }

            if (!File.Exists(_filePath))
            {
                Current = AppSettingsDefaults.CreateDefault();
                RefreshDescriptions(Current);
                await TryPersistSettingsLoggedOnlyAsync(Current);
                return null;
            }

            try
            {
                await using var stream = File.OpenRead(_filePath);
                var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions)
                             ?? AppSettingsDefaults.CreateDefault();
                var rawSnapshot = CloneSettings(loaded);
                var normalized = NormalizeAndMerge(CloneSettings(loaded));
                RefreshDescriptions(normalized);
                Current = normalized;

                if (NeedsPersistAfterLoad(rawSnapshot, normalized))
                {
                    await TryPersistSettingsLoggedOnlyAsync(normalized);
                }

                return null;
            }
            catch (JsonException)
            {
                TryBackupCorruptFile();
                Current = AppSettingsDefaults.CreateDefault();
                RefreshDescriptions(Current);
                var persisted = await TryPersistSettingsLoggedOnlyAsync(Current);
                return persisted
                    ? "Файл настроек был повреждён; подставлены значения по умолчанию и сохранён новый файл."
                    : "Файл настроек был повреждён; восстановлены значения по умолчанию в памяти (запись на диск не удалась).";
            }
            catch (IOException ex)
            {
                Current = AppSettingsDefaults.CreateDefault();
                return
                    "Не удалось прочитать файл настроек (доступ к диску или блокировка файла).\n" +
                    ex.Message + "\n\n" + StartupDiagnostics.FormatPathsFooter();
            }
            catch (UnauthorizedAccessException ex)
            {
                Current = AppSettingsDefaults.CreateDefault();
                return
                    "Нет доступа к файлу настроек.\n" +
                    ex.Message + "\n\n" + StartupDiagnostics.FormatPathsFooter();
            }
            catch (Exception ex)
            {
                Current = AppSettingsDefaults.CreateDefault();
                return
                    "Не удалось прочитать или разобрать файл настроек.\n" +
                    ex.Message + "\n\n" + StartupDiagnostics.FormatPathsFooter();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(bool Ok, string? Error)> TrySaveCurrentAsync(AppSettings snapshot)
    {
        await _lock.WaitAsync();
        try
        {
            snapshot.ExtensionData ??= Current.ExtensionData;
            Current = NormalizeAndMerge(snapshot);
            RefreshDescriptions(Current);
            await SaveUnlockedAsync(Current);
            return (true, null);
        }
        catch (Exception ex)
        {
            LogWriteException("TrySaveCurrentAsync", ex);
            return (false, FormatUserSaveError(ex));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Best-effort сохранение при завершении сессии / выходе. Без UI.
    /// </summary>
    public void TrySaveCurrentOnShutdown()
    {
        if (!_lock.Wait(0))
        {
            Debug.WriteLine($"[SettingsService] TrySaveCurrentOnShutdown skipped: lock busy, path={_filePath}");
            return;
        }

        try
        {
            Current = NormalizeAndMerge(Current);
            RefreshDescriptions(Current);
            SaveUnlockedSync(Current);
        }
        catch (Exception ex)
        {
            LogWriteException("TrySaveCurrentOnShutdown", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public AppSettings CloneCurrent()
    {
        return CloneSettings(Current);
    }

    private AppSettings CloneSettings(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, _jsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
               ?? AppSettingsDefaults.CreateDefault();
    }

    private async Task<bool> TryPersistSettingsLoggedOnlyAsync(AppSettings settings)
    {
        try
        {
            await SaveUnlockedAsync(settings);
            return true;
        }
        catch (Exception ex)
        {
            LogWriteException("LoadAsync persist", ex);
            return false;
        }
    }

    private string FormatUserSaveError(Exception ex) =>
        "Ошибка записи settings.json:\n" + ex.Message + "\n\nФайл: " + _filePath;

    private void LogWriteException(string phase, Exception ex)
    {
        Debug.WriteLine($"[SettingsService] {phase} failed, path={_filePath}{Environment.NewLine}{ex}");
    }

    private static void RefreshDescriptions(AppSettings s)
    {
        foreach (var kv in AppSettingsDefaults.Descriptions)
        {
            s.Descriptions[kv.Key] = kv.Value;
        }
    }

    private static bool NeedsPersistAfterLoad(AppSettings raw, AppSettings normalized)
    {
        if (raw.SnoozeOptionsMinutes is { Count: > 0 })
        {
            return true;
        }

        foreach (var key in AppSettingsDefaults.Descriptions.Keys)
        {
            if (!raw.Descriptions.TryGetValue(key, out var rawValue)
                || !string.Equals(rawValue, AppSettingsDefaults.Descriptions[key], StringComparison.Ordinal))
            {
                return true;
            }
        }

        var rawNormalized = NormalizeAndMerge(CloneSettingsForCompare(raw));
        return !SettingsEqualIgnoringDescriptions(rawNormalized, normalized);
    }

    private static AppSettings CloneSettingsForCompare(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        }) ?? AppSettingsDefaults.CreateDefault();
    }

    private static bool SettingsEqualIgnoringDescriptions(AppSettings a, AppSettings b)
    {
        if (a.ThemeMode != b.ThemeMode) return false;
        if (a.AutoSnoozeOnDismissMinutes != b.AutoSnoozeOnDismissMinutes) return false;
        if (a.SubmitReminderOnEnter != b.SubmitReminderOnEnter) return false;
        if (a.AutoFocusInputOnStart != b.AutoFocusInputOnStart) return false;
        if (a.ShowInputExamples != b.ShowInputExamples) return false;
        if (a.ShowCreatedNotification != b.ShowCreatedNotification) return false;
        if (a.PlayNotificationSound != b.PlayNotificationSound) return false;
        if (a.NotificationDurationSeconds != b.NotificationDurationSeconds) return false;
        if (a.MaxVisibleNotifications != b.MaxVisibleNotifications) return false;
        if (a.MinimizeToTrayOnClose != b.MinimizeToTrayOnClose) return false;
        if (a.ShowTrayNoticeOncePerSession != b.ShowTrayNoticeOncePerSession) return false;
        if (a.SchedulerIntervalSeconds != b.SchedulerIntervalSeconds) return false;
        if (a.AutoStartWithWindows != b.AutoStartWithWindows) return false;
        if (a.StartMinimizedOnAutoStart != b.StartMinimizedOnAutoStart) return false;
        if (!SnoozeButtonsEqual(a.SnoozeButtons, b.SnoozeButtons)) return false;
        if (!PlacementEqual(a.NotificationPlacement, b.NotificationPlacement)) return false;
        if (!CustomSnoozePlacementEqual(a.CustomSnoozeWindowPlacement, b.CustomSnoozeWindowPlacement)) return false;
        if (!MainWindowPlacementEqual(a.MainWindowPlacement, b.MainWindowPlacement)) return false;
        return true;
    }

    private static bool SnoozeButtonsEqual(List<SnoozeButtonSettings> a, List<SnoozeButtonSettings> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Kind != b[i].Kind) return false;
            if (a[i].Enabled != b[i].Enabled) return false;
            if (a[i].Minutes != b[i].Minutes) return false;
        }

        return true;
    }

    private static bool PlacementEqual(NotificationPlacementSettings a, NotificationPlacementSettings b)
    {
        if (a.Mode != b.Mode) return false;
        if (a.CustomLeft != b.CustomLeft) return false;
        if (a.CustomTop != b.CustomTop) return false;
        if (!string.Equals(a.ScreenDeviceName, b.ScreenDeviceName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private async Task SaveUnlockedAsync(AppSettings settings)
    {
        await SaveUnlockedCore(settings, async stream =>
        {
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
            await stream.FlushAsync();
        });
    }

    private void SaveUnlockedSync(AppSettings settings)
    {
        SaveUnlockedCore(settings, stream =>
        {
            JsonSerializer.Serialize(stream, settings, _jsonOptions);
            stream.Flush();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    private async Task SaveUnlockedCore(AppSettings settings, Func<FileStream, Task> writeAsync)
    {
        PortablePaths.EnsureDataDirectoryExists();
        var tempPath = _filePath + ".tmp";
        Exception? lastException = null;

        for (var attempt = 0; attempt < WriteMaxRetries; attempt++)
        {
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await writeAsync(stream);
                }

                if (File.Exists(_filePath))
                {
                    File.Replace(tempPath, _filePath, null);
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }

                return;
            }
            catch (IOException ex) when (attempt < WriteMaxRetries - 1)
            {
                lastException = ex;
                Debug.WriteLine(
                    $"[SettingsService] Write retry {attempt + 1}/{WriteMaxRetries}, path={_filePath}{Environment.NewLine}{ex}");
                TryDeleteOrphanTemp(tempPath);
                await Task.Delay(50 * (attempt + 1));
            }
            catch (IOException ex)
            {
                lastException = ex;
                TryDeleteOrphanTemp(tempPath);
                throw;
            }
            catch (Exception)
            {
                TryDeleteOrphanTemp(tempPath);
                throw;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }
    }

    private static void TryDeleteOrphanTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Failed to delete orphan temp: {tempPath}{Environment.NewLine}{ex}");
        }
    }

    public static AppSettings NormalizeAndMerge(AppSettings s)
    {
        if (!Enum.IsDefined(typeof(ThemeMode), s.ThemeMode))
        {
            s.ThemeMode = ThemeMode.Auto;
        }

        s.SnoozeButtons = NormalizeSnoozeButtons(s.SnoozeButtons, s.SnoozeOptionsMinutes);
        s.SnoozeOptionsMinutes = null;

        s.NotificationDurationSeconds = Math.Clamp(s.NotificationDurationSeconds, 2, 30);
        s.MaxVisibleNotifications = Math.Clamp(s.MaxVisibleNotifications, 1, 8);
        s.AutoSnoozeOnDismissMinutes = Math.Clamp(s.AutoSnoozeOnDismissMinutes, 1, 1440);
        s.SchedulerIntervalSeconds = Math.Clamp(s.SchedulerIntervalSeconds, 1, 60);
        s.NotificationPlacement = NormalizePlacement(s.NotificationPlacement);
        s.CustomSnoozeWindowPlacement = NormalizeCustomSnoozePlacement(s.CustomSnoozeWindowPlacement);
        s.MainWindowPlacement = NormalizeMainWindowPlacement(s.MainWindowPlacement);

        s.Descriptions ??= new Dictionary<string, string>(StringComparer.Ordinal);

        return s;
    }

    private static CustomSnoozeWindowPlacementSettings NormalizeCustomSnoozePlacement(
        CustomSnoozeWindowPlacementSettings? placement)
    {
        var p = placement ?? new CustomSnoozeWindowPlacementSettings();
        if (!Enum.IsDefined(typeof(CustomSnoozeWindowPlacementMode), p.Mode))
        {
            p.Mode = CustomSnoozeWindowPlacementMode.NearMainWindow;
        }

        if (!IsFiniteAndReasonableCoordinate(p.CustomLeft))
        {
            p.CustomLeft = null;
        }

        if (!IsFiniteAndReasonableCoordinate(p.CustomTop))
        {
            p.CustomTop = null;
        }

        if (string.IsNullOrWhiteSpace(p.ScreenDeviceName))
        {
            p.ScreenDeviceName = null;
        }

        return p;
    }

    private static MainWindowPlacementSettings NormalizeMainWindowPlacement(MainWindowPlacementSettings? placement)
    {
        var p = placement ?? new MainWindowPlacementSettings();
        if (!IsFiniteAndReasonableCoordinate(p.Left))
        {
            p.Left = null;
        }

        if (!IsFiniteAndReasonableCoordinate(p.Top))
        {
            p.Top = null;
        }

        if (!IsFiniteAndReasonableSize(p.Width, 720))
        {
            p.Width = null;
        }

        if (!IsFiniteAndReasonableSize(p.Height, 520))
        {
            p.Height = null;
        }

        if (string.IsNullOrWhiteSpace(p.ScreenDeviceName))
        {
            p.ScreenDeviceName = null;
        }

        var state = p.WindowState?.Trim();
        p.WindowState = string.Equals(state, "Maximized", StringComparison.OrdinalIgnoreCase)
            ? "Maximized"
            : "Normal";

        return p;
    }

    private static bool IsFiniteAndReasonableSize(double? value, double min)
    {
        if (!value.HasValue)
        {
            return true;
        }

        var v = value.Value;
        return !double.IsNaN(v) && !double.IsInfinity(v) && v >= min && v <= 100000;
    }

    private static bool CustomSnoozePlacementEqual(
        CustomSnoozeWindowPlacementSettings a,
        CustomSnoozeWindowPlacementSettings b)
    {
        if (a.Mode != b.Mode) return false;
        if (a.CustomLeft != b.CustomLeft) return false;
        if (a.CustomTop != b.CustomTop) return false;
        if (!string.Equals(a.ScreenDeviceName, b.ScreenDeviceName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool MainWindowPlacementEqual(MainWindowPlacementSettings a, MainWindowPlacementSettings b)
    {
        if (a.Left != b.Left) return false;
        if (a.Top != b.Top) return false;
        if (a.Width != b.Width) return false;
        if (a.Height != b.Height) return false;
        if (!string.Equals(a.WindowState, b.WindowState, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.ScreenDeviceName, b.ScreenDeviceName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static NotificationPlacementSettings NormalizePlacement(NotificationPlacementSettings? placement)
    {
        var p = placement ?? new NotificationPlacementSettings();
        if (!Enum.IsDefined(typeof(NotificationPlacementMode), p.Mode))
        {
            p.Mode = NotificationPlacementMode.BottomRight;
        }

        if (!IsFiniteAndReasonableCoordinate(p.CustomLeft))
        {
            p.CustomLeft = null;
        }

        if (!IsFiniteAndReasonableCoordinate(p.CustomTop))
        {
            p.CustomTop = null;
        }

        if (string.IsNullOrWhiteSpace(p.ScreenDeviceName))
        {
            p.ScreenDeviceName = null;
        }

        return p;
    }

    private static bool IsFiniteAndReasonableCoordinate(double? value)
    {
        if (!value.HasValue)
        {
            return true;
        }

        var v = value.Value;
        return !double.IsNaN(v) && !double.IsInfinity(v) && v is >= -100000 and <= 100000;
    }

    private static List<SnoozeButtonSettings> NormalizeSnoozeButtons(
        List<SnoozeButtonSettings>? snoozeButtons,
        List<int>? legacyMinutes)
    {
        var defaults = AppSettingsDefaults.CreateDefault().SnoozeButtons;
        var source = (snoozeButtons is { Count: > 0 } ? snoozeButtons : null)
                     ?? MigrateLegacySnoozeButtons(legacyMinutes)
                     ?? defaults;

        var minuteButtons = source
            .Where(b => b.Kind == SnoozeButtonKind.Minutes)
            .Select(b => new SnoozeButtonSettings
            {
                Kind = SnoozeButtonKind.Minutes,
                Enabled = b.Enabled,
                Minutes = Math.Clamp(b.Minutes ?? 0, 1, 1440)
            })
            .Take(3)
            .ToList();

        while (minuteButtons.Count < 3)
        {
            var fallback = defaults[minuteButtons.Count];
            minuteButtons.Add(new SnoozeButtonSettings
            {
                Kind = SnoozeButtonKind.Minutes,
                Enabled = fallback.Enabled,
                Minutes = fallback.Minutes
            });
        }

        var seenEnabledMinutes = new HashSet<int>();
        foreach (var button in minuteButtons)
        {
            if (button.Minutes is not int m)
            {
                button.Minutes = 5;
                m = 5;
            }

            if (button.Enabled && !seenEnabledMinutes.Add(m))
            {
                button.Enabled = false;
            }
        }

        var custom = source.FirstOrDefault(b => b.Kind == SnoozeButtonKind.Custom);
        var customButton = new SnoozeButtonSettings
        {
            Kind = SnoozeButtonKind.Custom,
            Enabled = custom?.Enabled ?? defaults[3].Enabled
        };

        var result = new List<SnoozeButtonSettings>(4);
        result.AddRange(minuteButtons);
        result.Add(customButton);
        return result;
    }

    private static List<SnoozeButtonSettings>? MigrateLegacySnoozeButtons(List<int>? legacyMinutes)
    {
        if (legacyMinutes is null || legacyMinutes.Count == 0)
        {
            return null;
        }

        var normalized = legacyMinutes
            .Where(m => m is >= 1 and <= 1440)
            .Distinct()
            .Take(3)
            .ToList();
        if (normalized.Count == 0)
        {
            return null;
        }

        var defaults = AppSettingsDefaults.CreateDefault().SnoozeButtons;
        var migrated = new List<SnoozeButtonSettings>(4);
        for (var i = 0; i < 3; i++)
        {
            var has = i < normalized.Count;
            migrated.Add(new SnoozeButtonSettings
            {
                Kind = SnoozeButtonKind.Minutes,
                Enabled = has,
                Minutes = has ? normalized[i] : defaults[i].Minutes
            });
        }

        migrated.Add(new SnoozeButtonSettings { Kind = SnoozeButtonKind.Custom, Enabled = true });
        return migrated;
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(PortablePaths.DataDirectory, $"settings.broken-{stamp}.json");
            File.Copy(_filePath, backupPath, overwrite: true);
            File.Delete(_filePath);
        }
        catch
        {
            // ignore
        }
    }
}
