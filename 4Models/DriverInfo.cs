// Models/DriverInfo.cs
using System;

namespace YafesV2.Models
{
    /// <summary>
    /// Sürücü bilgilerini temsil eden model sınıfı
    /// Eski Main.xaml.cs'teki DriverInfo class'ından alınmıştır
    /// </summary>
    public class DriverInfo
    {

        /// <summary>
        /// Cihaz ID'si (Windows Device Manager için)
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Üretici firma adı
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Mevcut yüklü versiyon
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// En son sürüm
        /// </summary>
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>
        /// Güncelleme mevcut mu
        /// </summary>
        public bool HasUpdate { get; set; } = false;
        /// <summary>
        /// Sürücünün görünen adı
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// İndirme URL'i
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Dosya adı (kurulum dosyası)
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Kurulum sürecinin process adı
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Kurulum komut satırı parametreleri
        /// </summary>
        public string InstallArguments { get; set; } = string.Empty;

        /// <summary>
        /// Dosyanın ZIP formatında olup olmadığı
        /// </summary>
        public bool IsZip { get; set; }

        /// <summary>
        /// Alternatif klasörde arama yaparken kullanılacak desen
        /// </summary>
        public string AlternativeSearchPattern { get; set; } = string.Empty;

        /// <summary>
        /// Gömülü kaynak adı (Resources'dan çıkarmak için)
        /// </summary>
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Sürücünün kategorisi (örn: "Graphics", "Audio", "Network")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Sürücünün gerekli olup olmadığı
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Sürücü açıklaması
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Sürücü versiyonu
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Dosya boyutu (byte cinsinden)
        /// </summary>
        public long FileSize { get; set; } = 0;

        /// <summary>
        /// Kurulum durumu
        /// </summary>
        public string Status { get; set; } = "Bekliyor";

        /// <summary>
        /// İndirme ilerleme yüzdesi (0-100)
        /// </summary>
        public int DownloadProgress { get; set; } = 0;

        /// <summary>
        /// Kullanıcı tarafından seçilip seçilmediği
        /// </summary>
        public bool IsSelected { get; set; } = true;

        /// <summary>
        /// Sürücünün aktif olup olmadığı
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Son güncelleme tarihi
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string FileHash { get; set; } = string.Empty;

        /// <summary>
        /// ToString override - debugging için
        /// </summary>
        public override string ToString()
        {
            return $"{Name} - {Status} ({FileName})";
        }

        /// <summary>
        /// Sürücünün kuruluma hazır olup olmadığını kontrol eder
        /// </summary>
        public bool IsReadyForInstallation()
        {
            return !string.IsNullOrEmpty(Name) &&
                   !string.IsNullOrEmpty(FileName) &&
                   !string.IsNullOrEmpty(ProcessName) &&
                   IsEnabled &&
                   IsSelected;
        }

        /// <summary>
        /// Sürücünün online kaynaktan indirilebilir olup olmadığını kontrol eder
        /// </summary>
        public bool HasOnlineSource()
        {
            return !string.IsNullOrEmpty(Url);
        }

        /// <summary>
        /// Sürücünün gömülü kaynağa sahip olup olmadığını kontrol eder
        /// </summary>
        public bool HasEmbeddedResource()
        {
            return !string.IsNullOrEmpty(ResourceName);
        }
    }
}