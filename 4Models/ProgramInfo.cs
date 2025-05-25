// Models/ProgramInfo.cs
using System;
using System.Collections.Generic;
namespace YafesV2.Models
{
    /// <summary>
    /// Program bilgilerini temsil eden model sınıfı
    /// Eski Main.xaml.cs'teki ProgramInfo class'ından alınmıştır
    /// </summary>
    public class ProgramInfo
    {

        /// <summary>
        /// İndirme URL'i (Url ile aynı ama backward compatibility için)
        /// </summary>
        public string DownloadUrl
        {
            get => Url;
            set => Url = value;
        }

        /// <summary>
        /// Kurulum önceliği (düşük numara = yüksek öncelik)
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Varsayılan olarak seçili mi
        /// </summary>
        public bool IsSelectedByDefault { get; set; } = true;

        /// <summary>
        /// Program aktif mi (IsEnabled ile aynı ama backward compatibility için)
        /// </summary>
        public bool IsActive
        {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        /// <summary>
        /// Gerekli disk alanı (byte cinsinden)
        /// </summary>
        public long RequiredDiskSpace { get; set; } = 0;

        /// <summary>
        /// Dosya hash'i (integrity kontrolü için)
        /// </summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>
        /// Kurulum yolları listesi
        /// </summary>
        public List<string> InstallPaths { get; set; } = new List<string>();
        /// <summary>
        /// Programın görünen adı
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
        /// Özel kurulum yöntemi gerekip gerekmediği
        /// (WinRAR, Opera, Driver Booster gibi programlar için)
        /// </summary>
        public bool SpecialInstallation { get; set; } = false;

        /// <summary>
        /// Programın kategorisi (örn: "Communication", "Gaming", "Utilities")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Temel program olup olmadığı
        /// </summary>
        public bool IsEssential { get; set; } = false;

        /// <summary>
        /// Öne çıkan program olup olmadığı
        /// </summary>
        public bool IsFeatured { get; set; } = false;

        /// <summary>
        /// Program açıklaması
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Program versiyonu
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Geliştirici/Şirket adı
        /// </summary>
        public string Publisher { get; set; } = string.Empty;

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
        /// Programın aktif olup olmadığı
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Programın ücretsiz olup olmadığı
        /// </summary>
        public bool IsFree { get; set; } = true;

        /// <summary>
        /// Minimum sistem gereksinimleri
        /// </summary>
        public string SystemRequirements { get; set; } = string.Empty;

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Son güncelleme tarihi
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// ToString override - debugging için
        /// </summary>
        public override string ToString()
        {
            return $"{Name} - {Status} ({FileName})";
        }

        /// <summary>
        /// Programın kuruluma hazır olup olmadığını kontrol eder
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
        /// Programın online kaynaktan indirilebilir olup olmadığını kontrol eder
        /// </summary>
        public bool HasOnlineSource()
        {
            return !string.IsNullOrEmpty(Url);
        }

        /// <summary>
        /// Programın gömülü kaynağa sahip olup olmadığını kontrol eder
        /// </summary>
        public bool HasEmbeddedResource()
        {
            return !string.IsNullOrEmpty(ResourceName);
        }

        /// <summary>
        /// Programın özel kurulum mantığı gerektirip gerektirmediğini kontrol eder
        /// </summary>
        public bool RequiresSpecialInstallation()
        {
            return SpecialInstallation ||
                   Name.Contains("WinRAR") ||
                   Name.Contains("Opera") ||
                   Name.Contains("Driver Booster") ||
                   Name.Contains("Revo Uninstaller");
        }

        /// <summary>
        /// Programın kategori rengini döndürür (UI için)
        /// </summary>
        public string GetCategoryColor()
        {
            return Category.ToLower() switch
            {
                "communication" => "#FF4CAF50",
                "gaming" => "#FFFF5722",
                "utilities" => "#FF607D8B",
                "browsers" => "#FF2196F3",
                "development" => "#FF795548",
                "system" => "#FF9E9E9E",
                _ => "#FFCCCCCC"
            };
        }
    }
}