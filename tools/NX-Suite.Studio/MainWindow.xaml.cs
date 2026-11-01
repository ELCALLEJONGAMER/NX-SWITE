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
                ("⭐  Recomendados", CrearRecomendados),
                ("🛠  Herramientas", () => new EditorJson(_doc, "tools", "Herramientas externas del panel derecho.", Cambio)),
                ("🔊  Sonidos",      () => new EditorJson(_doc, "Sonidos", "URLs de los sonidos de la interfaz.", Cambio)),
                ("🃏  Tarjetas CFW", () => new EditorJson(_doc, "tarjetasHubCfw", "Tarjetas del hub CFW.", Cambio)),
                ("🎨  Interfaz",     () => new EditorJson(_doc, "ConfiguracionUI", "Branding, logos y textos generales.", Cambio)),
                ("✅  Validar y publicar", () => new VistaPublicar(_doc, ActualizarEstado)),
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

        private UIElement CrearRecomendados() => new EditorLista(
            "Recomendados", "Módulos que el asistente instala por defecto.",
            _doc.Lista("Recomendados"),
            new[]
            {
                new Campo("Id", "Id del módulo"),
                new Campo("Orden", "Orden", TipoCampo.Numero),
                new Campo("Obligatorio", "Obligatorio", TipoCampo.Booleano),
                new Campo("Nota", "Nota"),
            },
            o => $"{DocumentoCatalogo.Texto(o["Orden"])}. {DocumentoCatalogo.Texto(o["Id"])}",
            () => new JsonObject { ["Id"] = "", ["Orden"] = _doc.Lista("Recomendados").Count + 1, ["Obligatorio"] = false, ["Nota"] = "" },
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
