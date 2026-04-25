namespace NX_Suite.Models
{
    /// <summary>
    /// Entrada en la sección "Recomendados" del Gist.
    /// Permite al servidor fijar versiones exactas para garantizar compatibilidad
    /// (ej: bloquear atmosphere hasta que Mission Control soporte una nueva release).
    /// </summary>
    public class ModuloRecomendado
    {
        /// <summary>Debe coincidir con ModuloConfig.Id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Versión exacta a instalar. null = última disponible en Versiones[].
        /// Usar cuando necesitas congelar una versión por compatibilidad.
        /// Ejemplo: "1.7.1" para atmosphere cuando Mission Control aún no soporta 1.8.x
        /// </summary>
        public string? Version { get; set; }

        /// <summary>Posición en el pipeline de instalación (ascendente).</summary>
        public int Orden { get; set; }

        /// <summary>Si true, el proceso COMPLETO no puede continuar sin este módulo.</summary>
        public bool Obligatorio { get; set; } = true;

        /// <summary>Texto informativo visible al usuario durante el asistido completo.</summary>
        public string Nota { get; set; } = string.Empty;
    }
}
