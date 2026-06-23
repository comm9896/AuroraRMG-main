using OldenEraTemplateEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolViewerWindow : Window
    {
        private List<GamePool> _allPools = new();
        private Window? _debugWindow;

        public ContentPoolViewerWindow(List<string>? allPoolSids = null, List<string>? allSelectedPids = null)
        {
            InitializeComponent();

            // Setup category filter
            CategoryCombo.ItemsSource = new List<string>
            {
                "Все",
                "Guarded",
                "Unguarded",
                "Resources",
                "Random",
                "Default",
                "Template-specific",
                "Созданные"
            };
            CategoryCombo.SelectedIndex = 0;

            // Setup search placeholder
            SearchBox.GotFocus += (_, _) =>
            {
                if (SearchBox.Text == "Поиск по названию...")
                {
                    SearchBox.Text = "";
                    SearchBox.Foreground = System.Windows.Media.Brushes.Black;
                }
            };
            SearchBox.LostFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    SearchBox.Text = "Поиск по названию...";
                    SearchBox.Foreground = System.Windows.Media.Brushes.Gray;
                }
            };
            SearchBox.Text = "Поиск по названию...";
            SearchBox.Foreground = System.Windows.Media.Brushes.Gray;

            try
            {
                _allPools = GamePoolDataLoader.GetAllPools();

                if (_allPools.Count == 0)
                {
                    ResultCount.Text = "⚠ Пулы не загружены";
                    return;
                }

                FilterPools();
            }
            catch (Exception ex)
            {
                ResultCount.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void FilterPools()
        {
            var query = _allPools.AsEnumerable();

            // Filter by search text
            var searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Поиск по названию...")
            {
                query = query.Where(p => p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by category
            if (CategoryCombo.SelectedItem is string category && category != "Все")
            {
                query = query.Where(p => MatchesCategory(p.Name, category));
            }

            var filtered = query.ToList();
            PoolList.ItemsSource = filtered;

            ResultCount.Text = $"Найдено: {filtered.Count} из {_allPools.Count}";

            if (filtered.Count == 0)
            {
                ResultCount.Text = "Ничего не найдено";
            }
        }

        private bool MatchesCategory(string poolName, string category)
        {
            var name = poolName.ToLowerInvariant();
            return category switch
            {
                "Guarded" => name.Contains("guarded"),
                "Unguarded" => name.Contains("unguarded"),
                "Resources" => name.Contains("resources"),
                "Random" => name.Contains("random"),
                "Default" => name.Contains("default"),
                "Template-specific" => name.Contains("template_pool_") && !name.Contains("random"),
                "Созданные" => name.StartsWith("custom_"),
                _ => true
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPools();
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterPools();
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            FilterPools();
        }

        private void ClearCategoryBtn_Click(object sender, RoutedEventArgs e)
        {
            CategoryCombo.SelectedIndex = 0;
            FilterPools();
        }

        private void PoolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PoolList.SelectedItem is GamePool pool)
                ShowPool(pool);
        }

        private void ShowPool(GamePool pool)
        {
            var items = GamePoolDataLoader.GetPoolItems(pool);
            PoolDataGrid.ItemsSource = null;
            PoolDataGrid.ItemsSource = items;
        }

        private void DebugBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_debugWindow != null)
            {
                _debugWindow.Activate();
                return;
            }

            _debugWindow = new Window
            {
                Title = "Отладка пулов",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.DarkSlateGray
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var text = new TextBlock
            {
                Text = GetDebugInfo(),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            scroll.Content = text;
            _debugWindow.Content = scroll;

            _debugWindow.Closed += (_, _) => _debugWindow = null;
            _debugWindow.Show();
        }

        private string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Статус загрузки ===");
            sb.AppendLine(GamePoolDataLoader.Status);
            sb.AppendLine();
            sb.AppendLine($"Пулов в памяти: {_allPools.Count}");
            sb.AppendLine($"Фильтр поиска: '{SearchBox.Text}'");
            sb.AppendLine($"Категория: {CategoryCombo.SelectedItem}");
            sb.AppendLine();
            sb.AppendLine("=== Примеры пулов (первые 10) ===");
            foreach (var pool in _allPools.Take(10))
            {
                sb.AppendLine($"- {pool.Name} (групп: {pool.Groups?.Count ?? 0})");
            }
            return sb.ToString();
        }
    }
}
