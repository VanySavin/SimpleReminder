using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ReminderApp.Services;

/// <summary>
/// Один экземпляр на portable-папку: mutex + именованный канал для команды «показать окно».
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    public const string ShowMainWindowCommand = "show-main-window";

    private readonly Mutex? _mutex;
    private readonly bool _ownsMutex;
    private readonly string _pipeName;
    private readonly Dispatcher _uiDispatcher;
    private readonly Action _onShowMainWindow;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    private SingleInstanceService(
        Mutex? mutex,
        bool ownsMutex,
        string pipeName,
        Dispatcher uiDispatcher,
        Action onShowMainWindow)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _pipeName = pipeName;
        _uiDispatcher = uiDispatcher;
        _onShowMainWindow = onShowMainWindow;
    }

    /// <summary>
    /// Пытается стать главным экземпляром. Если уже есть другой — обрабатывает второй запуск и возвращает false.
    /// </summary>
    public static bool TryEnterAsPrimaryOrExitSecondary(
        bool secondaryCmdAutostart,
        bool secondaryCmdMinimized,
        Dispatcher uiDispatcher,
        Action onShowMainWindow,
        out SingleInstanceService? primary)
    {
        primary = null;
        var (mutexName, pipeName) = GetMutexAndPipeNames();

        Mutex? m = null;
        try
        {
            m = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
            if (!createdNew)
            {
                m.Dispose();
                primary = null;
                HandleSecondary(secondaryCmdAutostart, secondaryCmdMinimized, pipeName);
                return false;
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.TraceException("Single-instance: mutex недоступен, продолжаем без блокировки", ex);
            m?.Dispose();
            primary = new SingleInstanceService(null, false, string.Empty, uiDispatcher, onShowMainWindow);
            return true;
        }

        primary = new SingleInstanceService(m, true, pipeName, uiDispatcher, onShowMainWindow);
        primary.StartPipeServerIfNeeded();
        return true;
    }

    private static (string MutexName, string PipeName) GetMutexAndPipeNames()
    {
        var hash = ComputeInstanceHash(GetKeyPathForInstance());
        return (@"Local\SimpleReminder-" + hash, "SimpleReminder-" + hash);
    }

    /// <summary>
    /// Ключ экземпляра: полный путь к SimpleReminder.exe, если процесс запущен из него; иначе папка установки (как у portable data).
    /// </summary>
    private static string GetKeyPathForInstance()
    {
        try
        {
            var p = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(p)
                && p.EndsWith("SimpleReminder.exe", StringComparison.OrdinalIgnoreCase)
                && File.Exists(p))
            {
                return Path.GetFullPath(p);
            }
        }
        catch
        {
            // ignore
        }

        return Path.GetFullPath(
            PortablePaths.ApplicationBaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
    }

    private static string ComputeInstanceHash(string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(bytes)[..24];
    }

    private static void HandleSecondary(bool autostart, bool minimized, string pipeName)
    {
        if (autostart && minimized)
        {
            StartupDiagnostics.TracePhase("Второй запуск", "тихий выход (--autostart --minimized), первый экземпляр не трогаем");
            return;
        }

        if (TrySendShowMainWindowWithRetries(pipeName))
        {
            StartupDiagnostics.TracePhase("Второй запуск", "команда «показать окно» передана первому экземпляру");
            return;
        }

        StartupDiagnostics.ShowStartupWarning(
            "SimpleReminder уже запущен, но не удалось открыть его окно. Проверьте значок в трее.");
    }

    private static bool TrySendShowMainWindowWithRetries(string pipeName)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (TrySendShowMainWindowOnce(pipeName, connectTimeoutMs: 600))
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static bool TrySendShowMainWindowOnce(string pipeName, int connectTimeoutMs)
    {
        if (string.IsNullOrEmpty(pipeName))
        {
            return false;
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.None);

            client.Connect(connectTimeoutMs);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(ShowMainWindowCommand);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartPipeServerIfNeeded()
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            return;
        }

        _serverTask = Task.Run(() => RunPipeServerLoop(_cts.Token), CancellationToken.None);
    }

    private void RunPipeServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                try
                {
                    server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                string? line;
                try
                {
                    using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256, leaveOpen: true);
                    line = reader.ReadLine();
                }
                finally
                {
                    try
                    {
                        if (server.IsConnected)
                        {
                            server.Disconnect();
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (string.Equals(line, ShowMainWindowCommand, StringComparison.Ordinal))
                {
                    _uiDispatcher.BeginInvoke(_onShowMainWindow, DispatcherPriority.Normal);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StartupDiagnostics.TraceException("Single-instance: канал", ex);
                try
                {
                    Thread.Sleep(80);
                }
                catch
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _serverTask?.Wait(TimeSpan.FromMilliseconds(900));
        }
        catch
        {
            // ignore
        }

        if (_ownsMutex && _mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // ignore
            }

            try
            {
                _mutex.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            _cts.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
