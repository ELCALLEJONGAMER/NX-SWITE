using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using NX_Suite.Studio.Vistas;

namespace NX_Suite.Studio
{
    public partial class MainWindow : Window
    {
        private readonly DocumentoCatalogo _doc;
        private readonly (string Nombre, Func<UIElement> Crear)[] _secciones;

        internal MainWindow(DocumentoCatalogo doc)
        {
            InitializeComponent();
            _doc = doc;

            _secciones = new (string, Func<UIElement>)[]
            {
                ("📦  Módulos",      () => new VistaActualizaciones(_doc, Cambio)),
                ("📰  Noticias",     CrearNoticias),
                ("🚀  App y versión", CrearApp),
                ("🛠  Herramientas", () => new EditorClaves(_doc, "tools", "Herramientas externas del panel derecho.", Cambio)),
                ("🔊  Sonidos",      () => new EditorClaves(_doc, "Sonidos", "URLs de los sonidos de la interfaz.", Cambio)),
                ("🃏  Tarjetas CFW", () => new EditorClaves(_doc, "tarjetasHubCfw", "Tarjetas del hub CFW.", Cambio)),
                ("🎨  Interfaz",     () => new EditorClaves(_doc, "ConfiguracionUI", "Iconos y colores de la interfaz.", Cambio,
                                          k => !ClavesConfiguracion.Contains(k) && !ClavesInternas.Contains(k), "Interfaz")),
                ("⚙  Configuración", CrearConfiguracion),
                ("🔑  Master keys",  () => new EditorMasterKeys(_doc, Cambio)),
                ("🏷  Marca",        () => new EditorClaves(_doc, "GlobalBranding", "Nombre, logo y color de acento.", Cambio)),
            };
            Menu.ItemsSource = _secciones.Select(s => s.Nombre).ToList();
            Menu.SelectedIndex = 0;
            ActualizarEstado();
        }

        private void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Menu.SelectedIndex >= 0) Contenido.Content = _secciones[Menu.SelectedIndex].Crear();
        }

        private void Cambio()
        {
            _doc.Modificado = true;
            ActualizarEstado();
        }

        private void ActualizarEstado()
        {
            TxtEstado.Text = $"{_doc.Ruta}   ·   {_doc.Lista("Modulos").Count} módulos   ·   " +
                             (_doc.Modificado ? "● Cambios sin guardar" : "Sin cambios");
            Title = _doc.Modificado ? "NX-STUDIO *" : "NX-STUDIO";
        }

        // ── Secciones con formulario ───────────────────────────────────────

        private UIElement CrearNoticias() => new EditorLista(
            "Noticias", "Se muestran en la pantalla de inicio en este orden.",
            _doc.Lista("news"),
            new[]
            {
                new Campo("title", "Título"),
                new Campo("description", "Texto", TipoCampo.Multilinea),
                new Campo("imageUrl", "Imagen", TipoCampo.Imagen),
                new Campo("link", "Enlace al pulsar (opcional)"),
            },
            o => DocumentoCatalogo.Texto(o["title"]),
            () => new JsonObject { ["title"] = "Nueva noticia", ["description"] = "", ["imageUrl"] = "", ["link"] = "" },
            Cambio);

        private UIElement CrearApp()
        {
            var panel = new StackPanel();
            panel.Children.Add(Formularios.Titulo("Actualización de NX-SWITE"));
            panel.Children.Add(Formularios.Ayuda("Al subir AppVersion, los usuarios con una versión anterior verán el aviso de actualización con estas notas."));
            panel.Children.Add(Formularios.Crear(_doc.Raiz, new[]
            {
                new Campo("AppVersion", "Versión publicada (ej. 0.8.0)"),
                new Campo("AppUpdateUrl", "URL del ZIP de la release"),
                new Campo("AppUpdateNotes", "Notas de la versión (Markdown)", TipoCampo.Multilinea),
            }, Cambio));
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        /// <summary>Claves de ConfiguracionUI que no son apariencia: van en «Configuración».</summary>
        private static readonly HashSet<string> ClavesConfiguracion = new(StringComparer.OrdinalIgnoreCase)
        {
            "version_firmware_rp2040",
        };

        /// <summary>Claves con editor propio o retiradas (la tabla de modelos ahora está en el código).</summary>
        private static readonly HashSet<string> ClavesInternas = new(StringComparer.OrdinalIgnoreCase)
        {
            "tabla_master_keys", "tabla_modelos_switch", "nota_certificado",
        };

        private UIElement CrearConfiguracion()
        {
            var ui = _doc.Objeto("ConfiguracionUI");
            var panel = new StackPanel();
            panel.Children.Add(Formularios.Titulo("Asistido completo"));
            panel.Children.Add(Formularios.Crear(ui, new[]
            {
                new Campo("VersionCompatible", "Firmware para el que está preparado el asistido (ej. 23.0.1)"),
                new Campo("UrlFat32Format", "URL de fat32format.exe", Ayuda: "Se descarga para formatear la microSD en FAT32."),
            }, Cambio));
            panel.Children.Add(Formularios.Titulo("Picofly / RP2040"));
            panel.Children.Add(Formularios.Crear(ui, new[]
            {
                new Campo("version_firmware_rp2040", "Versión publicada del firmware (ej. 2.7.3)"),
                new Campo("url_firmware_rp2040", "URL del archivo .uf2"),
            }, Cambio));
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // ── Barra inferior ─────────────────────────────────────────────────

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var errores = _doc.Validar();
            if (errores.Count > 0 &&
                MessageBox.Show("Hay errores:\n\n• " + string.Join("\n• ", errores.Take(15)) + "\n\n¿Guardar igualmente (sin publicar)?",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _doc.Guardar();
            ActualizarEstado();
        }

        private void BtnRecargar_Click(object sender, RoutedEventArgs e)
        {
            if (_doc.Modificado && MessageBox.Show("Se perderán los cambios sin guardar. ¿Recargar?",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _doc.Cargar();
            Menu_SelectionChanged(Menu, null!);
            ActualizarEstado();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_doc.Modificado)
            {
                var r = MessageBox.Show("Hay cambios sin guardar. ¿Guardar antes de salir?",
                    "NX-STUDIO", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (r == MessageBoxResult.Yes) _doc.Guardar();
            }
            base.OnClosing(e);
        }
    }
}
