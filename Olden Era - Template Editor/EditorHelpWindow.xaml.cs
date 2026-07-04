using System.Windows;

namespace Olden_Era___Template_Editor
{
    public partial class EditorHelpWindow : Window
    {
        public EditorHelpWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
