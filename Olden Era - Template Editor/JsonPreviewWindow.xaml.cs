using System;
using System.Text.Json;
using System.Windows;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using IOPath = System.IO.Path;

namespace Olden_Era___Template_Editor
{
    public partial class JsonPreviewWindow : Window
    {
        private readonly TemplateEditorWindow _editor;
        private static readonly JsonSerializerOptions JsonOptions = Olden_Era___Template_Editor.Services.JsonExport.Options;
        private string _originalJson = "";
        private bool _modified;
        private bool _suppressValidation;

        public JsonPreviewWindow(TemplateEditorWindow editor)
        {
            InitializeComponent();
            _editor = editor;
            Owner = editor;
            RefreshJson();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            JsonBox.Focus();
            JsonBox.CaretIndex = 0;
        }

        private void RefreshJson()
        {
            _originalJson = JsonSerializer.Serialize(_editor.CurrentTemplate, JsonOptions);
            _suppressValidation = true;
            JsonBox.Text = _originalJson;
            _suppressValidation = false;
            _modified = false;
            StatusText.Text = "OK";
            StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            BtnApply.IsEnabled = false;
            if (JsonBox.IsFocused)
                JsonBox.CaretIndex = JsonBox.Text.Length;
        }

        private void JsonBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressValidation) return;
            _modified = true;
            ValidateJson();
        }

        private void JsonBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_modified) ValidateJson();
        }

        private void ValidateJson()
        {
            try
            {
                var result = JsonSerializer.Deserialize<RmgTemplate>(JsonBox.Text, JsonOptions);
                if (result != null)
                {
                    StatusText.Text = "JSON valid";
                    StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                    BtnApply.IsEnabled = true;
                }
                else
                {
                    StatusText.Text = "Deserialization returned null";
                    StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    BtnApply.IsEnabled = false;
                }
            }
            catch (JsonException ex)
            {
                StatusText.Text = $"JSON error: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnApply.IsEnabled = false;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnApply.IsEnabled = false;
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = JsonSerializer.Deserialize<RmgTemplate>(JsonBox.Text, JsonOptions);
                if (result == null)
                {
                    MessageBox.Show(this, "Deserialization returned null.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _editor.LoadTemplate(result);
                RefreshJson();
                _editor.UpdateStatus("Template updated from JSON.");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Apply failed: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnApply.IsEnabled = false;

                MessageBox.Show(this,
                    $"Failed to apply JSON changes:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            RefreshJson();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
