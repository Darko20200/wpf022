using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace YafesV2.Managers
{
    /// <summary>
    /// Log yönetimi için manager sınıfı
    /// Asenkron, thread-safe loglama sistemi
    /// </summary>
    public class LogManager : IDisposable
    {
        #region Properties

        /// <summary>
        /// Log dizini
        /// </summary>
        public string LogDirectory { get; private set; }

        /// <summary>
        /// Mevcut log dosyası
        /// </summary>
        public string CurrentLogFile { get; private set; }

        /// <summary>
        /// Log seviyesi
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Maksimum log dosya boyutu (bytes)
        /// </summary>
        public long MaxLogFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Maksimum log dosya sayısı
        /// </summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>
        /// Log formatı
        /// </summary>
        public LogFormat LogFormat { get; set; } = LogFormat.Text;

        /// <summary>
        /// Console'a da yazsın mı
        /// </summary>
        public bool LogToConsole { get; set; } = true;

        /// <summary>
        /// Loglama aktif mi
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        #endregion

        #region Private Fields

        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundTask;
        private StreamWriter _currentWriter;
        private bool _disposed;

        #endregion

        #region Constructor

        public LogManager(string logDirectory)
        {
            LogDirectory = logDirectory;
            _logQueue = new ConcurrentQueue<LogEntry>();
            _writeSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            // Background log processing task
            _backgroundTask = Task.Run(ProcessLogQueueAsync);

            // Periyodik flush timer (her 5 saniyede)
            _flushTimer = new Timer(async _ => await FlushAsync(), null,
                                  TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Log manager'ı başlatır
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                await CreateNewLogFileAsync();
                await LogInformationAsync("LogManager initialized");
            }
            catch (Exception ex)
            {
                // Log manager başlatılamadıysa console'a yaz
                Console.WriteLine($"LogManager initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug seviyesinde log yazar
        /// </summary>
        public void LogDebug(string message, Exception exception = null)
        {
            LogAsync(LogLevel.Debug, message, exception);
        }

        /// <summary>
        /// Information seviyesinde log yazar
        /// </summary>
        public void LogInformation(string message, Exception exception = null)
        {
            LogAsync(LogLevel.Information, message, exception);
        }

        /// <summary>
        /// Warning seviyesinde log yazar
        /// </summary>
        public void LogWarning(string message, Exception exception = null)
        {
            LogAsync(LogLevel.Warning, message, exception);
        }

        /// <summary>
        /// Error seviyesinde log yazar
        /// </summary>
        public void LogError(string message, Exception exception = null)
        {
            LogAsync(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// Critical seviyesinde log yazar
        /// </summary>
        public void LogCritical(string message, Exception exception = null)
        {
            LogAsync(LogLevel.Critical, message, exception);
        }

        /// <summary>
        /// Asenkron debug log
        /// </summary>
        public async Task LogDebugAsync(string message, Exception exception = null)
        {
            await LogAsync(LogLevel.Debug, message, exception);
        }

        /// <summary>
        /// Asenkron information log
        /// </summary>
        public async Task LogInformationAsync(string message, Exception exception = null)
        {
            await LogAsync(LogLevel.Information, message, exception);
        }

        /// <summary>
        /// Asenkron warning log
        /// </summary>
        public async Task LogWarningAsync(string message, Exception exception = null)
        {
            await LogAsync(LogLevel.Warning, message, exception);
        }

        /// <summary>
        /// Asenkron error log
        /// </summary>
        public async Task LogErrorAsync(string message, Exception exception = null)
        {
            await LogAsync(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// Asenkron critical log
        /// </summary>
        public async Task LogCriticalAsync(string message, Exception exception = null)
        {
            await LogAsync(LogLevel.Critical, message, exception);
        }

        /// <summary>
        /// Özel log seviyesi ile log yazar
        /// </summary>
        public async Task LogAsync(LogLevel level, string message, Exception exception = null)
        {
            if (!IsEnabled || level < MinimumLogLevel)
                return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Exception = exception,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            _logQueue.Enqueue(logEntry);

            // Console output (opsiyonel)
            if (LogToConsole)
            {
                WriteToConsole(logEntry);
            }
        }

        /// <summary>
        /// Log buffer'ını temizler
        /// </summary>
        public async Task FlushAsync()
        {
            if (_currentWriter != null)
            {
                await _writeSemaphore.WaitAsync();
                try
                {
                    await _currentWriter.FlushAsync();
                }
                finally
                {
                    _writeSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Log dosyalarını temizler (eski dosyaları siler)
        /// </summary>
        public async Task CleanupLogFilesAsync()
        {
            try
            {
                await _writeSemaphore.WaitAsync();

                var logFiles = Directory.GetFiles(LogDirectory, "*.log")
                                       .Select(f => new FileInfo(f))
                                       .OrderByDescending(f => f.CreationTime)
                                       .ToList();

                // Fazla dosyaları sil
                if (logFiles.Count > MaxLogFiles)
                {
                    var filesToDelete = logFiles.Skip(MaxLogFiles);
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch
                        {
                            // Ignore individual file deletion errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log cleanup error: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Log dosyalarının listesini alır
        /// </summary>
        public List<LogFileInfo> GetLogFiles()
        {
            try
            {
                return Directory.GetFiles(LogDirectory, "*.log")
                               .Select(f => new FileInfo(f))
                               .Select(f => new LogFileInfo
                               {
                                   FileName = f.Name,
                                   FilePath = f.FullName,
                                   Size = f.Length,
                                   CreatedDate = f.CreationTime,
                                   ModifiedDate = f.LastWriteTime
                               })
                               .OrderByDescending(f => f.CreatedDate)
                               .ToList();
            }
            catch
            {
                return new List<LogFileInfo>();
            }
        }

        /// <summary>
        /// Belirli tarih aralığındaki logları okur
        /// </summary>
        public async Task<List<LogEntry>> ReadLogsAsync(DateTime fromDate, DateTime toDate, LogLevel? minLevel = null)
        {
            var logs = new List<LogEntry>();

            try
            {
                var logFiles = GetLogFiles()
                              .Where(f => f.CreatedDate >= fromDate.Date && f.CreatedDate <= toDate.Date.AddDays(1))
                              .ToList();

                foreach (var logFile in logFiles)
                {
                    var fileLogs = await ReadLogFileAsync(logFile.FilePath);

                    var filteredLogs = fileLogs.Where(l =>
                        l.Timestamp >= fromDate &&
                        l.Timestamp <= toDate &&
                        (minLevel == null || l.Level >= minLevel.Value));

                    logs.AddRange(filteredLogs);
                }

                return logs.OrderBy(l => l.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error reading logs: {ex.Message}", ex);
                return logs;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Background log processing
        /// </summary>
        private async Task ProcessLogQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_logQueue.TryDequeue(out var logEntry))
                    {
                        await WriteLogEntryAsync(logEntry);
                    }
                    else
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Log processing error: {ex.Message}");
                }
            }

            // Process remaining queue items
            while (_logQueue.TryDequeue(out var logEntry))
            {
                await WriteLogEntryAsync(logEntry);
            }
        }

        /// <summary>
        /// Log entry'sini dosyaya yazar
        /// </summary>
        private async Task WriteLogEntryAsync(LogEntry logEntry)
        {
            try
            {
                await _writeSemaphore.WaitAsync();

                // Dosya boyutu kontrolü
                if (_currentWriter != null && new FileInfo(CurrentLogFile).Length > MaxLogFileSize)
                {
                    await CreateNewLogFileAsync();
                }

                if (_currentWriter != null)
                {
                    string logLine = FormatLogEntry(logEntry);
                    await _currentWriter.WriteLineAsync(logLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log write error: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Yeni log dosyası oluşturur
        /// </summary>
        private async Task CreateNewLogFileAsync()
        {
            try
            {
                if (_currentWriter != null)
                {
                    await _currentWriter.DisposeAsync();
                }

                string fileName = $"yafes_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                CurrentLogFile = Path.Combine(LogDirectory, fileName);

                _currentWriter = new StreamWriter(CurrentLogFile, append: true)
                {
                    AutoFlush = false
                };

                // Log dosyası temizliği
                await CleanupLogFilesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log file creation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Log entry'sini formatlar
        /// </summary>
        private string FormatLogEntry(LogEntry logEntry)
        {
            return LogFormat switch
            {
                LogFormat.Json => FormatAsJson(logEntry),
                LogFormat.Xml => FormatAsXml(logEntry),
                _ => FormatAsText(logEntry)
            };
        }

        /// <summary>
        /// Text formatında log
        /// </summary>
        private string FormatAsText(LogEntry logEntry)
        {
            var text = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] " +
                      $"[{logEntry.Level}] " +
                      $"[Thread-{logEntry.ThreadId}] " +
                      $"{logEntry.Message}";

            if (logEntry.Exception != null)
            {
                text += $"\nException: {logEntry.Exception}";
            }

            return text;
        }

        /// <summary>
        /// JSON formatında log
        /// </summary>
        private string FormatAsJson(LogEntry logEntry)
        {
            var jsonEntry = new
            {
                timestamp = logEntry.Timestamp,
                level = logEntry.Level.ToString(),
                threadId = logEntry.ThreadId,
                message = logEntry.Message,
                exception = logEntry.Exception?.ToString()
            };

            return JsonSerializer.Serialize(jsonEntry);
        }

        /// <summary>
        /// XML formatında log
        /// </summary>
        private string FormatAsXml(LogEntry logEntry)
        {
            return $"<logEntry>" +
                   $"<timestamp>{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}</timestamp>" +
                   $"<level>{logEntry.Level}</level>" +
                   $"<threadId>{logEntry.ThreadId}</threadId>" +
                   $"<message><![CDATA[{logEntry.Message}]]></message>" +
                   (logEntry.Exception != null ? $"<exception><![CDATA[{logEntry.Exception}]]></exception>" : "") +
                   $"</logEntry>";
        }

        /// <summary>
        /// Console'a log yazar
        /// </summary>
        private void WriteToConsole(LogEntry logEntry)
        {
            var color = logEntry.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(FormatAsText(logEntry));
            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Log dosyasını okur
        /// </summary>
        private async Task<List<LogEntry>> ReadLogFileAsync(string filePath)
        {
            var logs = new List<LogEntry>();

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);

                foreach (var line in lines)
                {
                    if (TryParseLogLine(line, out var logEntry))
                    {
                        logs.Add(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log file {filePath}: {ex.Message}");
            }

            return logs;
        }

        /// <summary>
        /// Log satırını parse eder
        /// </summary>
        private bool TryParseLogLine(string line, out LogEntry logEntry)
        {
            logEntry = null;

            try
            {
                // Basit text format parsing
                // [2024-01-01 12:00:00.000] [Information] [Thread-1] Message
                if (line.StartsWith("[") && line.Contains("] [") && line.Contains("] "))
                {
                    var parts = line.Split(new[] { "] [", "] " }, StringSplitOptions.None);

                    if (parts.Length >= 4)
                    {
                        var timestampStr = parts[0].Substring(1); // Remove first [
                        var levelStr = parts[1];
                        var threadStr = parts[2];
                        var message = string.Join("] ", parts.Skip(3));

                        if (DateTime.TryParse(timestampStr, out var timestamp) &&
                            Enum.TryParse<LogLevel>(levelStr, out var level) &&
                            threadStr.StartsWith("Thread-") &&
                            int.TryParse(threadStr.Substring(7), out var threadId))
                        {
                            logEntry = new LogEntry
                            {
                                Timestamp = timestamp,
                                Level = level,
                                ThreadId = threadId,
                                Message = message
                            };
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return false;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _flushTimer?.Dispose();

            try
            {
                _backgroundTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }

            _currentWriter?.Dispose();
            _writeSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }

    #region Enums and Models

    /// <summary>
    /// Log seviyesi
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    /// <summary>
    /// Log formatı
    /// </summary>
    public enum LogFormat
    {
        Text,
        Json,
        Xml
    }

    /// <summary>
    /// Log entry modeli
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public int ThreadId { get; set; }
    }

    /// <summary>
    /// Log dosya bilgisi
    /// </summary>
    public class LogFileInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long Size { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        public string FormattedSize
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = Size;
                int order = 0;

                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }
        }
    }

    #endregion
}