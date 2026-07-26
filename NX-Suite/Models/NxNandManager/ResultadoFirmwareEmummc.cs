namespace NX_Swite.Models
{
    /// <summary>
    /// Resultado de una detección de firmware de emuMMC vía NxNandManager.
    /// Devuelto por <see cref="Core.NxNandManagerLogic"/> y consumido por
    /// <c>MainWindow.SD.cs</c> para actualizar el panel derecho.
    /// </summary>
    public class ResultadoFirmwareEmummc
    {
        public EstadoFirmwareEmummc Estado { get; init; } = EstadoFirmwareEmummc.NotStarted;

        /// <summary>Versión de firmware detectada (solo si <see cref="Estado"/> es <c>Detected</c>).</summary>
        public string? Version { get; init; }

        /// <summary>Detalle técnico para el log; nunca se muestra directamente en la UI.</summary>
        public string? MensajeError { get; init; }

        /// <summary>Salida cruda combinada (stdout+stderr) del CLI, para diagnóstico.</summary>
        public string? SalidaCruda { get; init; }

        public static ResultadoFirmwareEmummc Ok(string version) =>
            new() { Estado = EstadoFirmwareEmummc.Detected, Version = version };

        public static ResultadoFirmwareEmummc De(EstadoFirmwareEmummc estado, string? mensajeError = null, string? salidaCruda = null) =>
            new() { Estado = estado, MensajeError = mensajeError, SalidaCruda = salidaCruda };
    }
}
