using System.Windows;

namespace NX_Suite.Studio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string? raiz = DocumentoCatalogo.BuscarRaizRepo() ?? ResolverRepo();
            if (raiz == null)
            {
                Shutdown(1);
                return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var doc = new DocumentoCatalogo(raiz);
            doc.Cargar();
            var ventana = new MainWindow(doc);
            MainWindow = ventana;
            ventana.Show();
        }

        /// <summary>Sin repositorio: clonarlo junto al .exe o elegir una carpeta existente (se recuerda).</summary>
        private static string? ResolverRepo()
        {
            while (true)
            {
                var r = MessageBox.Show(
                    "No se encontró el repositorio NX-SWITE (catalogo/catalogo.json).\n\n" +
                    "Sí: descargarlo ahora con Git en la carpeta 'NX-SWITE' junto al ejecutable.\n" +
                    "No: elegir una carpeta donde ya tengas el repositorio.\n" +
                    "Cancelar: salir.",
                    "NX-STUDIO", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return null;

                string? ruta = r == MessageBoxResult.Yes ? ModoPortable.ClonarRepo(out string error) : ElegirCarpeta(out error);
                if (ruta != null)
                {
                    ModoPortable.RecordarRepo(ruta);
                    return ruta;
                }
                if (error.Length > 0) MessageBox.Show(error, "NX-STUDIO", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string? ElegirCarpeta(out string error)
        {
            error = "";
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta del repositorio NX-SWITE" };
            if (dlg.ShowDialog() != true) return null;
            if (ModoPortable.EsRepo(dlg.FolderName)) return dlg.FolderName;
            error = "Esa carpeta no contiene catalogo/catalogo.json.";
            return null;
        }
    }
}
