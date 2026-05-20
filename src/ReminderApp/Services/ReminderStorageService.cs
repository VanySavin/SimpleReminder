using System.IO;
using System.Text.Json;
using ReminderApp.Models;

namespace ReminderApp.Services;

public class ReminderStorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ReminderStorageService()
    {
        _filePath = PortablePaths.RemindersFilePath;
    }

    public async Task<List<Reminder>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            try
            {
                await using var stream = File.OpenRead(_filePath);
                var reminders = await JsonSerializer.DeserializeAsync<List<Reminder>>(stream, _jsonOptions);
                return reminders ?? [];
            }
            catch (JsonException)
            {
                TryBackupAndRemoveCorruptFile();
                return [];
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void TryBackupAndRemoveCorruptFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            PortablePaths.EnsureDataDirectoryExists();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(PortablePaths.DataDirectory, $"reminders.broken-{stamp}.json");
            File.Copy(_filePath, backupPath, overwrite: true);
            File.Delete(_filePath);
        }
        catch
        {
            // стартуем с пустым списком в любом случае
        }
    }

    public async Task SaveAllAsync(IReadOnlyCollection<Reminder> reminders)
    {
        await _lock.WaitAsync();
        try
        {
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, reminders, _jsonOptions);
            }

            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, null);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
