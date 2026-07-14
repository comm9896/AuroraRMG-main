using System.Windows;
using System.Windows.Input;

namespace Olden_Era___Template_Editor
{
    /// <summary>Modal dialog configuring the visual editor's mirror-creation mode.</summary>
    public partial class MirrorSettingsWindow : Window
    {
        /// <summary>Whether mirror creation is enabled (master toggle).</summary>
        public bool Enable => ChkEnable.IsChecked == true;

        /// <summary>Whether zone property edits are mirrored to the twin.</summary>
        public bool MirrorProps => ChkProps.IsChecked == true;

        /// <summary>Whether connections (and their settings) are mirrored to the twins.</summary>
        public bool MirrorConns => ChkConns.IsChecked == true;

        public MirrorSettingsWindow(bool enable, bool props, bool conns)
        {
            InitializeComponent();
            ChkEnable.IsChecked = enable;
            ChkProps.IsChecked = props;
            ChkConns.IsChecked = conns;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void ChkEnable_Changed(object sender, RoutedEventArgs e) { }

        private void BtnHub_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is TemplateEditorWindow te)
                te.CreateHubZoneExternally();
            DialogResult = false; // close without re-applying mirror enable/disable
        }
    }
}
