using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.UI.Controles
{
    public partial class PanelIzquierdo : UserControl
    {
        public PanelIzquierdo()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}