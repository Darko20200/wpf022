// Repository/ProgramRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YafesV2.Models;

namespace YafesV2.Repository
{
    public class ProgramRepository
    {
        private List<ProgramInfo> _programs;

        public ProgramRepository()
        {
            _programs = new List<ProgramInfo>();
            InitializePrograms();
        }

        private void InitializePrograms()
        {
            // Eski Main.xaml.cs'teki program listesini buraya taşıyoruz
            _programs.Clear();

            _programs.Add(new ProgramInfo
            {
                Name = "Discord",
                Url = "https://discord.com/api/downloads/distributions/app/installers/latest?channel=stable&platform=win&arch=x64",
                FileName = "DiscordSetup.exe",
                ProcessName = "DiscordSetup",
                InstallArguments = "-s",
                IsZip = false,
                AlternativeSearchPattern = "discord*.exe",
                ResourceName = "Yafes.Resources.DiscordSetup.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "WinRAR",
                Url = "https://www.win-rar.com/postdownload.html?&L=5",
                FileName = "winrar-x64-711tr.exe",
                ProcessName = "WinRAR",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "winrar*.exe",
                ResourceName = "Yafes.Resources.winrar-x64-711tr.exe",
                SpecialInstallation = true
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Opera",
                Url = "https://www.opera.com/tr/computer/thanks?ni=stable&os=windows",
                FileName = "OperaSetup.exe",
                ProcessName = "opera",
                InstallArguments = "--silent --installfolder=\"C:\\Program Files\\Opera\"",
                IsZip = false,
                AlternativeSearchPattern = "opera*.exe",
                ResourceName = "Yafes.Resources.OperaSetup.exe",
                SpecialInstallation = true
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Steam",
                Url = "https://cdn.fastly.steamstatic.com/client/installer/SteamSetup.exe",
                FileName = "steam_installer.exe",
                ProcessName = "Steam",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "steam*.exe",
                ResourceName = "Yafes.Resources.steam_installer.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Lightshot",
                Url = "https://app.prntscr.com/build/setup-lightshot.exe",
                FileName = "lightshot_installer.exe",
                ProcessName = "setup-lightshot",
                InstallArguments = "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL",
                IsZip = false,
                AlternativeSearchPattern = "*lightshot*.exe",
                ResourceName = "Yafes.Resources.lightshot_installer.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Notepad++",
                Url = "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.7.7/npp.8.7.7.Installer.x64.exe",
                FileName = "npp_installer.exe",
                ProcessName = "notepad++",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "npp*.exe",
                ResourceName = "Yafes.Resources.npp_installer.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Visual Studio Setup",
                Url = "",
                FileName = "VisualStudioSetup.exe",
                ProcessName = "VisualStudioSetup",
                InstallArguments = "/quiet",
                IsZip = false,
                AlternativeSearchPattern = "VisualStudioSetup*.exe",
                ResourceName = "Yafes.Resources.VisualStudioSetup.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "uTorrent",
                Url = "",
                FileName = "uTorrent 3.6.0.47196.exe",
                ProcessName = "uTorrent 3.6.0.47196",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "uTorrent*.exe",
                ResourceName = "Yafes.Resources.uTorrent 3.6.0.47196.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "EA App",
                Url = "",
                FileName = "EAappInstaller.exe",
                ProcessName = "EAappInstaller",
                InstallArguments = "/quiet",
                IsZip = false,
                AlternativeSearchPattern = "EAapp*.exe",
                ResourceName = "Yafes.Resources.EAappInstaller.exe",
                SpecialInstallation = false
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Driver Booster",
                Url = "",
                FileName = "driver_booster_setup.exe",
                ProcessName = "driver_booster_setup",
                InstallArguments = "/VERYSILENT /NORESTART /NoAutoRun",
                IsZip = false,
                AlternativeSearchPattern = "driver_booster*.exe",
                ResourceName = "Yafes.Resources.driver_booster_setup.exe",
                SpecialInstallation = true
            });

            _programs.Add(new ProgramInfo
            {
                Name = "Revo Uninstaller Pro",
                Url = "",
                FileName = "RevoUninProSetup.exe",
                ProcessName = "RevoUninProSetup",
                InstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                IsZip = false,
                AlternativeSearchPattern = "RevoUnin*.exe",
                ResourceName = "Yafes.Resources.RevoUninProSetup.exe",
                SpecialInstallation = true
            });
        }

