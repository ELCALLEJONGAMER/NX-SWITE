namespace NX_Suite.Models
{
    public class HallazgoIncompatibilidad
    {
        public ModuloConfig Modulo          { get; init; } = null!;
        public ModuloConfig ModuloConflicto { get; init; } = null!;
        public string TipoConflicto         { get; init; } = string.Empty;
        public string VersionInstalada      { get; init; } = string.Empty;
        public string VersionRequerida      { get; init; } = string.Empty;
        public string Mensaje               { get; init; } = string.Empty;

        public bool   EsIncompatibleTotal => TipoConflicto == "incompatible";

        /// <summary>
        /// Modulo sobre el que hay que actuar para resolver el conflicto.
        /// version_maxima: actualizar Modulo (el que declara la restriccion, ej. mission_control).
        /// version_minima | incompatible: actuar sobre ModuloConflicto.
        /// </summary>
        public ModuloConfig ModuloAAccionar =>
            TipoConflicto == "version_maxima" ? Modulo : ModuloConflicto;

        public string TextoAccion => EsIncompatibleTotal ? "ELIMINAR" : "ACTUALIZAR";
    }
}
