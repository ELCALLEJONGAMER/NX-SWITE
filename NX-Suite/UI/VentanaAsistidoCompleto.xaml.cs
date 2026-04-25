using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_Suite.UI
{
    public partial class VentanaAsistidoCompleto : Window
    {
        private readonly List<ModuloConfig>  _todosModulos;
        private readonly EscanerDiscos         _scanner = new();
        private SDInfo?                      _sdSel;
        private int                          _gbEmuMMC = 12;

        private static readonly int[] _gbTicks = { 4, 8, 12, 16, 24, 32, 48, 64 };

        public event EventHandler<ProcesarCompletoArgs>? ProcesarSolicitado;

        /// <summary>Letra de la SD seleccionada en el combo de esta ventana.</summary>
        public string? LetraSD => _sdSel?.Letra;

        /// <summary>Numero de disco fisico de la SD seleccionada.</summary>
        public int DiscoFisico => _sdSel?.DiscoFisico ?? -1;

        /// <summary>GB seleccionados para la particion emuMMC.</summary>
        public int GbEmuMMC => _gbEmuMMC;

        public VentanaAsistidoCompleto(List<ModuloConfig> todosModulos)
        {
            InitializeComponent();
            _todosModulos = todosModulos;
            CargarSD();
            CargarModulosRecomendados();
            SliderGb.Value = 2; // indice 2 = 12 GB
            ActualizarVistaSlider(12);
        }

        private void TopBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();
        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void CargarSD()
        {
            var unidades = _scanner.ObtenerUnidadesRemovibles();
            ComboSD.ItemsSource       = unidades;
            ComboSD.DisplayMemberPath = "FullName";
            if (unidades.Count > 0) ComboSD.SelectedIndex = 0;
        }

        private void ComboSD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _sdSel = ComboSD.SelectedItem as SDInfo;
            ActualizarInfoSD();
            ActualizarRecomendacionSlider();
            ActualizarBoton();
        }

        private void ActualizarInfoSD()
        {
            if (_sdSel == null) { TxtInfoSD.Text = "Sin SD detectada"; return; }
            TxtInfoSD.Text = $"Disco #{_sdSel.DiscoFisico}  -  {_sdSel.CapacidadTotal} GB  -  {_sdSel.Formato}";
        }

        private void ActualizarRecomendacionSlider()
        {
            if (_sdSel == null || !int.TryParse(_sdSel.CapacidadTotal, out int sdGb)) return;
            int rec = sdGb >= 512 ? 24 : 12;
            TxtRecomendado.Text = $"Recomendado: {rec} GB";
            int idx = Array.IndexOf(_gbTicks, rec);
            if (idx >= 0) SliderGb.Value = idx;
        }

        private void SliderGb_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int idx = (int)Math.Round(e.NewValue);
            idx = Math.Clamp(idx, 0, _gbTicks.Length - 1);
            _gbEmuMMC = _gbTicks[idx];
            ActualizarVistaSlider(_gbEmuMMC);
        }

        private void ActualizarVistaSlider(int gb)
        {
            if (TxtGbValor == null) return;
            TxtGbValor.Text       = $"{gb} GB";
            BadgeRec12.Visibility = gb == 12 ? Visibility.Visible : Visibility.Collapsed;
            BadgeRec24.Visibility = gb == 24 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CargarModulosRecomendados()
        {
            var vms = ConfiguracionRemota.Recomendados
                .Select(r =>
                {
                    var m = _todosModulos.FirstOrDefault(x =>
                        string.Equals(x.Id, r.Id, StringComparison.OrdinalIgnoreCase));
                    return m == null ? null : new RecomendadoVM { Modulo = m, Config = r };
                })
                .Where(v => v != null)
                .ToList();

            ListaModulos.ItemsSource = vms;
            ActualizarBoton();
        }

        private void ActualizarBoton()
        {
            BtnIniciar.IsEnabled = _sdSel != null
                                && (ListaModulos.ItemsSource as IEnumerable<RecomendadoVM>)?.Any() == true;
        }

        private void BtnIniciar_Click(object sender, RoutedEventArgs e)
        {
            if (_sdSel == null) return;

            if (!Dialogos.Confirmar(
                    $"Se borrarán TODOS los datos del disco {_sdSel.Letra} ({_sdSel.CapacidadTotal} GB).\n\n¿Deseas continuar?",
                    "Confirmar formateo", MessageBoxImage.Warning))
                return;

            var modulos = (ListaModulos.ItemsSource as IEnumerable<RecomendadoVM>)?
                .Select(v => v.Modulo).ToList() ?? new();

            ProcesarSolicitado?.Invoke(this, new ProcesarCompletoArgs
            {
                GbEmuMMC    = _gbEmuMMC,
                LetraSD     = _sdSel?.Letra,
                NumeroDisco = _sdSel?.DiscoFisico ?? -1,
                Modulos     = modulos,
                Logger      = null
            });

            // Cerrar la ventana — el progreso se ve en la cola global de MainWindow
            Close();
        }
    }
}
