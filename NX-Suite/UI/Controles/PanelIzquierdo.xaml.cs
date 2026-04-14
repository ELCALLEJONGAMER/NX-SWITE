using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NX_Suite.Models;

namespace NX_Suite.UI.Controles
{
    public partial class PanelIzquierdo : UserControl
    {
        public static readonly DependencyProperty NombreProgramaProperty =
            DependencyProperty.Register(
                nameof(NombrePrograma),
                typeof(string),
                typeof(PanelIzquierdo),
                new PropertyMetadata(string.Empty, OnNombreProgramaChanged));

        public static readonly DependencyProperty LogoUrlProperty =
            DependencyProperty.Register(
                nameof(LogoUrl),
                typeof(string),
                typeof(PanelIzquierdo),
                new PropertyMetadata(string.Empty));

        public string NombrePrograma
        {
            get => (string)GetValue(NombreProgramaProperty);
            set => SetValue(NombreProgramaProperty, value);
        }

        public string LogoUrl
        {
            get => (string)GetValue(LogoUrlProperty);
            set => SetValue(LogoUrlProperty, value);
        }

        public PanelIzquierdo()
        {
            InitializeComponent();
        }

        public Task AplicarBrandingAsync(BrandingConfig branding)
        {
            if (branding == null)
                return Task.CompletedTask;

            NombrePrograma = branding.NombrePrograma ?? string.Empty;
            LogoUrl = branding.LogoUrl ?? string.Empty;

            bool tieneTexto = !string.IsNullOrWhiteSpace(NombrePrograma);

            TxtNombrePrograma.Visibility = tieneTexto ? Visibility.Visible : Visibility.Collapsed;
            ImgLogoPrograma.Visibility = string.IsNullOrWhiteSpace(LogoUrl)
                ? Visibility.Collapsed
                : Visibility.Visible;

            CabeceraPrograma.Margin = tieneTexto
                ? new Thickness(0, 10, 0, 35)
                : new Thickness(0, 10, 0, 18);

            return Task.CompletedTask;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private static void OnNombreProgramaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanelIzquierdo panel)
            {
                panel.TxtNombrePrograma.Text = e.NewValue as string ?? string.Empty;
            }
        }
    }
}

