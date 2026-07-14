using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Models.Generated;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolViewerWindow : Window
    {
        private List<GamePool> _allPools = new();
        private Dictionary<GamePool, List<PoolItemInfo>> _poolItems = new();
        private Window? _debugWindow;

        // Selection mode state
        private bool _selectionMode;
        private readonly List<string> _selected = new();
        public List<string> SelectedPools { get; private set; } = new();

        public ContentPoolViewerWindow(List<GamePool>? allPools = null, List<string>? selected = null,
            bool selectionMode = false, List<string>? allowedCategories = null)
        {
            InitializeComponent();

            _selectionMode = selectionMode;
            SelectionPanel.Visibility = selectionMode ? Visibility.Visible : Visibility.Collapsed;

            // Build category filter
            var available = new List<string>
            {
                "Guarded", "Unguarded", "Resources", "Random", "Default",
                "Template-specific", "Созданные", "обязательный контент", "лимиты количества контента"
            };
            var cats = new List<string> { "Все" };
            if (allowedCategories != null && allowedCategories.Count > 0)
                cats.AddRange(available.Where(allowedCategories.Contains));
            else
                cats.AddRange(available);
            CategoryCombo.ItemsSource = cats;
            CategoryCombo.SelectedIndex = 0;

            // Search placeholder
            SearchBox.GotFocus += (_, _) =>
            {
                if (SearchBox.Text == L("S.CPV.Search"))
                {
                    SearchBox.Text = "";
                    SearchBox.Foreground = Brushes.Black;
                }
            };
            SearchBox.LostFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    SearchBox.Text = "Поиск по названию...";
                    SearchBox.Foreground = Brushes.Gray;
                }
            };
            SearchBox.Text = "Поиск по названию...";
            SearchBox.Foreground = Brushes.Gray;

            try
            {
                if (allPools != null)
                {
                    _allPools = allPools;
                    _poolItems = new Dictionary<GamePool, List<PoolItemInfo>>();
                }
                else
                {
                    // Full universe: game pools + every template's mandatory_content / content_limits pools.
                    _allPools = GamePoolDataLoader.GetAllPoolsWithTemplates(out _poolItems);
                }

                if (_allPools.Count == 0)
                {
                    ResultCount.Text = L("S.CPV.NoPools");
                    return;
                }

                // Restrict the universe to the allowed categories (picker selection mode).
                if (allowedCategories != null && allowedCategories.Count > 0)
                    _allPools = _allPools
                        .Where(p => allowedCategories.Any(c => MatchesCategory(p.Name, c)))
                        .ToList();

                if (_selectionMode && selected != null)
                    _selected.AddRange(selected);
                RefreshSelectionUI();

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

            var searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Поиск по названию...")
                query = query.Where(p => p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            if (CategoryCombo.SelectedItem is string category && category != "Все")
                query = query.Where(p => MatchesCategory(p.Name, category));

            var filtered = query.ToList();
            PoolList.ItemsSource = filtered;
            ResultCount.Text = $"Найдено: {filtered.Count} из {_allPools.Count}";
            if (filtered.Count == 0)
                ResultCount.Text = "Ничего не найдено";
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
                "обязательный контент" => name.Contains("mandatory") || GetTag(poolName) == "mandatory",
                "лимиты количества контента" => name.Contains("content_limits") || GetTag(poolName) == "content_limits",
                _ => true
            };
        }

        private string? GetTag(string poolName)
        {
            var pool = _allPools.FirstOrDefault(p => p.Name == poolName);
            return pool?.Tag;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterPools();
        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => FilterPools();

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

        private void PoolList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PoolList.SelectedItem is GamePool pool && _selectionMode)
                AddToSelection(pool.Name);
        }

        private void ShowPool(GamePool pool)
        {
            List<PoolItemInfo> items;
            if (_poolItems.TryGetValue(pool, out var tItems))
                items = tItems;
            else
                items = GamePoolDataLoader.GetPoolItems(pool);

            // Build display rows. For mandatory-content / content-limit pools the "вариант" column
            // shows the item's variant field: empty when absent (null) and "-1" when -1; the extra
            // column shows the max-count value for limits (empty when absent/0). For every other
            // pool the raw weight is shown in the variant column. The detail column header is pool-type specific.
            bool isMandatory = pool.Tag == "mandatory";
            bool isLimits = pool.Tag == "content_limits";
            string VariantText(int? v) => !v.HasValue ? "" : (v == -1 ? "-1" : v.ToString()!);
            var display = new List<PoolRow>(items.Count);
            foreach (var r in items)
            {
                string variant;
                string extra;
                if (isMandatory || isLimits)
                {
                    variant = VariantText(r.Variant);
                    extra = isLimits
                        ? (r.MaxCount == 0 ? "" : r.MaxCount.ToString())
                        : (r.Detail ?? "");
                }
                else
                {
                    if (r.Weight == 0) variant = "";
                    else if (r.Weight == -1) variant = "-1";
                    else variant = r.Weight.ToString();
                    extra = r.Detail ?? "";
                }
                display.Add(new PoolRow
                {
                    ListName = r.ListName,
                    Sid = r.Sid ?? "",
                    Variant = variant,
                    Biome = r.Biome ?? "",
                    Extra = extra,
                });
            }

            PoolDataGrid.ItemsSource = null;
            PoolDataGrid.ItemsSource = display;

            if (PoolDataGrid.Columns.Count > 4)
                PoolDataGrid.Columns[4].Header = isMandatory ? "Дополнительно"
                    : isLimits ? "максимальное количество"
                    : "настройки";

            SourceText.Text = pool.SourceTemplate != null
                ? $"Источник: шаблон игры «{pool.SourceTemplate}»"
                : "Источник: GameData / созданный пул";
        }

        /// <summary>Display-only projection of <see cref="PoolItemInfo"/> with pre-formatted strings
        /// (variant rule for mandatory-content, etc.) so the DataGrid can bind to plain text.</summary>
        private sealed class PoolRow
        {
            public string ListName { get; set; } = "";
            public string Sid { get; set; } = "";
            public string Variant { get; set; } = "";
            public string Biome { get; set; } = "";
            public string Extra { get; set; } = "";
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
                Background = Brushes.DarkSlateGray
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var text = new TextBlock
            {
                Text = GetDebugInfo(),
                Foreground = Brushes.White,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8),
                FontFamily = new FontFamily("Consolas")
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
                var src = pool.SourceTemplate != null ? $" [{pool.SourceTemplate}]" : "";
                sb.AppendLine($"- {pool.Name}{src} (групп: {pool.Groups?.Count ?? 0})");
            }
            return sb.ToString();
        }

        // ── Selection mode ────────────────────────────────────────────────────────
        private void BtnAddSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PoolList.SelectedItem is GamePool pool)
                AddToSelection(pool.Name);
        }

        private void AddToSelection(string poolName)
        {
            if (!_selected.Contains(poolName))
            {
                _selected.Add(poolName);
                RefreshSelectionUI();
            }
        }

        private void RefreshSelectionUI()
        {
            SelectionChips.Children.Clear();
            foreach (var name in _selected)
            {
                var chip = new System.Windows.Controls.Border
                {
                    Background = (Brush)FindResource("BrushInput"),
                    BorderBrush = (Brush)FindResource("BrushBorder"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 2, 2),
                    Margin = new Thickness(0, 0, 4, 4),
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var txt = new TextBlock { Text = name, FontSize = 10, Foreground = (Brush)FindResource("BrushText"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
                Grid.SetColumn(txt, 0);
                var btn = new Button
                {
                    Content = "✕",
                    FontSize = 9,
                    Padding = new Thickness(4, 0, 4, 0),
                    MinWidth = 18,
                    MinHeight = 18,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = (Brush)FindResource("BrushTextDim"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                var captured = name;
                btn.Click += (_, _) =>
                {
                    _selected.Remove(captured);
                    RefreshSelectionUI();
                };
                Grid.SetColumn(btn, 1);
                grid.Children.Add(txt);
                grid.Children.Add(btn);
                chip.Child = grid;
                SelectionChips.Children.Add(chip);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedPools = _selected.ToList();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Localisation helper ──
        private static string L(string key, params object[] args)
            => string.Format(Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T(key), args);
    }
}
