using NX_Suite.Core;
using NX_Suite.Models;
using System;
using System.Collections.Generic;

namespace NX_Suite.Core.Pipeline
{
    /// <summary>
    /// Estado compartido por todos los pasos de un mismo pipeline.
    /// Se construye una sola vez al principio de
    /// <c>ReglasLogic.EjecutarPipelineAsync</c> y se pasa por referencia a cada
    /// <see cref="IPasoPipeline.EjecutarAsync"/>.
    /// </summary>
    public class ContextoPipeline
    {
        /// <summary>Letra raíz de la SD (ej. "E:\\"). Es la base para resolver rutas relativas del JSON.</summary>
        public string LetraSD { get; init; } = string.Empty;

        /// <summary>Carpeta local donde se guardan los ZIPs descargados (caché).</summary>
        public string RutaCacheZips { get; init; } = string.Empty;

        /// <summary>Carpeta local donde se extraen los ZIPs antes de copiar a la SD.</summary>
        public string RutaCacheExtraccion { get; init; } = string.Empty;

        /// <summary>Carpeta local de respaldos (operaciones RESPALDARAPC / RESTAURARDEPC).</summary>
        public string RutaBackups { get; init; } = string.Empty;

        /// <summary>
        /// Versión del módulo que se está instalando (ej. "1.8.1").
        /// Usada por <see cref="Pipeline.Pasos.PasoDescargar"/> para invalidar
        /// archivos en caché que pertenecen a una versión anterior.
        /// </summary>
        public string VersionModulo { get; init; } = string.Empty;

        /// <summary>Motor de descargas reutilizable (mantiene HttpClient y reporta progreso).</summary>
        public DownloadLogic MotorDescarga { get; init; } = null!;

        /// <summary>Motor de extracción de ZIPs reutilizable.</summary>
        public ZipLogic MotorZip { get; init; } = null!;

        /// <summary>
        /// Reporte de progreso global del pipeline. Cada paso puede emitir reportes
        /// adicionales para sub-progresos (ej. % de descarga). Puede ser null.
        /// ReglasLogic lo reemplaza antes de cada paso con un wrapper que mapea el
        /// progreso interno (0-100 %) al rango global del paso.
        /// </summary>
        public IProgress<EstadoProgreso>? Progreso { get; set; }

        /// <summary>
        /// Validador de assets de GitHub (hash remoto vs. caché local).
        /// Puede ser null si no se configuró token y se instanció sin validador.
        /// El uso es opcional y no forzoso: si es null o devuelve
        /// <see cref="ResultadoValidacion.NoDisponible"/> la instalación continúa
        /// desde caché sin interrumpirse.
        /// </summary>
        public GitHubAssetValidator? ValidadorAsset { get; init; }

        /// <summary>
        /// Conjunto de nombres de archivo (ZIPs) que fueron descargados
        /// efectivamente en esta sesión (no reutilizados desde caché).
        /// <see cref="Pipeline.Pasos.PasoDescargar"/> lo rellena tras cada
        /// descarga real. <see cref="Pipeline.Pasos.PasoExtraer"/> lo consulta
        /// para forzar re-extracción aunque el sidecar de versión coincida,
        /// evitando reusar contenido extraído obsoleto cuando el upstream
        /// publica un ZIP nuevo con el mismo número de versión.
        /// </summary>
        public HashSet<string> ZipsDescargadosEnEstaSesion { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
