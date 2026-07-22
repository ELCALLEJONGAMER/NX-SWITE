namespace NX_Swite.Models
{
    /// <summary>
    /// Entrada en la secci�n "Recomendados" del Gist.
    /// Permite al servidor fijar versiones exactas para garantizar compatibilidad
    /// (ej: bloquear atmosphere hasta que Mission Control soporte una nueva release).
    /// </summary>
    public class ModuloRecomendado
    {
        /// <summary>Debe coincidir con ModuloConfig.Id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Versi�n exacta a instalar. null = �ltima disponible en Versiones[].
        /// Usar cuando necesitas congelar una versi�n por compatibilidad.
        /// Ejemplo: "1.7.1" para atmosphere cuando Mission Control a�n no soporta 1.8.x
        /// </summary>
        public string? Version { get; set; }

        /// <summary>Posici�n en el pipeline de instalaci�n (ascendente).</summary>
        public int Orden { get; set; }

        /// <summary>Si true, el proceso COMPLETO no puede continuar sin este m�dulo.</summary>
        public bool Obligatorio { get; set; } = true;

        /// <summary>Texto informativo visible al usuario durante el asistido completo.</summary>
        public string Nota { get; set; } = string.Empty;
    }
}