        /// <summary>
        /// Tüm programları getirir
        /// </summary>
        public List<ProgramInfo> GetAllPrograms()
        {
            return _programs.ToList();
        }

        /// <summary>
        /// İsme göre program bulur
        /// </summary>
        public ProgramInfo GetProgramByName(string name)
        {
            return _programs.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Özel kurulum gerektiren programları getirir
        /// </summary>
        public List<ProgramInfo> GetSpecialInstallationPrograms()
        {
            return _programs.Where(p => p.SpecialInstallation).ToList();
        }

        /// <summary>
        /// Normal kurulum programlarını getirir
        /// </summary>
        public List<ProgramInfo> GetNormalInstallationPrograms()
        {
            return _programs.Where(p => !p.SpecialInstallation).ToList();
        }

        /// <summary>
        /// Yeni program ekler
        /// </summary>
        public bool AddProgram(ProgramInfo program)
        {
            try
            {
                if (program == null || string.IsNullOrEmpty(program.Name))
                    return false;

                // Aynı isimde program var mı kontrol et
                if (_programs.Any(p => p.Name.Equals(program.Name, StringComparison.OrdinalIgnoreCase)))
                    return false;

                _programs.Add(program);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Program günceller
        /// </summary>
        public bool UpdateProgram(ProgramInfo program)
        {
            try
            {
                var existingProgram = _programs.FirstOrDefault(p => p.Name.Equals(program.Name, StringComparison.OrdinalIgnoreCase));
                if (existingProgram != null)
                {
                    var index = _programs.IndexOf(existingProgram);
                    _programs[index] = program;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Program siler
        /// </summary>
        public bool RemoveProgram(string name)
        {
            try
            {
                var program = _programs.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (program != null)
                {
                    _programs.Remove(program);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Program arama yapar
        /// </summary>
        public List<ProgramInfo> SearchPrograms(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return GetAllPrograms();

            return _programs.Where(p =>
                p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.FileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        /// <summary>
        /// Toplam program sayısını döndürür
        /// </summary>
        public int GetProgramCount()
        {
            return _programs.Count;
        }

        /// <summary>
        /// Program listesini temizler
        /// </summary>
        public void ClearPrograms()
        {
            _programs.Clear();
        }

        /// <summary>
        /// Program listesini yeniden başlatır (varsayılan programları yükler)
        /// </summary>
        public void ResetToDefaults()
        {
            ClearPrograms();
            InitializePrograms();
        }

        /// <summary>
        /// Kategoriye göre program filtrele (gelecekte category eklenebilir)
        /// </summary>
        public List<ProgramInfo> GetProgramsByCategory(string category)
        {
            // Şimdilik basit kategorizasyon yapıyoruz
            var categoryPrograms = new List<ProgramInfo>();

            switch (category.ToLower())
            {
                case "communication":
                    categoryPrograms.AddRange(_programs.Where(p => p.Name.Contains("Discord")));
                    break;
                case "utilities":
                    categoryPrograms.AddRange(_programs.Where(p =>
                        p.Name.Contains("WinRAR") ||
                        p.Name.Contains("Lightshot") ||
                        p.Name.Contains("Notepad++")));
                    break;
                case "browsers":
                    categoryPrograms.AddRange(_programs.Where(p => p.Name.Contains("Opera")));
                    break;
                case "gaming":
                    categoryPrograms.AddRange(_programs.Where(p =>
                        p.Name.Contains("Steam") ||
                        p.Name.Contains("EA App")));
                    break;
                case "system":
                    categoryPrograms.AddRange(_programs.Where(p =>
                        p.Name.Contains("Driver Booster") ||
                        p.Name.Contains("Revo Uninstaller")));
                    break;
                case "development":
                    categoryPrograms.AddRange(_programs.Where(p =>
                        p.Name.Contains("Visual Studio")));
                    break;
                default:
                    return GetAllPrograms();
            }

            return categoryPrograms;
        }
    }
}