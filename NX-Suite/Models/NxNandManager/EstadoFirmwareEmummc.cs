namespace NX_Swite.Models
{
    /// <summary>
    /// Estado de la detección de firmware de emuMMC mediante NxNandManager.
    /// Cubre tanto el ciclo de vida de la UI (NotStarted/Detecting) como los
    /// resultados finales devueltos por <c>NxNandManagerLogic</c>.
    /// </summary>
    public enum EstadoFirmwareEmummc
    {
        /// <summary>Aún no se ha iniciado ninguna detección para la unidad actual.</summary>
        NotStarted,

        /// <summary>Detección en curso (proceso CLI ejecutándose o herramienta descargándose).</summary>
        Detecting,

        /// <summary>Firmware detectado correctamente.</summary>
        Detected,

        /// <summary>La NAND fue leída (NAND type presente) pero no se pudo identificar el firmware.</summary>
        FirmwareNotDetected,

        /// <summary>No se detectó una partición/carpeta de emuMMC en la unidad.</summary>
        EmuMmcNotFound,

        /// <summary>No existe <c>switch/prod.keys</c> en la SD.</summary>
        KeysMissing,

        /// <summary>Las llaves existen pero NxNandManager las rechazó (formato inválido, etc.).</summary>
        KeysInvalid,

        /// <summary>El CLI se está descargando/validando (subestado informativo de Detecting).</summary>
        ToolDownloading,

        /// <summary>La descarga o validación (SHA-256) del CLI falló.</summary>
        ToolValidationFailed,

        /// <summary>Acceso denegado al disco físico (permisos insuficientes o unidad protegida).</summary>
        AccessDenied,

        /// <summary>La detección superó el timeout configurado y fue cancelada.</summary>
        TimedOut,

        /// <summary>Fallo genérico no cubierto por los estados anteriores.</summary>
        Failed
    }
}
