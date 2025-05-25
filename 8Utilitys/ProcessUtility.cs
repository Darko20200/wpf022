// 8Utilities/ProcessUtility.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace YafesV2.Utilities
{
    /// <summary>
    /// V1'deki process işlemlerini modern hale getiren utility sınıfı
    /// V1'deki ExecuteInstallerAsync, process monitoring mantığından adapte edilmiştir
    /// </summary>
    public static class ProcessUtility
    {
        #region Process Execution

        /// <summary>
        /// V1'deki ExecuteInstallerAsync metodunun gelişmiş versiyonu
        /// </summary>
        public static async Task<ProcessResult> ExecuteAsync(string fileName, string arguments = "",
            ProcessExecutionOptions options = null, IProgress<ProcessProgressEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ProcessExecutionOptions();
            var result = new ProcessResult();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = !options.RedirectOutput,
                    CreateNoWindow = options.CreateNoWindow,
                    WindowStyle = options.WindowStyle,
                    WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(fileName),
                    RedirectStandardOutput = options.RedirectOutput,
                    RedirectStandardError = options.RedirectOutput,
                    RedirectStandardInput = options.RedirectInput
                };

                // Environment variables
                if (options.EnvironmentVariables?.Any() == true)
                {
                    foreach (var kvp in options.EnvironmentVariables)
                    {
                        startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }

                // Admin privileges
                if (options.RunAsAdministrator)
                {
                    startInfo.Verb = "runas";
                }

                result.StartTime = DateTime.Now;
                progress?.Report(new ProcessProgressEventArgs("Process başlatılıyor...", 0));

                using (var process = new Process { StartInfo = startInfo })
                {
                    // Output handling
                    if (options.RedirectOutput)
                    {
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                result.StandardOutput.Add(e.Data);
                                progress?.Report(new ProcessProgressEventArgs($"Output: {e.Data}", result.Progress));
                            }
                        };

                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                result.StandardError.Add(e.Data);
                                progress?.Report(new ProcessProgressEventArgs($"Error: {e.Data}", result.Progress));
                            }
                        };
                    }

                    process.Start();
                    result.ProcessId = process.Id;

                    if (options.RedirectOutput)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }

                    // Progress monitoring
                    var progressTask = MonitorProcessProgressAsync(process, options.Timeout, progress, cancellationToken);

                    // Wait for completion
                    if (options.Timeout.HasValue)
                    {
                        var completed = await WaitForExitAsync(process, options.Timeout.Value, cancellationToken);
                        if (!completed)
                        {
                            result.IsTimedOut = true;
                            result.ErrorMessage = $"Process {options.Timeout.Value.TotalSeconds} saniye içinde tamamlanmadı";

                            try
                            {
                                process.Kill();
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        await WaitForExitAsync(process, TimeSpan.FromMinutes(30), cancellationToken);
                    }

                    result.EndTime = DateTime.Now;
                    result.Duration = result.EndTime - result.StartTime;
                    result.ExitCode = process.HasExited ? process.ExitCode : -1;
                    result.IsSuccess = process.HasExited && process.ExitCode == 0;

                    if (!result.IsSuccess && result.ExitCode != 0)
                    {
                        result.ErrorMessage = $"Process hata kodu ile sonlandı: {result.ExitCode}";
                    }
                }

                progress?.Report(new ProcessProgressEventArgs("Process tamamlandı", 100));
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;

                if (result.StartTime != default)
                {
                    result.Duration = result.EndTime - result.StartTime;
                }
            }

            return result;
        }

        /// <summary>
        /// V1'deki silent installer execution mantığı
        /// </summary>
        public static async Task<ProcessResult> ExecuteSilentInstallerAsync(string installerPath,
            string silentArgs = "/S", TimeSpan? timeout = null,
            IProgress<ProcessProgressEventArgs> progress = null)
        {
            var options = new ProcessExecutionOptions
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Timeout = timeout ?? TimeSpan.FromMinutes(30),
                RunAsAdministrator = true
            };

            return await ExecuteAsync(installerPath, silentArgs, options, progress);
        }

        /// <summary>
        /// Batch/Script dosyası çalıştırır
        /// </summary>
        public static async Task<ProcessResult> ExecuteScriptAsync(string scriptContent,
            ScriptType scriptType = ScriptType.Batch, ProcessExecutionOptions options = null)
        {
            var tempFile = scriptType switch
            {
                ScriptType.Batch => Path.GetTempFileName() + ".bat",
                ScriptType.PowerShell => Path.GetTempFileName() + ".ps1",
                ScriptType.Python => Path.GetTempFileName() + ".py",
                _ => Path.GetTempFileName() + ".bat"
            };

            try
            {
                await File.WriteAllTextAsync(tempFile, scriptContent);

                var executor = scriptType switch
                {
                    ScriptType.Batch => "cmd.exe",
                    ScriptType.PowerShell => "powershell.exe",
                    ScriptType.Python => "python.exe",
                    _ => "cmd.exe"
                };

                var arguments = scriptType switch
                {
                    ScriptType.Batch => $"/c \"{tempFile}\"",
                    ScriptType.PowerShell => $"-ExecutionPolicy Bypass -File \"{tempFile}\"",
                    ScriptType.Python => $"\"{tempFile}\"",
                    _ => $"/c \"{tempFile}\""
                };

                return await ExecuteAsync(executor, arguments, options);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { }
            }
        }

        #endregion

        #region Process Monitoring

        /// <summary>
        /// V1'deki process monitoring mantığının gelişmiş versiyonu
        /// </summary>
        public static async Task<bool> WaitForProcessToExitAsync(string processName,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.Now.Add(timeout);

            while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return true;

                await Task.Delay(1000, cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// Process'in bitmesini bekler
        /// </summary>
        public static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => tcs.TrySetResult(true);

                if (process.HasExited)
                    return true;

                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var exitTask = tcs.Task;

                var completedTask = await Task.WhenAny(timeoutTask, exitTask);
                return completedTask == exitTask;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Process ilerlemesini monitor eder
        /// </summary>
        private static async Task MonitorProcessProgressAsync(Process process, TimeSpan? timeout,
            IProgress<ProcessProgressEventArgs> progress, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now;
                var endTime = timeout.HasValue ? startTime.Add(timeout.Value) : DateTime.MaxValue;

                while (!process.HasExited && DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
                {
                    var elapsed = DateTime.Now - startTime;
                    var progressPercentage = timeout.HasValue ?
                        Math.Min(95, (int)(elapsed.TotalMilliseconds / timeout.Value.TotalMilliseconds * 100)) : 0;

                    progress?.Report(new ProcessProgressEventArgs("Process çalışıyor...", progressPercentage));

                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch
            {
                // Ignore monitoring errors
            }
        }

        #endregion

        #region Process Information

        /// <summary>
        /// Çalışan process'leri listeler
        /// </summary>
        public static List<ProcessInfo> GetRunningProcesses(string nameFilter = null)
        {
            try
            {
                var processes = Process.GetProcesses();
                var result = new List<ProcessInfo>();

                foreach (var process in processes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(nameFilter) &&
                            !process.ProcessName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var processInfo = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            StartTime = process.StartTime,
                            WorkingSet = process.WorkingSet64,
                            PrivateMemory = process.PrivateMemorySize64,
                            VirtualMemory = process.VirtualMemorySize64,
                            ThreadCount = process.Threads.Count,
                            HandleCount = process.HandleCount,
                            SessionId = process.SessionId
                        };

                        try
                        {
                            processInfo.FileName = process.MainModule?.FileName;
                            processInfo.WindowTitle = process.MainWindowTitle;
                        }
                        catch
                        {
                            // Access denied for some system processes
                        }

                        result.Add(processInfo);
                    }
                    catch
                    {
                        // Skip processes that can't be accessed
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return result.OrderBy(p => p.Name).ToList();
            }
            catch
            {
                return new List<ProcessInfo>();
            }
        }

        /// <summary>
        /// Belirli process'i bulur
        /// </summary>
        public static ProcessInfo FindProcess(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return null;

                var process = processes[0];
                var result = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    StartTime = process.StartTime,
                    WorkingSet = process.WorkingSet64,
                    PrivateMemory = process.PrivateMemorySize64,
                    VirtualMemory = process.VirtualMemorySize64,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    SessionId = process.SessionId
                };

                try
                {
                    result.FileName = process.MainModule?.FileName;
                    result.WindowTitle = process.MainWindowTitle;
                }
                catch { }

                foreach (var p in processes)
                    p.Dispose();

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Process'i öldürür
        /// </summary>
        public static bool KillProcess(string processName, bool force = false)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    return true;

                foreach (var process in processes)
                {
                    try
                    {
                        if (force)
                        {
                            process.Kill();
                        }
                        else
                        {
                            process.CloseMainWindow();
                            if (!process.WaitForExit(5000))
                            {
                                process.Kill();
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Process'in çalışıp çalışmadığını kontrol eder
        /// </summary>
        public static bool IsProcessRunning(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var isRunning = processes.Length > 0;

                foreach (var process in processes)
                    process.Dispose();

                return isRunning;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Installer Utilities

        /// <summary>
        /// V1'deki installer process tracking mantığı
        /// </summary>
        public static async Task<ProcessResult> TrackInstallerProcessAsync(string installerPath,
            string processName, string arguments = "", TimeSpan? timeout = null,
            IProgress<ProcessProgressEventArgs> progress = null)
        {
            var options = new ProcessExecutionOptions
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                Timeout = timeout ?? TimeSpan.FromMinutes(30),
                RunAsAdministrator = true
            };

            // Installer'ı başlat
            var result = await ExecuteAsync(installerPath, arguments, options, progress);

            if (result.IsSuccess)
            {
                // Installer process'inin bitmesini bekle
                progress?.Report(new ProcessProgressEventArgs("Installer process takip ediliyor...", 90));

                var processExited = await WaitForProcessToExitAsync(processName,
                    TimeSpan.FromMinutes(10), CancellationToken.None);

                if (processExited)
                {
                    progress?.Report(new ProcessProgressEventArgs("Kurulum tamamlandı", 100));
                }
                else
                {
                    result.ErrorMessage += " Process beklenen sürede sonlanmadı.";
                }
            }

            return result;
        }

        /// <summary>
        /// MSI installer çalıştırır
        /// </summary>
        public static async Task<ProcessResult> ExecuteMsiInstallerAsync(string msiPath,
            MsiInstallOptions options = null, IProgress<ProcessProgressEventArgs> progress = null)
        {
            options ??= new MsiInstallOptions();

            var arguments = $"/i \"{msiPath}\"";

            if (options.Silent)
                arguments += " /quiet";

            if (options.NoRestart)
                arguments += " /norestart";

            if (!string.IsNullOrEmpty(options.LogFile))
                arguments += $" /l*v \"{options.LogFile}\"";

            if (options.Properties?.Any() == true)
            {
                foreach (var prop in options.Properties)
                {
                    arguments += $" {prop.Key}={prop.Value}";
                }
            }

            var execOptions = new ProcessExecutionOptions
            {
                CreateNoWindow = options.Silent,
                WindowStyle = options.Silent ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                Timeout = options.Timeout ?? TimeSpan.FromMinutes(30),
                RunAsAdministrator = true
            };

            return await ExecuteAsync("msiexec.exe", arguments, execOptions, progress);
        }

        /// <summary>
        /// Registry'den uninstaller çalıştırır
        /// </summary>
        public static async Task<ProcessResult> ExecuteUninstallerAsync(string programName,
            bool silent = true, IProgress<ProcessProgressEventArgs> progress = null)
        {
            try
            {
                var uninstallString = PathUtility.FindProgramInRegistry(programName);
                if (string.IsNullOrEmpty(uninstallString))
                {
                    return new ProcessResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Uninstaller bulunamadı"
                    };
                }

                var arguments = silent ? "/S /NORESTART" : "";

                var options = new ProcessExecutionOptions
                {
                    CreateNoWindow = silent,
                    WindowStyle = silent ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    Timeout = TimeSpan.FromMinutes(15),
                    RunAsAdministrator = true
                };

                return await ExecuteAsync(uninstallString, arguments, options, progress);
            }
            catch (Exception ex)
            {
                return new ProcessResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region System Utilities

        /// <summary>
        /// Sistem yeniden başlatır
        /// </summary>
        public static async Task<ProcessResult> RestartSystemAsync(int delaySeconds = 10)
        {
            var arguments = $"/r /t {delaySeconds}";

            var options = new ProcessExecutionOptions
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RunAsAdministrator = true
            };

            return await ExecuteAsync("shutdown.exe", arguments, options);
        }

        /// <summary>
        /// Sistem kapanır
        /// </summary>
        public static async Task<ProcessResult> ShutdownSystemAsync(int delaySeconds = 10)
        {
            var arguments = $"/s /t {delaySeconds}";

            var options = new ProcessExecutionOptions
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RunAsAdministrator = true
            };

            return await ExecuteAsync("shutdown.exe", arguments, options);
        }

        /// <summary>
        /// Sistem restore point oluşturur
        /// </summary>
        public static async Task<ProcessResult> CreateSystemRestorePointAsync(string description)
        {
            var scriptContent = $@"
Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'
";

            var options = new ProcessExecutionOptions
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RunAsAdministrator = true,
                RedirectOutput = true
            };

            return await ExecuteScriptAsync(scriptContent, ScriptType.PowerShell, options);
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// Process execution seçenekleri
    /// </summary>
    public class ProcessExecutionOptions
    {
        public bool CreateNoWindow { get; set; } = true;
        public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Hidden;
        public string WorkingDirectory { get; set; }
        public TimeSpan? Timeout { get; set; }
        public bool RunAsAdministrator { get; set; } = false;
        public bool RedirectOutput { get; set; } = false;
        public bool RedirectInput { get; set; } = false;
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }

    /// <summary>
    /// MSI install seçenekleri
    /// </summary>
    public class MsiInstallOptions
    {
        public bool Silent { get; set; } = true;
        public bool NoRestart { get; set; } = true;
        public string LogFile { get; set; }
        public TimeSpan? Timeout { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    /// <summary>
    /// Process execution sonucu
    /// </summary>
    public class ProcessResult
    {
        public bool IsSuccess { get; set; }
        public int ExitCode { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int ProcessId { get; set; }
        public bool IsTimedOut { get; set; }
        public List<string> StandardOutput { get; set; } = new List<string>();
        public List<string> StandardError { get; set; } = new List<string>();
        public int Progress { get; set; }
    }

    /// <summary>
    /// Process bilgisi
    /// </summary>
    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
        public string WindowTitle { get; set; }
        public DateTime StartTime { get; set; }
        public long WorkingSet { get; set; }
        public long PrivateMemory { get; set; }
        public long VirtualMemory { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int SessionId { get; set; }

        public string FormattedWorkingSet => FileUtility.FormatFileSize(WorkingSet);
        public string FormattedPrivateMemory => FileUtility.FormatFileSize(PrivateMemory);
        public string FormattedVirtualMemory => FileUtility.FormatFileSize(VirtualMemory);
        public TimeSpan RunningTime => DateTime.Now - StartTime;
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Process progress event args
    /// </summary>
    public class ProcessProgressEventArgs : EventArgs
    {
        public string Message { get; }
        public int Progress { get; }
        public DateTime Timestamp { get; }

        public ProcessProgressEventArgs(string message, int progress)
        {
            Message = message;
            Progress = Math.Max(0, Math.Min(100, progress));
            Timestamp = DateTime.Now;
        }
    }

    #endregion

    #region Enums

    public enum ScriptType
    {
        Batch,
        PowerShell,
        Python
    }

    #endregion
}