// Repository/DriverRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YafesV2.Models;

namespace YafesV2.Repository
{
    public class DriverRepository
    {
        private List<DriverInfo> _drivers;

        public DriverRepository()
        {
            _drivers = new List<DriverInfo>();
            InitializeDrivers();
        }

        private void InitializeDrivers()
        {
            // Eski Main.xaml.cs'teki sürücü listesini buraya taşıyoruz
            _drivers.Clear();

            _drivers.Add(new DriverInfo
            {
                Name = "NVIDIA Graphics Driver",
                Url = "https://tr.download.nvidia.com/Windows/576.40/576.40-desktop-win10-win11-64bit-international-dch-whql.exe",
                FileName = "nvidia_driver.exe",
                ProcessName = "setup",
                InstallArguments = "/s /n",
                IsZip = false,
                AlternativeSearchPattern = "nvidia*.exe",
                ResourceName = "Yafes.Resources.nvidia_driver.exe"
            });

            _drivers.Add(new DriverInfo
            {
                Name = "Realtek PCIe LAN Driver",
                Url = "https://download.msi.com/dvr_exe/mb/realtek_pcielan_w10.zip",
                FileName = "realtek_lan.zip",
                ProcessName = "setup",
                InstallArguments = "/s",
                IsZip = true,
                AlternativeSearchPattern = "*lan*.zip",
                ResourceName = "Yafes.Resources.realtek_pcielan_w10.zip"
            });

            _drivers.Add(new DriverInfo
            {
                Name = "Realtek Audio Driver",
                Url = "https://download.msi.com/dvr_exe/mb/realtek_audio_R.zip",
                FileName = "realtek_audio.zip",
                ProcessName = "setup",
                InstallArguments = "/s",
                IsZip = true,
                AlternativeSearchPattern = "*audio*.zip",
                ResourceName = "Yafes.Resources.realtek_audio_R.zip"
            });
        }

        /// <summary>
        /// Tüm sürücüleri getirir
        /// </summary>
        public List<DriverInfo> GetAllDrivers()
        {
            return _drivers.ToList();
        }

        /// <summary>
        /// İsme göre sürücü bulur
        /// </summary>
        public DriverInfo GetDriverByName(string name)
        {
            return _drivers.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Yeni sürücü ekler
        /// </summary>
        public bool AddDriver(DriverInfo driver)
        {
            try
            {
                if (driver == null || string.IsNullOrEmpty(driver.Name))
                    return false;

                // Aynı isimde sürücü var mı kontrol et
                if (_drivers.Any(d => d.Name.Equals(driver.Name, StringComparison.OrdinalIgnoreCase)))
                    return false;

                _drivers.Add(driver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sürücü günceller
        /// </summary>
        public bool UpdateDriver(DriverInfo driver)
        {
            try
            {
                var existingDriver = _drivers.FirstOrDefault(d => d.Name.Equals(driver.Name, StringComparison.OrdinalIgnoreCase));
                if (existingDriver != null)
                {
                    var index = _drivers.IndexOf(existingDriver);
                    _drivers[index] = driver;
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
        /// Sürücü siler
        /// </summary>
        public bool RemoveDriver(string name)
        {
            try
            {
                var driver = _drivers.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (driver != null)
                {
                    _drivers.Remove(driver);
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
        /// Sürücü arama yapar
        /// </summary>
        public List<DriverInfo> SearchDrivers(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return GetAllDrivers();

            return _drivers.Where(d =>
                d.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                d.FileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        /// <summary>
        /// Toplam sürücü sayısını döndürür
        /// </summary>
        public int GetDriverCount()
        {
            return _drivers.Count;
        }

        /// <summary>
        /// Sürücü listesini temizler
        /// </summary>
        public void ClearDrivers()
        {
            _drivers.Clear();
        }

        /// <summary>
        /// Sürücü listesini yeniden başlatır (varsayılan sürücüleri yükler)
        /// </summary>
        public void ResetToDefaults()
        {
            ClearDrivers();
            InitializeDrivers();
        }
    }
}