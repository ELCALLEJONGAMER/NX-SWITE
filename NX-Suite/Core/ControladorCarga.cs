using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Swite.Core
{
    public class ControladorCarga
    {
        private static readonly Brush BrushGris   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#707080"));
        private static readonly Brush BrushCyan   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF"));
        private static readonly Brush BrushMorado = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BD00FF"));
        private static readonly Brush BrushVerde  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40C057"));
        private static readonly Brush BrushRojo   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4444"));

        private const double AnchoMaximoBarra = 394;
        private const double AnchoBarraIndeterminada = 110;

        private readonly UIElement _overlayCarga;
        private readonly TextBlock _txtSubtitulo;
        private readonly TextBlock _txtDetalle;
        private readonly TextBlock _txtPorcentaje;
        private readonly FrameworkElement _barraProgreso;
        private readonly TextBlock _paso1;
        private readonly TextBlock _paso2;
        private readonly TextBlock _paso3;
        private readonly TextBlock _paso4;

        public ControladorCarga(
            UIElement overlay,
            TextBlock subtitulo,
            TextBlock detalle,
            TextBlock porcentaje,
            FrameworkElement barra,
            TextBlock p1, TextBlock p2, TextBlock p3, TextBlock p4)
        {
            _overlayCarga   = overlay;
            _txtSubtitulo   = subtitulo;
            _txtDetalle     = detalle;
            _txtPorcentaje  = porcentaje;
            _barraProgreso  = barra;
            _paso1 = p1; _paso2 = p2; _paso3 = p3; _paso4 = p4;
        }

        /// <summary>
        /// Hook opcional ejecutado justo antes de hacer visible el overlay.
        /// Lo usa MainWindow para aplicar el blur al fondo y mantener una
        /// pantalla de carga frosted-glass coherente con el resto de overlays.
        /// </summary>
        public Action? AntesDeMostrar { get; set; }

        /// <summary>
        /// Hook opcional ejecutado justo después de ocultar el overlay.
        /// MainWindow lo usa para retirar el blur del fondo.
        /// </summary>
        public Action? DespuesDeOcultar { get; set; }

        /// <summary>Panel con los 4 pasos de instalación; se oculta en modo análisis.</summary>
        public UIElement? PanelPasos { get; set; }

        /// <summary>Contenedor y lista de módulos del lote (pendiente / instalando / terminado).</summary>
        public FrameworkElement? ContenedorModulos { get; set; }
        public Panel? ListaModulos { get; set; }

        // ── Modo lote: progreso global de N módulos ──
        private int _loteTotal;
        private int _loteIndice = -1;
        private int _tramoFrase = -1;
        private readonly Random _azar = new();
        private readonly List<TextBlock> _filasLote = new();

        private static readonly string[][] FrasesPorTramo =
        {
            new[] { "Calentando los motores del hamster…", "Despertando a los servidores con un café…", "Afinando la guitarra antes del concierto…", "Buscando el cable que siempre se pierde…" },
            new[] { "Descargando paquete de videos de gatitos…", "Negociando con los píxeles rebeldes…", "Pidiendo permiso al alojador de archivos…", "Contando bytes de uno en uno…" },
            new[] { "Descomprimiendo los archivos comprimidos del alojador de archivos comprimidos…", "Doblando carpetas como ropa recién lavada…", "Desenredando cables invisibles…", "Enseñándole a la Switch trucos nuevos…" },
            new[] { "Acomodando todo en la Micro SD como Tetris…", "Puliendo los bits hasta que brillen…", "Sobornando al sistema de archivos…", "Revisando que no falte ninguna tuerca…" },
            new[] { "Terminando de asar la carne en el asador…", "Poniendo la cereza al pastel…", "Barriendo las migajas del instalador…", "Sirviendo el plato principal…" },
        };

        public bool EnLote => _loteTotal > 0;

        /// <summary>
        /// Inicia una instalación por lotes: la barra mide el avance global de todos los módulos,
        /// el texto muestra frases divertidas según el porcentaje y se lista el estado de cada módulo.
        /// </summary>
        public void IniciarLote(string tituloPrincipal, IReadOnlyList<string> nombresModulos)
        {
            _loteTotal = Math.Max(1, nombresModulos.Count);
            _loteIndice = -1;
            _tramoFrase = -1;

            _txtSubtitulo.Text = tituloPrincipal.ToUpper();
            DetenerIndeterminado();
            _barraProgreso.BeginAnimation(FrameworkElement.WidthProperty, null);
            _barraProgreso.Width = 0;
            if (PanelPasos != null) PanelPasos.Visibility = Visibility.Collapsed;

            _filasLote.Clear();
            ListaModulos?.Children.Clear();
            foreach (var nombre in nombresModulos)
            {
                var fila = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2), Tag = nombre };
                _filasLote.Add(fila);
                ListaModulos?.Children.Add(fila);
                PintarFila(fila, 0);
            }
            if (ContenedorModulos != null) ContenedorModulos.Visibility = Visibility.Visible;

            AplicarProgresoGlobal(0);
            AntesDeMostrar?.Invoke();
            _overlayCarga.Visibility = Visibility.Visible;
        }

        /// <summary>Marca el módulo <paramref name="indice"/> como «instalando» y los anteriores como terminados.</summary>
        public void IniciarModuloLote(int indice)
        {
            _loteIndice = indice;
            for (int i = 0; i < _filasLote.Count; i++)
                PintarFila(_filasLote[i], i < indice ? 2 : i == indice ? 1 : 0);
            if (indice >= 0 && indice < _filasLote.Count)
                _filasLote[indice].BringIntoView();
            AplicarProgresoGlobal((double)indice / _loteTotal * 100);
        }

        /// <summary>Marca el resultado final del módulo en curso.</summary>
        public void TerminarModuloLote(int indice, bool exito)
        {
            if (indice >= 0 && indice < _filasLote.Count)
                PintarFila(_filasLote[indice], exito ? 2 : 3);
            AplicarProgresoGlobal((double)(indice + 1) / _loteTotal * 100);
        }

        private static void PintarFila(TextBlock fila, int estado)
        {
            string nombre = fila.Tag as string ?? "";
            (fila.Text, fila.Foreground) = estado switch
            {
                1 => ("⟳  " + nombre, BrushCyan),
                2 => ("✔  " + nombre, BrushVerde),
                3 => ("✖  " + nombre, BrushRojo),
                _ => ("○  " + nombre, BrushGris),
            };
        }

        private void AplicarProgresoGlobal(double porcentaje)
        {
            porcentaje = Math.Clamp(porcentaje, 0, 100);
            _txtPorcentaje.Text = $"{porcentaje:F0}%";
            _barraProgreso.BeginAnimation(
                FrameworkElement.WidthProperty,
                new DoubleAnimation(porcentaje / 100.0 * AnchoMaximoBarra, TimeSpan.FromMilliseconds(250)));

            int tramo = Math.Min(FrasesPorTramo.Length - 1, (int)(porcentaje / 100.0 * FrasesPorTramo.Length));
            if (tramo != _tramoFrase)
            {
                _tramoFrase = tramo;
                var frases = FrasesPorTramo[tramo];
                _txtDetalle.Text = frases[_azar.Next(frases.Length)];
            }
        }

        public void Mostrar(string tituloPrincipal)
        {
            _loteTotal = 0;
            if (ContenedorModulos != null) ContenedorModulos.Visibility = Visibility.Collapsed;
            _txtSubtitulo.Text  = tituloPrincipal.ToUpper();
            _txtDetalle.Text    = "Preparando...";
            _txtPorcentaje.Text = "0%";
            DetenerIndeterminado();
            _barraProgreso.Width = 0;
            if (PanelPasos != null) PanelPasos.Visibility = Visibility.Visible;

            ActualizarColoresPasos(0);
            AntesDeMostrar?.Invoke();
            _overlayCarga.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Variante para tareas sin progreso medible (análisis rápidos): sin pasos ni
        /// porcentaje, con la barra recorriendo la pista de forma continua.
        /// </summary>
        public void MostrarAnalisis(string tituloPrincipal, string detalle)
        {
            _loteTotal = 0;
            if (ContenedorModulos != null) ContenedorModulos.Visibility = Visibility.Collapsed;
            _txtSubtitulo.Text  = tituloPrincipal.ToUpper();
            _txtDetalle.Text    = detalle;
            _txtPorcentaje.Text = "";
            if (PanelPasos != null) PanelPasos.Visibility = Visibility.Collapsed;

            _barraProgreso.BeginAnimation(FrameworkElement.WidthProperty, null);
            _barraProgreso.Width = AnchoBarraIndeterminada;
            var barrido = new ThicknessAnimation(
                new Thickness(-AnchoBarraIndeterminada, 0, 0, 0),
                new Thickness(AnchoMaximoBarra, 0, 0, 0),
                TimeSpan.FromMilliseconds(1100))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            _barraProgreso.BeginAnimation(FrameworkElement.MarginProperty, barrido);

            AntesDeMostrar?.Invoke();
            _overlayCarga.Visibility = Visibility.Visible;
        }

        private void DetenerIndeterminado()
        {
            _barraProgreso.BeginAnimation(FrameworkElement.MarginProperty, null);
            _barraProgreso.Margin = new Thickness(0);
        }

        public void Ocultar()
        {
            _loteTotal = 0;
            _overlayCarga.Visibility = Visibility.Collapsed;
            DetenerIndeterminado();
            DespuesDeOcultar?.Invoke();
        }

        public IProgress<EstadoProgreso> ObtenerReportador()
        {
            return new Progress<EstadoProgreso>(estado =>
            {
                if (EnLote)
                {
                    // Progreso del módulo en curso proyectado sobre su porción del total.
                    double baseLote = Math.Max(0, _loteIndice) / (double)_loteTotal * 100;
                    AplicarProgresoGlobal(baseLote + estado.Porcentaje / _loteTotal);
                    return;
                }

                _txtDetalle.Text    = estado.TareaActual;
                _txtPorcentaje.Text = $"{estado.Porcentaje:F0}%";

                double nuevoAncho = estado.Porcentaje / 100.0 * AnchoMaximoBarra;
                _barraProgreso.BeginAnimation(
                    FrameworkElement.WidthProperty,
                    new DoubleAnimation(nuevoAncho, TimeSpan.FromMilliseconds(150)));

                ActualizarColoresPasos(estado.PasoActual);
            });
        }

        private void ActualizarColoresPasos(int pasoActivo)
        {
            _paso1.Foreground = pasoActivo == 1 ? BrushCyan : pasoActivo > 1 ? BrushMorado : BrushGris;
            _paso1.Text = pasoActivo > 1 ? "✅ 1. Descarga completa" : "1. Descargando paquete";

            _paso2.Foreground = pasoActivo == 2 ? BrushCyan : pasoActivo > 2 ? BrushMorado : BrushGris;
            _paso2.Text = pasoActivo > 2 ? "✅ 2. Archivos extraídos" : "2. Extrayendo archivos";

            _paso3.Foreground = pasoActivo == 3 ? BrushCyan : pasoActivo > 3 ? BrushMorado : BrushGris;
            _paso3.Text = pasoActivo > 3 ? "✅ 3. SD Actualizada" : "3. Aplicando en SD";

            _paso4.Foreground = pasoActivo == 4 ? BrushCyan : pasoActivo > 4 ? BrushMorado : BrushGris;
            _paso4.Text = pasoActivo > 4 ? "✅ 4. Limpieza terminada" : "4. Limpiando temporales";
        }
    }
}
