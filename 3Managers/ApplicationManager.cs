using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;

namespace YafesV2.Managers
{
    /// <summary>
    /// Uygulama yaşam döngüsünü yöneten manager sınıfı
    /// Başlangıç, kapatma, güncelleme, hata yönetimi
    /// </summary>
    public class ApplicationManager : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ApplicationStateChangedEventArgs> StateChanged;
        public event EventHandler<ApplicationErrorEventArgs> ApplicationError;
        public event EventHandler ApplicationStartup;
        public event EventHandler ApplicationShutdown;

        #endregion

        #region Properties

        /// <summary>
        /// Uygulama durumu
        /// </summary>
        private ApplicationState _currentState;
        public ApplicationState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var previousState = _currentState;
                    _currentState = value;
                    OnPropertyChanged(nameof(CurrentState));
                    OnStateChanged(previousState, value);
                }
            }
        }

        /// <summary>
        /// Uygulama başlangıç zamanı
        /// </summary>
        public DateTime StartupTime { get; private set; }

        /// <summary>
        /// Uygulama sürümü
        /// </summary>
        public Version ApplicationVersion { get; private set; }

        /// <summary>
        /// Çalışma dizini
        /// </summary>
        public string WorkingDirectory { get; private set; }

        /// <summary>
        /// Temp dizini
        /// </summary>
        public string TempDirectory { get; private set; }

        /// <summary>
        /// Data dizini
        /// </summary>
        public string DataDirectory { get; private set; }

        /// <summary>
        /// Config dizini
        /// </summary>
        public string ConfigDirectory { get; private set; }

        /// <summary>
        /// Logs dizini
        /// </summary>
        public string LogsDirectory { get; private set; }

        /// <summary>
        /// Tek instance kontrolü
        /// </summary>
        private System.Threading.Mutex _singleInstanceMutex;
        public bool IsSingleInstanceRunning { get; private set; }

        /// <summary>
        /// Manager instances
        /// </summary>
        public ConfigurationManager ConfigurationManager { get; private set; }
        public LogManager LogManager { get; private set; }
        public InstallationManager InstallationManager { get; private set; }

        #endregion

        #region Singleton

        private static ApplicationManager _instance;
        private static readonly object _lock = new object();

        public static ApplicationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ApplicationManager();
                    }
                }
                return _instance;
            }
        }

        private ApplicationManager()
        {
            Initialize();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Uygulamayı başlatır
        /// </summary>
        public async Task<bool> StartupAsync()
        {
            try
            {
                CurrentState = ApplicationState.Starting;
                StartupTime = DateTime.Now;

                // Tek instance kontrolü
                if (!CheckSingleInstance())
                {
                    CurrentState = ApplicationState.Error;
                    return false;
                }

                // Dizinleri oluştur
                CreateDirectories();

                // Manager'ları başlat
                await InitializeManagersAsync();

                // Global exception handling
                SetupGlobalExceptionHandling();

                CurrentState = ApplicationState.Running;
                OnApplicationStartup();

                return true;
            }
            catch (Exception ex)
            {
                CurrentState = ApplicationState.Error;
                OnApplicationError("Startup failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Uygulamayı kapatır
        /// </summary>
        public async Task ShutdownAsync()
        {
            try
            {
                CurrentState = ApplicationState.Shutting_Down;

                // Manager'ları temizle
                await DisposeManagersAsync();

                // Geçici dosyaları temizle
                CleanupTempFiles();

                // Mutex'i serbest bırak
                ReleaseSingleInstanceMutex();

                CurrentState = ApplicationState.Stopped;
                OnApplicationShutdown();
            }
            catch (Exception ex)
            {
                OnApplicationError("Shutdown failed", ex);
            }
        }

        /// <summary>
        /// Uygulamayı yeniden başlatır
        /// </summary>
        public async Task RestartAsync()
        {
            try
            {
                CurrentState = ApplicationState.Restarting;

                // Mevcut instance'ı kapat
                await ShutdownAsync();

                // Yeni instance başlat
                var exePath = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(exePath);

                // Mevcut instance'ı kapat
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                OnApplicationError("Restart failed", ex);
            }
        }

        /// <summary>
        /// Güncelleme kontrolü yapar
        /// </summary>
        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                // TODO: Update service entegrasyonu
                await Task.Delay(1000); // Placeholder
                return false;
            }
            catch (Exception ex)
            {
                OnApplicationError("Update check failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Sistem bilgilerini alır
        /// </summary>
        public SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                Framework = Environment.Version.ToString(),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                TotalPhysicalMemory = GetTotalPhysicalMemory(),
                AvailablePhysicalMemory = GetAvailablePhysicalMemory(),
                ApplicationVersion = ApplicationVersion.ToString(),
                ApplicationPath = Assembly.GetExecutingAssembly().Location,
                WorkingDirectory = WorkingDirectory,
                StartupTime = StartupTime,
                Uptime = DateTime.Now - StartupTime
            };
        }

        /// <summary>
        /// Performans bilgilerini alır
        /// </summary>
        public PerformanceInfo GetPerformanceInfo()
        {
            var process = Process.GetCurrentProcess();

            return new PerformanceInfo
            {
                CpuUsage = GetCpuUsage(),
                MemoryUsage = process.WorkingSet64,
                PrivateMemoryUsage = process.PrivateMemorySize64,
                VirtualMemoryUsage = process.VirtualMemorySize64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                GCMemory = GC.GetTotalMemory(false),
                GCGeneration0Collections = GC.CollectionCount(0),
                GCGeneration1Collections = GC.CollectionCount(1),
                GCGeneration2Collections = GC.CollectionCount(2)
            };
        }

        /// <summary>
        /// Garbage collection tetikler
        /// </summary>
        public void ForceGarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Emergency shutdown - kritik hatalar için
        /// </summary>
        public void EmergencyShutdown(string reason)
        {
            try
            {
                LogManager?.LogCritical($"Emergency shutdown: {reason}");

                // Kritik verileri kaydet
                ConfigurationManager?.SaveConfiguration();

                // Hızlı temizlik
                ReleaseSingleInstanceMutex();

                Environment.Exit(1);
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Başlangıç inicializasyonu
        /// </summary>
        private void Initialize()
        {
            CurrentState = ApplicationState.Initializing;

            // Version bilgisi
            ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version;

            // Dizin yolları
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            WorkingDirectory = Path.Combine(appDataPath, "YafesV2");
            TempDirectory = Path.Combine(WorkingDirectory, "Temp");
            DataDirectory = Path.Combine(WorkingDirectory, "Data");
            ConfigDirectory = Path.Combine(WorkingDirectory, "Config");
            LogsDirectory = Path.Combine(WorkingDirectory, "Logs");
        }

        /// <summary>
        /// Tek instance kontrolü
        /// </summary>
        private bool CheckSingleInstance()
        {
            try
            {
                _singleInstanceMutex = new System.Threading.Mutex(true, "YafesV2_SingleInstance", out bool createdNew);
                IsSingleInstanceRunning = createdNew;
                return createdNew;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gerekli dizinleri oluşturur
        /// </summary>
        private void CreateDirectories()
        {
            Directory.CreateDirectory(WorkingDirectory);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(ConfigDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        /// <summary>
        /// Manager'ları başlatır
        /// </summary>
        private async Task InitializeManagersAsync()
        {
            // Configuration Manager
            ConfigurationManager = new ConfigurationManager();

            // Log Manager
            LogManager = new LogManager(LogsDirectory);
            await LogManager.InitializeAsync();

            // Installation Manager
            InstallationManager = new InstallationManager();
        }

        /// <summary>
        /// Manager'ları temizler
        /// </summary>
        private async Task DisposeManagersAsync()
        {
            try
            {
                ConfigurationManager?.SaveConfiguration();
                InstallationManager?.Dispose();

                if (LogManager != null)
                {
                    await LogManager.FlushAsync();
                    LogManager.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Manager disposal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Global exception handling ayarlar
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                OnApplicationError("Unhandled exception", ex);

                if (e.IsTerminating)
                {
                    EmergencyShutdown($"Terminating exception: {ex?.Message}");
                }
            };

            Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                OnApplicationError("UI thread exception", e.Exception);
                e.Handled = true; // Uygulamanın çökmesini engelle
            };
        }

        /// <summary>
        /// Geçici dosyaları temizler
        /// </summary>
        private void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(TempDirectory))
                {
                    var files = Directory.GetFiles(TempDirectory);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Ignore individual file deletion errors
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Single instance mutex'ini serbest bırakır
        /// </summary>
        private void ReleaseSingleInstanceMutex()
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch
            {
                // Ignore mutex release errors
            }
        }

        /// <summary>
        /// CPU kullanımını hesaplar
        /// </summary>
        private double GetCpuUsage()
        {
            // Basit CPU kullanım hesaplaması
            // Gerçek implementasyonda PerformanceCounter kullanılabilir
            return 0.0;
        }

        /// <summary>
        /// Toplam fiziksel belleği alır
        /// </summary>
        private long GetTotalPhysicalMemory()
        {
            // WMI veya Performance Counter kullanarak alınabilir
            return 0;
        }

        /// <summary>
        /// Kullanılabilir fiziksel belleği alır
        /// </summary>
        private long GetAvailablePhysicalMemory()
        {
            // WMI veya Performance Counter kullanarak alınabilir
            return 0;
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnStateChanged(ApplicationState previousState, ApplicationState newState)
        {
            StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(previousState, newState));
        }

        protected virtual void OnApplicationError(string message, Exception exception)
        {
            ApplicationError?.Invoke(this, new ApplicationErrorEventArgs(message, exception));
            LogManager?.LogError($"{message}: {exception?.Message}", exception);
        }

        protected virtual void OnApplicationStartup()
        {
            ApplicationStartup?.Invoke(this, EventArgs.Empty);
            LogManager?.LogInformation("Application started successfully");
        }

        protected virtual void OnApplicationShutdown()
        {
            ApplicationShutdown?.Invoke(this, EventArgs.Empty);
            LogManager?.LogInformation("Application shutdown completed");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Task.Run(async () => await ShutdownAsync()).Wait();
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// Uygulama durumu enum'u
    /// </summary>
    public enum ApplicationState
    {
        Initializing,
        Starting,
        Running,
        Paused,
        Shutting_Down,
        Stopped,
        Restarting,
        Error
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Sistem bilgileri modeli
    /// </summary>
    public class SystemInfo
    {
        public string OperatingSystem { get; set; }
        public string Framework { get; set; }
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public long TotalPhysicalMemory { get; set; }
        public long AvailablePhysicalMemory { get; set; }
        public string ApplicationVersion { get; set; }
        public string ApplicationPath { get; set; }
        public string WorkingDirectory { get; set; }
        public DateTime StartupTime { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    /// <summary>
    /// Performans bilgileri modeli
    /// </summary>
    public class PerformanceInfo
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long PrivateMemoryUsage { get; set; }
        public long VirtualMemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long GCMemory { get; set; }
        public int GCGeneration0Collections { get; set; }
        public int GCGeneration1Collections { get; set; }
        public int GCGeneration2Collections { get; set; }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Uygulama durum değişikliği event args
    /// </summary>
    public class ApplicationStateChangedEventArgs : EventArgs
    {
        public ApplicationState PreviousState { get; }
        public ApplicationState NewState { get; }
        public DateTime Timestamp { get; }

        public ApplicationStateChangedEventArgs(ApplicationState previousState, ApplicationState newState)
        {
            PreviousState = previousState;
            NewState = newState;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Uygulama hata event args
    /// </summary>
    public class ApplicationErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }
        public DateTime Timestamp { get; }

        public ApplicationErrorEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
            Timestamp = DateTime.Now;
        }
    }

    #endregion
}