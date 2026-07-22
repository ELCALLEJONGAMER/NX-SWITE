using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using NX_Swite.UI;
using System.IO;
using System.Windows;

namespace NX_Swite
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            MigrarAppDataAntiguo();
            Logger.IniciarSesion();

            var splash = new VentanaSplash();
            MainWindow = splash;
            splash.Show();
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
            catch { /* No crítico: si falla, la app arranca igual */ }
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