using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using YafesV2.Models;
using YafesV2.Installers;

namespace YafesV2.Managers
{
    /// <summary>
    /// Kurulum işlemlerini yöneten ana manager sınıfı
    /// V1'deki RuntimeManager'dan adapte edilmiştir
    /// </summary>
    public class InstallationManager : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<InstallationProgressEventArgs> GlobalProgressChanged;
        public event EventHandler<InstallationStatusEventArgs> GlobalStatusChanged;
        public event EventHandler<ProgramInstallationEventArgs> ProgramInstallationStarted;
        public event EventHandler<ProgramInstallationEventArgs> ProgramInstallationCompleted;
        public event EventHandler<InstallationCompletedEventArgs> AllInstallationsCompleted;

        #endregion

        #region Properties

        private int _globalProgress;
        public int GlobalProgress
        {
            get => _globalProgress;
            private set
            {
                _globalProgress = value;
                OnPropertyChanged(nameof(GlobalProgress));
                OnGlobalProgressChanged(value);
            }
        }

        private string _globalStatus;
        public string GlobalStatus
        {
            get => _globalStatus;
            private set
            {
                _globalStatus = value;
                OnPropertyChanged(nameof(GlobalStatus));
                OnGlobalStatusChanged(value);
            }
        }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            private set
            {
                _isInstalling = value;
                OnPropertyChanged(nameof(IsInstalling));
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                _isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
            }
        }

        private bool _isCancelled;
        public bool IsCancelled
        {
            get => _isCancelled;
            private set
            {
                _isCancelled = value;
                OnPropertyChanged(nameof(IsCancelled));
            }
        }

        /// <summary>
        /// Tüm kullanılabilir programlar
        /// </summary>
        public ObservableCollection<ProgramInfo> AvailablePrograms { get; private set; }

        /// <summary>
        /// Seçilen programlar (kurulacak)
        /// </summary>
        public ObservableCollection<ProgramInfo> SelectedPrograms { get; private set; }

        /// <summary>
        /// Kurulum sonuçları
        /// </summary>
        public Dictionary<string, InstallationResult> InstallationResults { get; private set; }

        /// <summary>
        /// Aktif installer'lar
        /// </summary>
        private Dictionary<string, BaseInstaller> ActiveInstallers { get; set; }

        /// <summary>
        /// Maksimum eşzamanlı kurulum sayısı
        /// </summary>
        public int MaxConcurrentInstallations { get; set; } = 3;

        /// <summary>
        /// Retry count for failed installations
        /// </summary>
        public int MaxRetryCount { get; set; } = 2;

        #endregion

        #region Constructor

        public InstallationManager()
        {
            AvailablePrograms = new ObservableCollection<ProgramInfo>();
            SelectedPrograms = new ObservableCollection<ProgramInfo>();
            InstallationResults = new Dictionary<string, InstallationResult>();
            ActiveInstallers = new Dictionary<string, BaseInstaller>();

            GlobalStatus = "Hazır";
            GlobalProgress = 0;

            // V1'den gelen temel programları yükle
            LoadDefaultPrograms();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Seçilen programları kurar - V1'den adapte
        /// </summary>
        public async Task<bool> InstallSelectedProgramsAsync()
        {
            if (IsInstalling)
            {
                GlobalStatus = "Kurulum zaten devam ediyor";
                return false;
            }

            if (!SelectedPrograms.Any())
            {
                GlobalStatus = "Kurulacak program seçilmedi";
                return false;
            }

            IsInstalling = true;
            IsCancelled = false;
            GlobalProgress = 0;
            InstallationResults.Clear();

            try
            {
                GlobalStatus = "Kurulum başlatılıyor...";

                // Bağımlılıkları çöz ve sırala
                var sortedPrograms = ResolveDependencies(SelectedPrograms.ToList());
                var totalPrograms = sortedPrograms.Count;
                var completedPrograms = 0;

                // Eşzamanlı kurulum grupları oluştur
                var installationGroups = CreateInstallationGroups(sortedPrograms);

                foreach (var group in installationGroups)
                {
                    if (IsCancelled) break;

                    // Grup içindeki programları eşzamanlı kur
                    var groupTasks = group.Select(program => InstallProgramAsync(program));
                    await Task.WhenAll(groupTasks);

                    completedPrograms += group.Count;
                    GlobalProgress = (completedPrograms * 100) / totalPrograms;
                }

                // Kurulum sonuçlarını değerlendir
                var successCount = InstallationResults.Values.Count(r => r == InstallationResult.Success || r == InstallationResult.AlreadyInstalled);
                var failureCount = InstallationResults.Values.Count(r => r != InstallationResult.Success && r != InstallationResult.AlreadyInstalled);

                GlobalStatus = IsCancelled ? "Kurulum iptal edildi" :
                              $"Kurulum tamamlandı: {successCount} başarılı, {failureCount} başarısız";
                GlobalProgress = 100;

                // Event fırlatma
                OnAllInstallationsCompleted(successCount, failureCount);

                return !IsCancelled && failureCount == 0;
            }
            catch (Exception ex)
            {
                GlobalStatus = $"Kurulum hatası: {ex.Message}";
                return false;
            }
            finally
            {
                IsInstalling = false;
                ActiveInstallers.Clear();
            }
        }

        /// <summary>
        /// Tek program kurar
        /// </summary>
        public async Task<InstallationResult> InstallProgramAsync(ProgramInfo program)
        {
            if (program == null)
                return InstallationResult.Error;

            try
            {
                // Event fırlatma
                OnProgramInstallationStarted(program);

                // Uygun installer'ı oluştur
                var installer = CreateInstaller(program);
                if (installer == null)
                {
                    InstallationResults[program.Name] = InstallationResult.Error;
                    return InstallationResult.Error;
                }

                // Event subscription
                installer.ProgressChanged += (s, e) => UpdateProgramProgress(program.Name, e.Progress);
                installer.StatusChanged += (s, e) => UpdateProgramStatus(program.Name, e.Status);

                // Installer'ı listeye ekle
                ActiveInstallers[program.Name] = installer;

                // Kurulumu başlat
                var result = await installer.InstallAsync();

                // Retry logic
                int retryCount = 0;
                while (result != InstallationResult.Success &&
                       result != InstallationResult.AlreadyInstalled &&
                       retryCount < MaxRetryCount)
                {
                    retryCount++;
                    GlobalStatus = $"{program.Name} yeniden deneniyor... ({retryCount}/{MaxRetryCount})";
                    await Task.Delay(2000); // Kısa bekleme
                    result = await installer.InstallAsync();
                }

                // Sonucu kaydet
                InstallationResults[program.Name] = result;

                // Event fırlatma
                OnProgramInstallationCompleted(program, result);

                return result;
            }
            catch (Exception ex)
            {
                GlobalStatus = $"{program.Name} kurulum hatası: {ex.Message}";
                InstallationResults[program.Name] = InstallationResult.Error;
                return InstallationResult.Error;
            }
            finally
            {
                // Installer'ı temizle
                if (ActiveInstallers.ContainsKey(program.Name))
                {
                    ActiveInstallers[program.Name]?.Dispose();
                    ActiveInstallers.Remove(program.Name);
                }
            }
        }

        /// <summary>
        /// Kurulumu duraklatır
        /// </summary>
        public void PauseInstallation()
        {
            IsPaused = true;
            GlobalStatus = "Kurulum duraklatıldı";
        }

        /// <summary>
        /// Kurulumu devam ettirir
        /// </summary>
        public void ResumeInstallation()
        {
            IsPaused = false;
            GlobalStatus = "Kurulum devam ediyor";
        }

        /// <summary>
        /// Kurulumu iptal eder
        /// </summary>
        public void CancelInstallation()
        {
            IsCancelled = true;
            GlobalStatus = "Kurulum iptal ediliyor...";

            // Aktif installer'ları durdur
            foreach (var installer in ActiveInstallers.Values)
            {
                installer?.Dispose();
            }
            ActiveInstallers.Clear();
        }

        /// <summary>
        /// Program seçer/seçimi kaldırır
        /// </summary>
        public void ToggleProgramSelection(ProgramInfo program)
        {
            if (SelectedPrograms.Contains(program))
            {
                SelectedPrograms.Remove(program);
            }
            else
            {
                SelectedPrograms.Add(program);
            }
        }

        /// <summary>
        /// Tüm programları seçer
        /// </summary>
        public void SelectAllPrograms()
        {
            SelectedPrograms.Clear();
            foreach (var program in AvailablePrograms.Where(p => p.IsActive))
            {
                SelectedPrograms.Add(program);
            }
        }

        /// <summary>
        /// Tüm seçimleri kaldırır
        /// </summary>
        public void ClearAllSelections()
        {
            SelectedPrograms.Clear();
        }

        /// <summary>
        /// Kategori bazında seçim yapar
        /// </summary>
        public void SelectProgramsByCategory(string category)
        {
            var categoryPrograms = AvailablePrograms.Where(p =>
                p.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true &&
                p.IsActive);

            foreach (var program in categoryPrograms)
            {
                if (!SelectedPrograms.Contains(program))
                {
                    SelectedPrograms.Add(program);
                }
            }
        }

        /// <summary>
        /// Kurulu programları kontrol eder
        /// </summary>
        public async Task CheckInstalledProgramsAsync()
        {
            GlobalStatus = "Kurulu programlar kontrol ediliyor...";

            var tasks = AvailablePrograms.Select(async program =>
            {
                var installer = CreateInstaller(program);
                if (installer != null)
                {
                    program.IsActive = !installer.IsInstalled();
                    installer.Dispose();
                }
            });

            await Task.WhenAll(tasks);
            GlobalStatus = "Kontrol tamamlandı";
        }

        #endregion

        #region Private Methods - V1'den Adapte

        /// <summary>
        /// Varsayılan programları yükler - V1'den adapte
        /// </summary>
        private void LoadDefaultPrograms()
        {
            // V1'deki programlar
            var defaultPrograms = new List<ProgramInfo>
            {
                new ProgramInfo
                {
                    Name = "Google Chrome",
                    Description = "Hızlı ve güvenli web tarayıcısı",
                    Category = "Browser",
                    DownloadUrl = "https://dl.google.com/chrome/install/ChromeStandaloneSetup64.exe",
                    FileName = "ChromeStandaloneSetup64.exe",
                    InstallArguments = "/silent /install",
                    Priority = 8,
                    IsSelectedByDefault = true
                },
                new ProgramInfo
                {
                    Name = "Opera",
                    Description = "Gelişmiş özelliklerle web tarayıcısı",
                    Category = "Browser",
                    DownloadUrl = "https://download.opera.com/download/get/?partner=www&opsys=windows",
                    FileName = "OperaSetup.exe",
                    InstallArguments = "/S /NORESTART",
                    Priority = 7
                },
                new ProgramInfo
                {
                    Name = "WinRAR",
                    Description = "Güçlü arşiv yöneticisi",
                    Category = "Utility",
                    DownloadUrl = "https://www.win-rar.com/fileadmin/winrar-versions/winrar/winrar-x64-611tr.exe",
                    FileName = "winrar-x64-611tr.exe",
                    InstallArguments = "/S",
                    Priority = 6
                },
                new ProgramInfo
                {
                    Name = "IObit Driver Booster",
                    Description = "Otomatik driver güncelleyici",
                    Category = "System",
                    DownloadUrl = "https://cdn.iobit.com/dl/driver_booster_setup.exe",
                    FileName = "driver_booster_setup.exe",
                    InstallArguments = "/VERYSILENT /NORESTART",
                    Priority = 5
                },
                new ProgramInfo
                {
                    Name = "Revo Uninstaller",
                    Description = "Gelişmiş program kaldırıcı",
                    Category = "Utility",
                    DownloadUrl = "https://download.revouninstaller.com/download/revosetup.exe",
                    FileName = "revosetup.exe",
                    InstallArguments = "/VERYSILENT /NORESTART",
                    Priority = 4
                }
            };

            foreach (var program in defaultPrograms)
            {
                AvailablePrograms.Add(program);
                if (program.IsSelectedByDefault)
                {
                    SelectedPrograms.Add(program);
                }
            }
        }

        /// <summary>
        /// Program için uygun installer oluşturur
        /// </summary>
        private BaseInstaller CreateInstaller(ProgramInfo program)
        {
            return program.Name.ToLower() switch
            {
                var name when name.Contains("opera") => new OperaPasswordManager
                {
                    EnablePasswordManager = true,
                    EnableSyncFeatures = true
                },
                var name when name.Contains("driver booster") => new DriverBoosterInstaller
                {
                    EnableAutoScan = true,
                    CreateDesktopShortcut = true
                },
                var name when name.Contains("winrar") => new WinRARInstaller
                {
                    AssociateArchiveFormats = true,
                    CreateDesktopShortcut = true
                },
                var name when name.Contains("revo") => new RevoUninstallerInstaller
                {
                    CreateDesktopShortcut = true,
                    EnableRealTimeMonitoring = true
                },
                _ => new ProgramInstaller(program)
            };
        }

        /// <summary>
        /// Bağımlılıkları çözer ve kurulum sırasını belirler
        /// </summary>
        private List<ProgramInfo> ResolveDependencies(List<ProgramInfo> programs)
        {
            // Öncelik sırasına göre sırala
            return programs.OrderByDescending(p => p.Priority).ToList();
        }

        /// <summary>
        /// Eşzamanlı kurulum grupları oluşturur
        /// </summary>
        private List<List<ProgramInfo>> CreateInstallationGroups(List<ProgramInfo> programs)
        {
            var groups = new List<List<ProgramInfo>>();
            var currentGroup = new List<ProgramInfo>();

            foreach (var program in programs)
            {
                currentGroup.Add(program);

                if (currentGroup.Count >= MaxConcurrentInstallations)
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<ProgramInfo>();
                }
            }

            if (currentGroup.Any())
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        /// <summary>
        /// Program kurulum ilerlemesini günceller
        /// </summary>
        private void UpdateProgramProgress(string programName, int progress)
        {
            // Program bazlı progress tracking burada yapılabilir
        }

        /// <summary>
        /// Program kurulum durumunu günceller
        /// </summary>
        private void UpdateProgramStatus(string programName, string status)
        {
            // Program bazlı status tracking burada yapılabilir
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnGlobalProgressChanged(int progress)
        {
            GlobalProgressChanged?.Invoke(this, new InstallationProgressEventArgs(progress));
        }

        protected virtual void OnGlobalStatusChanged(string status)
        {
            GlobalStatusChanged?.Invoke(this, new InstallationStatusEventArgs(status));
        }

        protected virtual void OnProgramInstallationStarted(ProgramInfo program)
        {
            ProgramInstallationStarted?.Invoke(this, new ProgramInstallationEventArgs(program));
        }

        protected virtual void OnProgramInstallationCompleted(ProgramInfo program, InstallationResult result)
        {
            ProgramInstallationCompleted?.Invoke(this, new ProgramInstallationEventArgs(program, result));
        }

        protected virtual void OnAllInstallationsCompleted(int successCount, int failureCount)
        {
            AllInstallationsCompleted?.Invoke(this, new InstallationCompletedEventArgs(successCount, failureCount));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // Aktif installer'ları temizle
            foreach (var installer in ActiveInstallers.Values)
            {
                installer?.Dispose();
            }
            ActiveInstallers.Clear();
        }

        #endregion
    }

    #region Event Args Classes

    public class ProgramInstallationEventArgs : EventArgs
    {
        public ProgramInfo Program { get; }
        public InstallationResult? Result { get; }

        public ProgramInstallationEventArgs(ProgramInfo program, InstallationResult? result = null)
        {
            Program = program;
            Result = result;
        }
    }

    public class InstallationCompletedEventArgs : EventArgs
    {
        public int SuccessCount { get; }
        public int FailureCount { get; }
        public int TotalCount => SuccessCount + FailureCount;

        public InstallationCompletedEventArgs(int successCount, int failureCount)
        {
            SuccessCount = successCount;
            FailureCount = failureCount;
        }
    }

    #endregion
}