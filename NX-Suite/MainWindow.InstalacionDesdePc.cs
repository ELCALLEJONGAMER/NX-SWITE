using NX_Swite.Core;
using NX_Swite.Core.Pipeline;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Flujo de "Instalación desde PC": el usuario elige una
    /// carpeta local que ya contiene un paquete de Atmosphere preparado
    /// (con las carpetas "atmosphere", "bootloader" y "switch" en la raíz) y
    /// se copia directamente a la microSD seleccionada en el panel derecho.
    /// </summary>
    public partial class MainWindow
    {
        private static readonly string[] _carpetasPaqueteAtmosphere = { "atmosphere", "bootloader", "switch" };

        /// <summary>
        /// Valida que <paramref name="carpetaRaiz"/> contenga al menos una de
        /// las carpetas esperadas de un paquete de Atmosphere
        /// ("atmosphere", "bootloader", "switch").
        /// </summary>
        private static bool EsPaqueteAtmosphereValido(string carpetaRaiz)
        {
            if (string.IsNullOrWhiteSpace(carpetaRaiz) || !Directory.Exists(carpetaRaiz))
                return false;

            foreach (string nombre in _carpetasPaqueteAtmosphere)
            {
                if (Directory.Exists(Path.Combine(carpetaRaiz, nombre)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Punto de entrada del modo "Instalación desde PC": solicita la SD
        /// activa, pide al usuario la carpeta de origen, valida que sea un
        /// paquete de Atmosphere válido y copia su contenido a la raíz de la
        /// microSD, reportando progreso en el overlay de carga global.
        /// </summary>
        public async void AbrirOverlayInstalacionDesdePc()
        {
            var sd = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (sd == null || sd.DiscoFisico < 0)
            {
                Dialogos.Advertencia(
                    "No se detecto ninguna microSD conectada. Conecta una microSD e intentalo de nuevo.",
                    "Sin microSD");
                return;
            }

            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Selecciona la carpeta que contiene el paquete de Atmosphere"
            };

            if (dlg.ShowDialog() != true)
                return;

            string carpetaOrigen = dlg.FolderName;

            if (!EsPaqueteAtmosphereValido(carpetaOrigen))
            {
                Dialogos.Advertencia(
                    "La carpeta seleccionada no contiene un paquete de Atmosphere valido. " +
                    "Selecciona una carpeta que contenga un paquete de Atmosphere valido " +
                    "(debe incluir las carpetas \"atmosphere\", \"bootloader\" o \"switch\").",
                    "Paquete invalido");
                return;
            }

            // Releer la SD por si el usuario cambio la seleccion mientras elegia la carpeta
            sd = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (sd == null || sd.DiscoFisico < 0)
            {
                Dialogos.Advertencia(
                    "No se detecto ninguna microSD conectada. Conecta una microSD e intentalo de nuevo.",
                    "Sin microSD");
                return;
            }

            string letraSD = sd.Letra;

            _pantallaCarga.Mostrar($"Instalando paquete en {letraSD}");
            try
            {
                int total = PipelineFsHelpers.ContarArchivos(carpetaOrigen);
                int copiados = 0;
                var reportador = _pantallaCarga.ObtenerReportador();

                await Task.Run(() =>
                {
                    PipelineFsHelpers.CopiarDirectorio(carpetaOrigen, letraSD, rutaArchivo =>
                    {
                        copiados++;
                        double pct = total > 0 ? copiados * 100.0 / total : 100.0;
                        reportador.Report(new EstadoProgreso
                        {
                            Porcentaje = pct,
                            TareaActual = $"Copiando en SD: {Path.GetFileName(rutaArchivo)}  ({copiados}/{total})",
                            PasoActual = 3
                        });
                    });
                });

                await Task.Delay(500);
                await ActualizarListaUnidadesAsync();

                _pantallaCarga.Ocultar();
                Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                Dialogos.Info(
                    $"El paquete de Atmosphere se instalo correctamente en {letraSD}.",
                    "Instalacion completada");
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error al instalar el paquete:\n\n{ex.Message}", "Fallo");
            }
        }
    }
}
