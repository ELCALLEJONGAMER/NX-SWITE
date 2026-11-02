using System.Windows;

namespace NX_Suite.Studio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string? raiz = DocumentoCatalogo.BuscarRaizRepo();
            if (raiz == null)
            {
                MessageBox.Show(
                    "No se encontró catalogo/catalogo.json.\nEjecuta NX-STUDIO desde el repositorio NX-SWITE " +
                                        "o coloca una copia del repositorio en la carpeta 'NX-SWITE' junto al ejecutable.",
                    "NX-STUDIO", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var doc = new DocumentoCatalogo(raiz);
            doc.Cargar();
            new MainWindow(doc).Show();
        }
    }
}
