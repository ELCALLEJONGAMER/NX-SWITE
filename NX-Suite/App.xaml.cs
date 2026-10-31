using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using NX_Swite.UI;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace NX_Swite
{
    public partial class App : Application
    {
        private static Exception? _errorMigracion;
        private static bool _huboMigracion;

        private void App_Startup(object sender, StartupEventArgs e)
        {
            RegistrarManejadoresGlobales();

            // La migración va antes del Logger para que este no cree la carpeta nueva
            // y fuerce el camino lento de fusión. El resultado se registra después.
            MigrarAppDataAntiguo();
            Logger.IniciarSesion();
            RegistrarResultadoMigracion();

            var splash = new VentanaSplash();
            MainWindow = splash;
            splash.Show();
        }

        /// <summary>
        /// Registra los manejadores globales de excepciones para que ningún fallo
        /// (incluidos <c>async void</c> y Tasks no observadas) cierre la app sin dejar rastro en el log.
        /// </summary>
        private void RegistrarManejadoresGlobales()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Logger.Error("Excepción no observada en Task", args.Exception);
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    Logger.Error($"Excepción fatal (cierre={args.IsTerminating})", ex);
            };
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error("Excepción no controlada en la UI", e.Exception);

            MessageBox.Show(
                "Ocurrió un error inesperado. La operación se canceló, pero la aplicación sigue funcionando.\n\n" +
                $"Detalle: {e.Exception.Message}\n\nRevisa el log para más información.",
                ConfiguracionLocal.NombreApp, MessageBoxButton.OK, MessageBoxImage.Warning);

            e.Handled = true;
        }

        private static void RegistrarResultadoMigracion()
        {
            if (_errorMigracion != null)
                Logger.Error("Fallo al migrar AppData de NX-Suite a NX-Swite", _errorMigracion);
            else if (_huboMigracion)
                Logger.Info("AppData migrado de NX-Suite a NX-Swite");
        }

        /// <summary>
        /// Migra el contenido de la carpeta AppData antigua (NX-Suite) a la nueva (NX-Swite).
        /// Si la carpeta nueva no existe se mueve directamente. Si ya existe (arranque parcial
        /// previo) se copia archivo por archivo sin sobreescribir lo que ya esté actualizado,
        /// y al terminar se elimina la carpeta antigua para liberar espacio.
        /// </summary>
        private static void MigrarAppDataAntiguo()
        {
            try
            {
                string antigua = ConfiguracionLocal.RutaAppDataAntiguo;
                string nueva   = ConfiguracionLocal.RutaAppData;

                if (!Directory.Exists(antigua))
                    return;

                _huboMigracion = true;

                if (!Directory.Exists(nueva))
                {
                    // Caso limpio: mover todo de golpe
                    Directory.Move(antigua, nueva);
                    return;
                }

                // Caso parcial: fusionar archivo por archivo y luego borrar la antigua
                MigrarRecursivo(antigua, nueva);
                Directory.Delete(antigua, recursive: true);
            }
            catch (Exception ex)
            {
                // No crítico: la app arranca igual; se registra cuando el Logger esté listo
                _errorMigracion = ex;
            }
        }

        /// <summary>
        /// Copia recursivamente los archivos de <paramref name="origen"/> a
        /// <paramref name="destino"/>. Solo sobreescribe si el archivo de origen
        /// es más reciente, para no perder trabajo ya descargado.
        /// </summary>
        private static void MigrarRecursivo(string origen, string destino)
        {
            Directory.CreateDirectory(destino);

            foreach (string archivo in Directory.GetFiles(origen))
            {
                string nombreArchivo = Path.GetFileName(archivo);
                string archivoDestino = Path.Combine(destino, nombreArchivo);

                // Solo copiar si no existe en destino o el origen es más nuevo
                if (!File.Exists(archivoDestino) ||
                    File.GetLastWriteTimeUtc(archivo) > File.GetLastWriteTimeUtc(archivoDestino))
                {
                    File.Copy(archivo, archivoDestino, overwrite: true);
                }
            }

            foreach (string subcarpeta in Directory.GetDirectories(origen))
            {
                string nombre = Path.GetFileName(subcarpeta);
                MigrarRecursivo(subcarpeta, Path.Combine(destino, nombre));
            }
        }
    }
}