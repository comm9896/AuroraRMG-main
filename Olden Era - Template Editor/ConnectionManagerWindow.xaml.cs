using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class ConnectionManagerWindow : Window
    {
        private readonly RmgTemplate _template;
        private readonly TemplateEditorWindow _editor;
        private readonly List<ConnectionInfo> _connectionsInfo = new();

        public ConnectionManagerWindow(RmgTemplate template, TemplateEditorWindow editor)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации ConnectionManagerWindow:\n\n{ex}");
                throw;
            }
            _template = template;
            _editor = editor;
            Owner = editor;
            LoadConnectionsData();
        }

        private void LoadConnectionsData()
        {
            _connectionsInfo.Clear();
            int num = 1;

            foreach (var variant in _template.Variants ?? new List<Variant>())
            {
                foreach (var connection in variant.Connections ?? new List<Connection>())
                {
                    _connectionsInfo.Add(new ConnectionInfo
                    {
                        Number = num++,
                        Connection = connection,
                        FromZoneName = connection.From,
                        ToZoneName = connection.To,
                        DisplayText = connection.Name ?? $"({connection.From} → {connection.To})",
                        IsModified = false
                    });
                }
            }

            ConnectionsList.ItemsSource = _connectionsInfo;
            ConnectionsList.Items.Refresh();
            ConnectionCountText.Text = _connectionsInfo.Count > 0
                ? $"Всего связей: {_connectionsInfo.Count}"
                : "Связи не найдены";
        }

        private void ConnectionsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ConnectionsList.SelectedItem is ConnectionInfo info && info.Connection != null)
            {
                var dialog = new ConnectionSettingsWindow(info.Connection, _template);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    info.DisplayText = info.Connection.Name ?? $"({info.Connection.From} → {info.Connection.To})";
                    info.IsModified = true;
                    ConnectionsList.Items.Refresh();
                    UpdateEditor();
                }
            }
        }

        private void RemoveConnection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ConnectionInfo info)
            {
                var result = MessageBox.Show(this,
                    $"Удалить связь «{info.DisplayText}»?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                var variant = _template.Variants?.FirstOrDefault(v => v.Connections != null);
                if (variant?.Connections != null)
                {
                    var target = variant.Connections
                        .FirstOrDefault(c => c.Name == info.Connection?.Name
                                          && c.From == info.Connection?.From
                                          && c.To == info.Connection?.To);
                    if (target != null)
                    {
                        _editor.RemoveRoadReferences(target.Name ?? $"{target.From}-{target.To}");
                        variant.Connections.Remove(target);
                    }
                }

                _connectionsInfo.Remove(info);
                Renumber();
                ConnectionsList.Items.Refresh();
                ConnectionCountText.Text = _connectionsInfo.Count > 0
                    ? $"Всего связей: {_connectionsInfo.Count}"
                    : "Связи не найдены";
                UpdateEditor();
            }
        }

        private void Renumber()
        {
            int num = 1;
            foreach (var info in _connectionsInfo)
                info.Number = num++;
        }

        private void UpdateEditor()
        {
            _editor?.RebuildGraph();
            _editor?.BuildInspector();
            _editor?.MarkDirty();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectionsList.Focus();
        }
    }
}
