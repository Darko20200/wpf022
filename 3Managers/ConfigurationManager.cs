using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace YafesV2.Managers
{
    /// <summary>
    /// Uygulama ayarlarını yöneten manager sınıfı
    /// JSON tabanlı yapılandırma yönetimi
    /// </summary>
    public class ConfigurationManager : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Yapılandırma dosya yolu
        /// </summary>
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// Mevcut yapılandırma
        /// </summary>
        public YafesConfiguration CurrentConfiguration { get; private set; }

        /// <summary>
        /// Varsayılan yapılandırma
        /// </summary>
        public YafesConfiguration DefaultConfiguration { get; private set; }

        /// <summary>
        /// Yapılandırma değişti mi
        /// </summary>
        private bool _hasChanges;
        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                _hasChanges = value;
                OnPropertyChanged(nameof(HasChanges));
            }
        }

        #endregion

        #region Constructor

        public ConfigurationManager()
        {
            InitializeConfigPath();
            InitializeDefaultConfiguration();
            LoadConfiguration();
        }

        public ConfigurationManager(string customConfigPath)
        {
            ConfigFilePath = customConfigPath;
            InitializeDefaultConfiguration();
            LoadConfiguration();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Yapılandırmayı yükler
        /// </summary>
        public bool LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    CurrentConfiguration = JsonSerializer.Deserialize<YafesConfiguration>(json) ?? DefaultConfiguration.Clone();
                }
                else
                {
                    CurrentConfiguration = DefaultConfiguration.Clone();
                    SaveConfiguration(); // İlk kez varsayılanları kaydet
                }

                HasChanges = false;
                OnConfigurationChanged("Configuration loaded");
                return true;
            }
            catch (Exception ex)
            {
                // Hata durumunda varsayılan ayarları kullan
                CurrentConfiguration = DefaultConfiguration.Clone();
                HasChanges = false;

                // Log the error
                System.Diagnostics.Debug.WriteLine($"Config load error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Yapılandırmayı kaydeder
        /// </summary>
        public bool SaveConfiguration()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(CurrentConfiguration, options);

                // Config dizinini oluştur
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));

                File.WriteAllText(ConfigFilePath, json);
                HasChanges = false;
                OnConfigurationChanged("Configuration saved");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config save error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Yapılandırmayı varsayılanlara sıfırlar
        /// </summary>
        public void ResetToDefaults()
        {
            CurrentConfiguration = DefaultConfiguration.Clone();
            HasChanges = true;
            OnConfigurationChanged("Configuration reset to defaults");
        }

        /// <summary>
        /// Belirli bir ayarı alır
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            try
            {
                var value = GetSettingValue(key);
                if (value != null)
                {
                    return JsonSerializer.Deserialize<T>(value.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get setting error for {key}: {ex.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// Belirli bir ayarı ayarlar
        /// </summary>
        public void SetSetting<T>(string key, T value)
        {
            try
            {
                SetSettingValue(key, value);
                HasChanges = true;
                OnConfigurationChanged($"Setting changed: {key}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Set setting error for {key}: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Config dosya yolunu başlatır
        /// </summary>
        private void InitializeConfigPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var yafesPath = Path.Combine(appDataPath, "YafesV2");
            ConfigFilePath = Path.Combine(yafesPath, "config.json");
        }

        /// <summary>
        /// Varsayılan yapılandırmayı başlatır
        /// </summary>
        private void InitializeDefaultConfiguration()
        {
            DefaultConfiguration = new YafesConfiguration
            {
                // Genel ayarlar
                Language = "tr-TR",
                Theme = "Dark",
                AutoSave = true,
                CheckForUpdates = true,

                // Kurulum ayarları
                MaxConcurrentInstallations = 3,
                MaxRetryCount = 2,
                CreateRestorePoint = true,
                DownloadTimeout = 300, // 5 dakika

                // UI ayarları
                StartMinimized = false,
                MinimizeToTray = true,
                ShowNotifications = true,
                WindowWidth = 800,
                WindowHeight = 600,

                // Gelişmiş ayarlar
                CustomSettings = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Ayar değerini alır
        /// </summary>
        private object GetSettingValue(string key)
        {
            var parts = key.Split('.');
            object current = CurrentConfiguration;

            foreach (var part in parts)
            {
                var property = current.GetType().GetProperty(part);
                if (property == null)
                {
                    if (current is YafesConfiguration config && part == "CustomSettings")
                    {
                        return config.CustomSettings;
                    }
                    else if (current is Dictionary<string, object> dict)
                    {
                        return dict.ContainsKey(part) ? dict[part] : null;
                    }
                    return null;
                }

                current = property.GetValue(current);
                if (current == null) return null;
            }

            return current;
        }

        /// <summary>
        /// Ayar değerini ayarlar
        /// </summary>
        private void SetSettingValue(string key, object value)
        {
            var parts = key.Split('.');
            object current = CurrentConfiguration;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var property = current.GetType().GetProperty(parts[i]);
                if (property == null)
                {
                    if (current is YafesConfiguration config)
                    {
                        if (!config.CustomSettings.ContainsKey(parts[i]))
                        {
                            config.CustomSettings[parts[i]] = new Dictionary<string, object>();
                        }
                        current = config.CustomSettings[parts[i]];
                    }
                    return;
                }

                current = property.GetValue(current);
                if (current == null) return;
            }

            var finalProperty = current.GetType().GetProperty(parts[parts.Length - 1]);
            if (finalProperty != null && finalProperty.CanWrite)
            {
                finalProperty.SetValue(current, value);
            }
            else if (current is Dictionary<string, object> dict)
            {
                dict[parts[parts.Length - 1]] = value;
            }
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnConfigurationChanged(string changeDescription)
        {
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(changeDescription));
        }

        #endregion
    }

    #region Configuration Model

    /// <summary>
    /// YafesV2 yapılandırma modeli
    /// </summary>
    public class YafesConfiguration
    {
        // Genel ayarlar
        public string Language { get; set; } = "tr-TR";
        public string Theme { get; set; } = "Dark";
        public bool AutoSave { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;

        // Kurulum ayarları
        public int MaxConcurrentInstallations { get; set; } = 3;
        public int MaxRetryCount { get; set; } = 2;
        public bool CreateRestorePoint { get; set; } = true;
        public int DownloadTimeout { get; set; } = 300;

        // UI ayarları
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 600;

        // Özel ayarlar
        public Dictionary<string, object> CustomSettings { get; set; } = new();

        /// <summary>
        /// Yapılandırmayı klonlar
        /// </summary>
        public YafesConfiguration Clone()
        {
            var json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<YafesConfiguration>(json);
        }
    }

    #endregion

    #region Event Args

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ChangeDescription { get; }
        public DateTime Timestamp { get; }

        public ConfigurationChangedEventArgs(string changeDescription)
        {
            ChangeDescription = changeDescription;
            Timestamp = DateTime.Now;
        }
    }

    #endregion
}