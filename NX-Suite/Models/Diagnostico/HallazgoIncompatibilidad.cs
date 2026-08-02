namespace NX_Swite.Models
{
    public class HallazgoIncompatibilidad
    {
        public ModuloConfig Modulo          { get; init; } = null!;
        public ModuloConfig ModuloConflicto { get; init; } = null!;
        public string TipoConflicto         { get; init; } = string.Empty;
        public string VersionInstalada      { get; init; } = string.Empty;
        public string VersionRequerida      { get; init; } = string.Empty;
        public string Mensaje               { get; init; } = string.Empty;

        /// <summary>
        /// Solo aplica a TipoConflicto == "firmware_real": indica si existe una version
        /// del propio modulo (Versiones[0]) compatible con el firmware/Atmos reales del
        /// sistema. Si es false, la unica accion posible es ELIMINAR.
        /// </summary>
        public bool HayVersionCompatible { get; init; } = true;

        /// <summary>
        /// Solo aplica a TipoConflicto == "firmware_real": indica si el origen del
        /// conflicto es el firmware de la consola ("Firmware") o Atmosphere ("Atmosphere").
        /// Permite reutilizar la misma tarjeta visual con textos/badges especificos.
        /// </summary>
        public string Origen { get; init; } = string.Empty;

        /// <summary>
        /// Etiqueta del badge que muestra el limite declarado por el modulo
        /// (ej. "Firmware máximo", "Atmosphere mínimo"). Solo aplica a firmware_real.
        /// </summary>
        public string EtiquetaLimite { get; init; } = string.Empty;

        /// <summary>
        /// Etiqueta del badge que muestra el valor real detectado en el sistema
        /// (ej. "Firmware detectado", "Atmosphere detectado"). Solo aplica a firmware_real.
        /// </summary>
        public string EtiquetaDetectado { get; init; } = string.Empty;

        public bool   EsIncompatibleTotal =>
            TipoConflicto == "incompatible" ||
            (TipoConflicto == "firmware_real" && !HayVersionCompatible);

        /// <summary>
        /// Modulo sobre el que hay que actuar para resolver el conflicto.
        /// version_maxima: actualizar Modulo (el que declara la restriccion, ej. mission_control).
        /// version_minima | incompatible: actuar sobre ModuloConflicto.
        /// firmware_real: siempre actuar sobre Modulo (el propio modulo desactualizado
        ///                frente al firmware/Atmos reales).
        /// </summary>
        public ModuloConfig ModuloAAccionar => TipoConflicto switch
        {
            "version_maxima" => Modulo,
            "firmware_real"  => Modulo,
            _                => ModuloConflicto
        };

        public string TextoAccion => EsIncompatibleTotal ? "ELIMINAR" : "ACTUALIZAR";
    }
}
