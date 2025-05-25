using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using YafesV2.Models;

namespace YafesV2.Managers
{
    /// <summary>
    /// Program kategorilerini yöneten manager sınıfı
    /// Kategori bazlı filtreleme ve gruplandırma
    /// </summary>
    public class CategoryManager : INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<CategoryEventArgs> CategoryAdded;
        public event EventHandler<CategoryEventArgs> CategoryRemoved;
        public event EventHandler<CategoryEventArgs> CategoryUpdated;

        #endregion

        #region Properties

        /// <summary>
        /// Tüm kategoriler
        /// </summary>
        public ObservableCollection<ProgramCategory> Categories { get; private set; }

        /// <summary>
        /// Seçili kategori
        /// </summary>
        private ProgramCategory _selectedCategory;
        public ProgramCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged(nameof(SelectedCategory));
                OnPropertyChanged(nameof(FilteredPrograms));
            }
        }

        /// <summary>
        /// Mevcut programlar (dışarıdan set edilecek)
        /// </summary>
        private ObservableCollection<ProgramInfo> _allPrograms;
        public ObservableCollection<ProgramInfo> AllPrograms
        {
            get => _allPrograms;
            set
            {
                _allPrograms = value;
                OnPropertyChanged(nameof(AllPrograms));
                OnPropertyChanged(nameof(FilteredPrograms));
                UpdateCategoryCounts();
            }
        }

        /// <summary>
        /// Filtrelenmiş programlar (seçili kategoriye göre)
        /// </summary>
        public IEnumerable<ProgramInfo> FilteredPrograms
        {
            get
            {
                if (AllPrograms == null) return new List<ProgramInfo>();

                if (SelectedCategory == null || SelectedCategory.Name == "Tümü")
                {
                    return AllPrograms;
                }

                return AllPrograms.Where(p =>
                    string.Equals(p.Category, SelectedCategory.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Arama filtresi
        /// </summary>
        private string _searchFilter;
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                _searchFilter = value;
                OnPropertyChanged(nameof(SearchFilter));
                OnPropertyChanged(nameof(FilteredPrograms));
            }
        }

        #endregion

        #region Constructor

        public CategoryManager()
        {
            Categories = new ObservableCollection<ProgramCategory>();
            _allPrograms = new ObservableCollection<ProgramInfo>();
            InitializeDefaultCategories();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Yeni kategori ekler
        /// </summary>
        public void AddCategory(string name, string description = "", string icon = "📁")
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;

            var category = new ProgramCategory
            {
                Name = name,
                Description = description,
                Icon = icon,
                Count = 0,
                IsVisible = true
            };

            Categories.Add(category);
            OnCategoryAdded(category);
            UpdateCategoryCounts();
        }

        /// <summary>
        /// Kategori siler
        /// </summary>
        public bool RemoveCategory(string name)
        {
            var category = Categories.FirstOrDefault(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (category != null && category.Name != "Tümü")
            {
                Categories.Remove(category);
                OnCategoryRemoved(category);

                // Seçili kategori silinirse "Tümü" seç
                if (SelectedCategory == category)
                {
                    SelectedCategory = Categories.FirstOrDefault();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Kategori günceller
        /// </summary>
        public bool UpdateCategory(string name, string newDescription = null, string newIcon = null)
        {
            var category = Categories.FirstOrDefault(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (category != null)
            {
                if (!string.IsNullOrEmpty(newDescription))
                    category.Description = newDescription;

                if (!string.IsNullOrEmpty(newIcon))
                    category.Icon = newIcon;

                OnCategoryUpdated(category);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Kategori sayılarını günceller
        /// </summary>
        public void UpdateCategoryCounts()
        {
            if (AllPrograms == null) return;

            foreach (var category in Categories)
            {
                if (category.Name == "Tümü")
                {
                    category.Count = AllPrograms.Count;
                }
                else
                {
                    category.Count = AllPrograms.Count(p =>
                        string.Equals(p.Category, category.Name, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        /// <summary>
        /// Kategori seçer
        /// </summary>
        public void SelectCategory(string name)
        {
            var category = Categories.FirstOrDefault(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (category != null)
            {
                SelectedCategory = category;
            }
        }

        /// <summary>
        /// Kategoriye göre programları alır
        /// </summary>
        public IEnumerable<ProgramInfo> GetProgramsByCategory(string categoryName)
        {
            if (AllPrograms == null) return new List<ProgramInfo>();

            if (string.IsNullOrEmpty(categoryName) || categoryName == "Tümü")
            {
                return AllPrograms;
            }

            return AllPrograms.Where(p =>
                string.Equals(p.Category, categoryName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Arama ve kategori filtresi uygular
        /// </summary>
        public IEnumerable<ProgramInfo> GetFilteredPrograms()
        {
            var programs = FilteredPrograms;

            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                programs = programs.Where(p =>
                    p.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase));
            }

            return programs;
        }

        /// <summary>
        /// Kategoriye göre program sayısı
        /// </summary>
        public int GetCategoryCount(string categoryName)
        {
            var category = Categories.FirstOrDefault(c =>
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            return category?.Count ?? 0;
        }

        /// <summary>
        /// Boş kategorileri gizler
        /// </summary>
        public void HideEmptyCategories()
        {
            foreach (var category in Categories)
            {
                category.IsVisible = category.Count > 0 || category.Name == "Tümü";
            }
        }

        /// <summary>
        /// Tüm kategorileri gösterir
        /// </summary>
        public void ShowAllCategories()
        {
            foreach (var category in Categories)
            {
                category.IsVisible = true;
            }
        }

        /// <summary>
        /// Programdan otomatik kategori çıkarır
        /// </summary>
        public string GetAutomaticCategory(ProgramInfo program)
        {
            if (program == null || string.IsNullOrEmpty(program.Name))
                return "Diğer";

            var name = program.Name.ToLower();

            // Browser'lar
            if (name.Contains("chrome") || name.Contains("firefox") ||
                name.Contains("opera") || name.Contains("edge") || name.Contains("safari"))
                return "Browser";

            // Media
            if (name.Contains("vlc") || name.Contains("media") || name.Contains("player") ||
                name.Contains("spotify") || name.Contains("itunes"))
                return "Media";

            // Development
            if (name.Contains("visual studio") || name.Contains("code") ||
                name.Contains("git") || name.Contains("notepad++"))
                return "Development";

            // Gaming
            if (name.Contains("steam") || name.Contains("origin") ||
                name.Contains("epic") || name.Contains("game"))
                return "Gaming";

            // Utility
            if (name.Contains("winrar") || name.Contains("7zip") ||
                name.Contains("uninstaller") || name.Contains("cleaner"))
                return "Utility";

            // System
            if (name.Contains("driver") || name.Contains("antivirus") ||
                name.Contains("windows") || name.Contains("system"))
                return "System";

            // Communication
            if (name.Contains("discord") || name.Contains("skype") ||
                name.Contains("teams") || name.Contains("zoom"))
                return "Communication";

            return "Diğer";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Varsayılan kategorileri başlatır
        /// </summary>
        private void InitializeDefaultCategories()
        {
            var defaultCategories = new List<ProgramCategory>
            {
                new ProgramCategory { Name = "Tümü", Description = "Tüm programlar", Icon = "📋" },
                new ProgramCategory { Name = "Browser", Description = "Web tarayıcıları", Icon = "🌐" },
                new ProgramCategory { Name = "Media", Description = "Medya oynatıcıları", Icon = "🎵" },
                new ProgramCategory { Name = "Development", Description = "Geliştirme araçları", Icon = "💻" },
                new ProgramCategory { Name = "Gaming", Description = "Oyun platformları", Icon = "🎮" },
                new ProgramCategory { Name = "Utility", Description = "Sistem araçları", Icon = "🔧" },
                new ProgramCategory { Name = "System", Description = "Sistem programları", Icon = "⚙️" },
                new ProgramCategory { Name = "Communication", Description = "İletişim araçları", Icon = "💬" },
                new ProgramCategory { Name = "Diğer", Description = "Diğer programlar", Icon = "📁" }
            };

            foreach (var category in defaultCategories)
            {
                Categories.Add(category);
            }

            // İlk kategoriyi seç (Tümü)
            SelectedCategory = Categories.FirstOrDefault();
        }

        #endregion

        #region Event Helpers

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnCategoryAdded(ProgramCategory category)
        {
            CategoryAdded?.Invoke(this, new CategoryEventArgs(category, CategoryAction.Added));
        }

        protected virtual void OnCategoryRemoved(ProgramCategory category)
        {
            CategoryRemoved?.Invoke(this, new CategoryEventArgs(category, CategoryAction.Removed));
        }

        protected virtual void OnCategoryUpdated(ProgramCategory category)
        {
            CategoryUpdated?.Invoke(this, new CategoryEventArgs(category, CategoryAction.Updated));
        }

        #endregion
    }

    #region Models

    /// <summary>
    /// Program kategorisi modeli
    /// </summary>
    public class ProgramCategory : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        private string _icon;
        public string Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                OnPropertyChanged(nameof(Count));
            }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Kategori event args
    /// </summary>
    public class CategoryEventArgs : EventArgs
    {
        public ProgramCategory Category { get; }
        public CategoryAction Action { get; }

        public CategoryEventArgs(ProgramCategory category, CategoryAction action)
        {
            Category = category;
            Action = action;
        }
    }

    /// <summary>
    /// Kategori aksiyonları
    /// </summary>
    public enum CategoryAction
    {
        Added,
        Removed,
        Updated
    }

    #endregion
}