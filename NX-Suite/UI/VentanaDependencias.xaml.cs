using NX_Suite.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NX_Suite.UI
{
    /// <summary>
    /// Diálogo pre-instalación que muestra las dependencias de un módulo
    /// y permite al usuario decidir qué hacer con cada una.
    /// </summary>
    public partial class VentanaDependencias : Window
    {
        // ?? Resultado de la decisión del usuario ?????????????????????????????

        public enum Accion { InstalarSeleccionadas, ContinuarSin, Cancelar }

        /// <summary>Decisión tomada por el usuario al cerrar el diálogo.</summary>
        public Accion AccionElegida { get; private set; } = Accion.Cancelar;

        /// <summary>Dependencias cuya casilla quedó marcada (solo las accionables).</summary>
        public IReadOnlyList<ResultadoDependencia> DepsSeleccionadas { get; private set; }
            = new List<ResultadoDependencia>();

        // ?? Datos internos ????????????????????????????????????????????????????

        private readonly List<ResultadoDependencia> _deps;

        // ?? Constructor ???????????????????????????????????????????????????????

        public VentanaDependencias(ModuloConfig moduloDestino, List<ResultadoDependencia> dependencias)
        {
            InitializeComponent();

            _deps = dependencias;

            ConfigurarEncabezado(moduloDestino);
            ConfigurarResumen();
            ListaDependencias.ItemsSource = _deps;
        }

        // ?? Configuración de la UI ????????????????????????????????????????????

        private void ConfigurarEncabezado(ModuloConfig modulo)
        {
            int total   = _deps.Count;
            int pending = _deps.Count(d => d.Estado != EstadoDependencia.OK);

            TxtTitulo.Text = "Dependencias requeridas";

            TxtSubtitulo.Text = pending == 0
                ? $"{modulo.Nombre} tiene {total} dependencia{(total != 1 ? "s" : "")} — todas están instaladas y actualizadas."
                : $"{modulo.Nombre} requiere {total} dependencia{(total != 1 ? "s" : "")}. " +
                  $"{pending} necesita{(pending != 1 ? "n" : "")} atención antes de instalar.";
        }

        private void ConfigurarResumen()
        {
            int noInstaladas    = _deps.Count(d => d.Estado == EstadoDependencia.NoInstalada);
            int parciales       = _deps.Count(d => d.Estado == EstadoDependencia.Parcial);
            int desactualizadas = _deps.Count(d => d.Estado == EstadoDependencia.Desactualizada);
            int ok              = _deps.Count(d => d.Estado == EstadoDependencia.OK);

            if (noInstaladas > 0)
                AgregarBadgeResumen($"  {noInstaladas} sin instalar  ", "#FF5555");

            if (parciales > 0)
                AgregarBadgeResumen($"  {parciales} incompleta{(parciales != 1 ? "s" : "")}  ", "#FFD54A");

            if (desactualizadas > 0)
                AgregarBadgeResumen($"  {desactualizadas} desactualizada{(desactualizadas != 1 ? "s" : "")}  ", "#FFD54A");

            if (ok > 0)
                AgregarBadgeResumen($"  {ok} correcta{(ok != 1 ? "s" : "")}  ", "#40C057");

            // Si todas están OK, desactivar el botón primario
            if (noInstaladas == 0 && parciales == 0 && desactualizadas == 0)
                BtnInstalar.IsEnabled = false;
        }

        private void AgregarBadgeResumen(string texto, string colorHex)
        {
            var color  = (Color)ColorConverter.ConvertFromString(colorHex);
            var brush  = new SolidColorBrush(color);
            var brushBg = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));

            var borde = new System.Windows.Controls.Border
            {
                CornerRadius   = new CornerRadius(6),
                Background     = brushBg,
                BorderBrush    = brush,
                BorderThickness = new Thickness(1),
                Margin         = new Thickness(0, 0, 8, 0),
                Padding        = new Thickness(0, 4, 0, 4)
            };

            var txt = new System.Windows.Controls.TextBlock
            {
                Text       = texto,
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = brush
            };

            borde.Child = txt;
            PanelResumen.Children.Add(borde);
        }

        // ?? Handlers de la barra superior ????????????????????????????????????

        private void TopBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            AccionElegida = Accion.Cancelar;
            Close();
        }

        // ?? Handlers de acciones ??????????????????????????????????????????????

        private void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            DepsSeleccionadas = _deps
                .Where(d => d.Seleccionada && d.EsAccionable)
                .ToList()
                .AsReadOnly();

            AccionElegida = Accion.InstalarSeleccionadas;
            Close();
        }

        private void BtnContinuarSin_Click(object sender, RoutedEventArgs e)
        {
            AccionElegida = Accion.ContinuarSin;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            AccionElegida = Accion.Cancelar;
            Close();
        }
    }
}
