using NX_Suite.Models;
using NX_Suite.Core;
using NX_Suite.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Pantalla inicial de noticias y acciones asociadas.
    /// </summary>
    public partial class MainWindow
    {
        private void MostrarVistaInicio()
        {
            bool estabaCargando = _cargandoCatalogoInicial;
            _cargandoCatalogoInicial = true;
            MenuMundos.ListaMundos.SelectedIndex = -1;
            _cargandoCatalogoInicial = estabaCargando;

            _moduloActual = null;
            _detalleDesdeAsistido = false;
            _mundoSeleccionado = null;
            _filtroSeleccionado = null;
            _textoBusqueda = string.Empty;

            TxtTopBarSeccion.Text = "Inicio";
            PanelTituloSeccion.Visibility = Visibility.Collapsed;
            PanelChipsFiltro.Visibility = Visibility.Collapsed;
            BtnHerramientasPersonalizacion.Visibility = Visibility.Collapsed;

            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaDetalle.Visibility = Visibility.Collapsed;
            VistaAsistida.Visibility = Visibility.Collapsed;
            VistaPersonalizacion.Visibility = Visibility.Collapsed;
            VistaNews.Visibility = Visibility.Visible;

            CargarNewsInicio();
            BtnCerrarPaneles_Click(null, null);
        }

        private void CargarNewsInicio()
        {
            List<NewsItem> news = _datosGist?.News?
                .Where(n => n != null && (!string.IsNullOrWhiteSpace(n.Title) || !string.IsNullOrWhiteSpace(n.Description)))
                .ToList() ?? new List<NewsItem>();

            ListaNews.ItemsSource = news;
            TxtSinNews.Visibility = news.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Dispatcher.InvokeAsync(ConectarHoverNews);
        }

        private void BtnMensajes_Click(object sender, RoutedEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            MostrarVistaInicio();
        }

        private void News_ClickBoton(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not NewsItem news)
                return;

            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            AbrirLinkNews(news.Link);
        }

        private void ConectarHoverNews()
        {
            if (ListaNews.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                return;

            foreach (var item in ListaNews.Items)
            {
                if (ListaNews.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter cp)
                    continue;

                cp.MouseEnter -= News_HoverTarjeta;
                cp.MouseEnter += News_HoverTarjeta;
            }
        }

        private void News_HoverTarjeta(object sender, MouseEventArgs e)
        {
            if (_cargandoCatalogoInicial) return;
            Servicios.Sonidos.Reproducir(EventoSonido.Hover);
        }

        private static void AbrirLinkNews(string link)
        {
            if (string.IsNullOrWhiteSpace(link))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = link,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Dialogos.Error($"No se pudo abrir la noticia: {ex.Message}");
            }
        }
    }
}
