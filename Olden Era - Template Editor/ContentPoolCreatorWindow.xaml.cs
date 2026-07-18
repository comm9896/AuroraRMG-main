using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolCreatorWindow : Window
    {
        private readonly List<ContentListInfo> _allLists = new();
        private readonly List<SelectedListEntry> _selectedEntries = new();

         public string? CreatedPoolName { get; private set; }
         public Dictionary<string, int>? CreatedPoolItems { get; private set; }

         public event Action<string, Dictionary<string, int>>? PoolCreated;

        public ContentPoolCreatorWindow()
        {
            InitializeComponent();
            LoadContentLists();
            CategoryCombo.ItemsSource = ContentListInfo.Categories;
            CategoryCombo.SelectedIndex = 0;
        }

        protected virtual void OnPoolCreated()
        {
            PoolCreated?.Invoke(CreatedPoolName!, CreatedPoolItems!);
        }

        private class SelectedListEntry
        {
            public string Name { get; set; } = "";
            public int Weight { get; set; } = 1;
        }

        private void RefreshSelectedGrid()
        {
            SelectedLists.ItemsSource = null;
            SelectedLists.ItemsSource = _selectedEntries;
        }

        private void LoadContentLists()
        {
            _allLists.Clear();
            _allLists.AddRange(ContentListInfo.GetAllLists());
            AvailableLists.ItemsSource = _allLists;
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryCombo.SelectedItem is string category)
            {
                var filtered = category == "Все"
                    ? _allLists
                    : _allLists.Where(l => l.Category == category).ToList();
                AvailableLists.ItemsSource = filtered;
            }
        }

        private void AddSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AvailableLists.SelectedItems)
            {
                if (item is ContentListInfo list)
                {
                    if (!_selectedEntries.Any(s => s.Name == list.Name))
                        _selectedEntries.Add(new SelectedListEntry { Name = list.Name, Weight = 1 });
                }
            }
            RefreshSelectedGrid();
        }

        private void AddAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var list in _allLists)
            {
                if (!_selectedEntries.Any(s => s.Name == list.Name))
                    _selectedEntries.Add(new SelectedListEntry { Name = list.Name, Weight = 1 });
            }
            RefreshSelectedGrid();
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = SelectedLists.SelectedItems.Cast<SelectedListEntry>().ToList();
            foreach (var entry in toRemove)
                _selectedEntries.Remove(entry);
            RefreshSelectedGrid();
        }

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedEntries.Clear();
            RefreshSelectedGrid();
        }

        private void CreatePool_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PoolNameBox.Text))
            {
                MessageBox.Show(this, L("S.CPC.EnterName"), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedEntries.Count == 0)
            {
                MessageBox.Show(this, "Добавьте хотя бы один список контента", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreatedPoolName = PoolNameBox.Text.Trim();
            CreatedPoolItems = _selectedEntries.ToDictionary(e => e.Name, e => e.Weight);
            OnPoolCreated();
            Close();
        }
        // ── Localisation helper ──
        private static string L(string key, params object[] args)
            => Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key, args);
    }


    public class ContentListInfo
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        public static readonly List<string> Categories = new()
        {
            "Все",
            "Предметы",
            "Ящики Пандоры",
            "Внешние жилища",
            "Банки существ",
            "Банки ресурсов",
            "Характеристики",
            "Магия",
            "Ресурсы",
            "Шахты",
            "Хранилища",
            "Здания",
            "Взаимодействие",
            "Видение",
            "Особые",
        };

        public static List<ContentListInfo> GetAllLists()
        {
            return new List<ContentListInfo>
            {
                // Предметы
                new() { Name = "basic_content_list_pickup_random_items", Category = "Предметы", Description = "Случайные предметы (common, rare, epic, legendary)" },
                new() { Name = "basic_content_list_pickup_prison", Category = "Предметы", Description = "Тюрьма (prison)" },
                new() { Name = "basic_content_list_pickup_scroll_box", Category = "Предметы", Description = "Свитки" },
                new() { Name = "basic_content_list_pickup_enchanted_scroll_box", Category = "Предметы", Description = "Зачарованные свитки" },
                new() { Name = "basic_content_list_pickup_mythic_scroll_box", Category = "Предметы", Description = "Мифические свитки" },

                // Ящики Пандоры
                new() { Name = "basic_content_list_pickup_pandora_box", Category = "Ящики Пандоры", Description = "Ящик Пандоры" },
                new() { Name = "content_list_pickup_pandora_box_gold", Category = "Ящики Пандоры", Description = "Ящик Пандоры (золото)" },
                new() { Name = "content_list_pickup_pandora_box_exp", Category = "Ящики Пандоры", Description = "Ящик Пандоры (опыт)" },
                new() { Name = "content_list_pickup_pandora_box_army", Category = "Ящики Пандоры", Description = "Ящик Пандоры (армия)" },

                // Внешние жилища
                new() { Name = "basic_content_list_building_random_hires", Category = "Внешние жилища", Description = "Случайные внешние жилища (hire 1-7)" },
                new() { Name = "content_list_building_random_hires_low_tier", Category = "Внешние жилища", Description = "Внешние жилища низкого тира (hire 1-4)" },
                new() { Name = "content_list_building_random_hires_high_tier", Category = "Внешние жилища", Description = "Внешние жилища высокого тира (hire 5-7)" },

                // Банки существ
                new() { Name = "basic_content_list_building_guarded_units_banks", Category = "Банки существ", Description = "Банки существ (с биомами)" },
                new() { Name = "basic_content_list_building_guarded_units_banks_no_biome_restriction", Category = "Банки существ", Description = "Банки существ (без биомов)" },
                new() { Name = "content_list_building_uncommon_guarded_units_banks", Category = "Банки существ", Description = "Редкие банки существ" },

                // Банки ресурсов
                new() { Name = "basic_content_list_building_guarded_resource_banks_tier_1", Category = "Банки ресурсов", Description = "Банки ресурсов T1" },
                new() { Name = "basic_content_list_building_guarded_resource_banks_tier_2", Category = "Банки ресурсов", Description = "Банки ресурсов T2 (с биомами)" },
                new() { Name = "basic_content_list_building_guarded_resource_banks_tier_3", Category = "Банки ресурсов", Description = "Банки ресурсов T3 (эпические)" },
                new() { Name = "content_list_building_epic_guarded_resource_banks", Category = "Банки ресурсов", Description = "Эпические банки ресурсов" },
                new() { Name = "content_list_building_common_resource_banks", Category = "Банки ресурсов", Description = "Обычные банки ресурсов" },
                new() { Name = "content_list_building_uncommon_resource_banks", Category = "Банки ресурсов", Description = "Редкие банки ресурсов" },

                // Характеристики
                new() { Name = "basic_content_list_building_hero_stats_and_skills_tier_1", Category = "Характеристики", Description = "Характеристики T1" },
                new() { Name = "basic_content_list_building_hero_stats_and_skills_tier_2", Category = "Характеристики", Description = "Характеристики T2" },
                new() { Name = "basic_content_list_building_hero_stats_and_skills_tier_3", Category = "Характеристики", Description = "Характеристики T3" },
                new() { Name = "basic_content_list_building_hero_exp_tier_1", Category = "Характеристики", Description = "Опыт T1" },
                new() { Name = "basic_content_list_building_hero_exp_tier_2", Category = "Характеристики", Description = "Опыт T2" },
                new() { Name = "basic_content_list_building_hero_buff_tier_1", Category = "Характеристики", Description = "Баффы T1" },

                // Магия
                new() { Name = "basic_content_list_building_magic_tier_1", Category = "Магия", Description = "Магия T1" },
                new() { Name = "basic_content_list_building_magic_tier_2", Category = "Магия", Description = "Магия T2" },

                // Ресурсы
                new() { Name = "basic_content_list_basic_resources", Category = "Ресурсы", Description = "Основные ресурсы (золото, дерево, руда)" },
                new() { Name = "basic_content_list_rare_resources", Category = "Ресурсы", Description = "Редкие ресурсы (кристаллы, ртуте, самоцветы)" },
                new() { Name = "basic_content_list_special_resources", Category = "Ресурсы", Description = "Особые ресурсы (пыла, сундуки, костры)" },

                // Шахты
                new() { Name = "basic_content_list_basic_mines", Category = "Шахты", Description = "Основные шахты (золото, дерево, руда)" },
                new() { Name = "basic_content_list_rare_mines", Category = "Шахты", Description = "Редкие шахты" },
                new() { Name = "basic_content_list_special_mines", Category = "Шахты", Description = "Особые шахты (алхимическая лаборатория)" },

                // Хранилища
                new() { Name = "basic_content_list_basic_storage", Category = "Хранилища", Description = "Хранилища ресурсов" },

                // Здания
                new() { Name = "basic_content_list_basic_buildings", Category = "Здания", Description = "Основные здания (рынок, кузня, таверна)" },
                new() { Name = "basic_content_list_non_content", Category = "Здания", Description = "Неконтентные здания" },

                // Взаимодействие
                new() { Name = "basic_content_list_building_common_interact", Category = "Взаимодействие", Description = "Обычные здания взаимодействия" },
                new() { Name = "basic_content_list_building_uncommon_interact", Category = "Взаимодействие", Description = "Редкие здания взаимодействия" },
                new() { Name = "basic_content_list_building_epic_interact", Category = "Взаимодействие", Description = "Эпические здания взаимодействия" },

                // Видение
                new() { Name = "basic_content_list_vision_buildings_tier_1", Category = "Видение", Description = "Здания видения T1" },
                new() { Name = "basic_content_list_vision_buildings_tier_2", Category = "Видение", Description = "Здания видения T2" },

                // Особые
                new() { Name = "content_list_building_utopia", Category = "Особые", Description = "Утопии (драконья утопия)" },
                new() { Name = "content_list_building_special", Category = "Особые", Description = "Особые объекты (мираж, вечный дракон)" },
                new() { Name = "content_list_town_gates", Category = "Особые", Description = "Городские ворота" },
            };
        }

        // ── Localisation helper ──
        private static string L(string key, params object[] args)
            => Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key, args);
    }
}
