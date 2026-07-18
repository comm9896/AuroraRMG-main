using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class EditorHelpWindow : Window
    {
        public EditorHelpWindow()
        {
            InitializeComponent();
            InitDictionary();
        }

        private void InitDictionary()
        {
            // Build category filter
            var categories = RmgFieldDictionary.Entries
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            categories.Insert(0, "Все");
            DictFilterCombo.ItemsSource = categories;
            DictFilterCombo.SelectedIndex = 0;

            DictList.ItemsSource = RmgFieldDictionary.Entries
                .Select(e => new { e.Field, e.Category, e.DescRu, e.DescEn })
                .ToList();
        }

        private void DictFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DictFilterCombo.SelectedItem is not string category) return;
            var query = RmgFieldDictionary.Entries.AsEnumerable();
            if (category != "Все")
                query = query.Where(en => en.Category == category);
            DictList.ItemsSource = query
                .Select(en => new { en.Field, en.Category, en.DescRu, en.DescEn })
                .ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
