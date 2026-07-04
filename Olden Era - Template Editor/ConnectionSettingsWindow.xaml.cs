using System;
using System.Linq;
using System.Windows;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class ConnectionSettingsWindow : Window
    {
        private readonly Connection _connection;
        private readonly RmgTemplate _template;

        public ConnectionSettingsWindow(Connection connection, RmgTemplate template)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации ConnectionSettingsWindow:\n\n{ex}");
                throw;
            }
            _connection = connection;
            _template = template;

            LoadConnectionData();
        }

        private void LoadConnectionData()
        {
            TxtName.Text = _connection.Name ?? "";
            TxtFrom.Text = _connection.From ?? "?";
            TxtTo.Text = _connection.To ?? "?";
            TxtGuardValue.Text = (_connection.GuardValue ?? 0).ToString();
            ChkRoad.IsChecked = _connection.Road == true;

            CmbType.Items.Add("Default");
            foreach (var t in KnownValues.ConnectionTypes)
                if (!CmbType.Items.Contains(t))
                    CmbType.Items.Add(t);

            var currentType = _connection.ConnectionType ?? "Default";
            if (CmbType.Items.Contains(currentType))
                CmbType.SelectedItem = currentType;
            else
                CmbType.SelectedIndex = 0;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _connection.Name = TxtName.Text.Trim();

            if (CmbType.SelectedItem is string type)
                _connection.ConnectionType = type;

            if (int.TryParse(TxtGuardValue.Text.Trim(), out var guardVal))
                _connection.GuardValue = guardVal;

            _connection.Road = ChkRoad.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
