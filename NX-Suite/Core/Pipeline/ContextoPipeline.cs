using NX_Suite.Models;
using System;

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

        /// <summary>Motor de descargas reutilizable (mantiene HttpClient y reporta progreso).</summary>
        public DownloadLogic MotorDescarga { get; init; } = null!;

        /// <summary>Motor de extracción de ZIPs reutilizable.</summary>
        public ZipLogic MotorZip { get; init; } = null!;

        /// <summary>
        /// Reporte de progreso global del pipeline. Cada paso puede emitir reportes
        /// adicionales para sub-progresos (ej. % de descarga). Puede ser null.
        /// </summary>
        public IProgress<EstadoProgreso>? Progreso { get; init; }
    }
}
