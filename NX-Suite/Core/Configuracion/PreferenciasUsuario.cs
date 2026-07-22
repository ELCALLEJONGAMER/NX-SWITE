using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX_Swite.Core.Configuracion
{
    /// <summary>
    /// Preferencias editables por el usuario que se persisten en
    /// <c>%AppData%\NX-Swite\preferencias.json</c>.
    ///
    /// ? REGLAS DE COMPATIBILIDAD:
    ///   ò Nunca eliminar propiedades existentes ù marcarlas [Obsolete] si ya no se usan.
    ///   ò Las nuevas propiedades DEBEN tener un valor por defecto para no romper
    ///     archivos guardados con versiones anteriores.
    ///   ò La deserializaci¾n usa <see cref="JsonIgnoreCondition.WhenWritingDefault"/>
    ///     + <see cref="JsonNumberHandling.AllowReadingFromString"/> para mßxima tolerancia.
    ///
    /// Si se agrega una secci¾n nueva, agregar tambiÚn la propiedad aquÝ y
    /// actualizar CODEBASE_INDEX.md.
    /// </summary>
    public sealed class PreferenciasUsuario
    {
        // ?? Versi¾n del esquema ??????????????????????????????????????????????
        // Incrementar solo si se hace un cambio INCOMPATIBLE (migraci¾n manual).
        public int SchemaVersion { get; set; } = 1;

        // ?? Sonido ??????????????????????????????????????????????????????????
        public SeccionSonido Sonido { get; set; } = new();

        // ?? Limpieza de Micro SD ?????????????????????????????????????????????
        public SeccionLimpiezaSD LimpiezaSD { get; set; } = new();
    }

    /// <summary>
    /// Ajustes de la secci¾n Limpieza de Micro SD.
    /// Define quÚ carpetas y archivos de primer nivel NO se borran al limpiar la SD.
    /// </summary>
    public sealed class SeccionLimpiezaSD
    {
        /// <summary>
        /// Nombres (sin ruta) de carpetas y archivos de primer nivel de la SD
        /// que se protegerßn del borrado. La comparaci¾n es case-insensitive en Windows.
        /// </summary>
        public List<string> EntradasProtegidas { get; set; } =
            new() { "emuMMC", "Nintendo", "roms" };
    }

    /// <summary>
    /// Ajustes de la secci¾n Sonido.
    /// Todos los campos tienen valores por defecto seguros (todo activado).
    /// </summary>
    public sealed class SeccionSonido
    {
        /// <summary>Master switch: si es false, ning·n sonido se reproduce.</summary>
        public bool Activo     { get; set; } = true;

        /// <summary>Sonido de arranque de la aplicaci¾n.</summary>
        public bool Intro      { get; set; } = true;

        /// <summary>Sonido al cerrar la aplicaci¾n.</summary>
        public bool Cerrar     { get; set; } = true;

        /// <summary>Sonido al hacer clic en botones.</summary>
        public bool Click      { get; set; } = true;

        /// <summary>Sonido al pasar el mouse por encima de elementos.</summary>
        public bool Hover      { get; set; } = true;

        /// <summary>Sonido al iniciar una instalaci¾n.</summary>
        public bool Instalar   { get; set; } = true;

        /// <summary>Sonido al completar una instalaci¾n con Úxito.</summary>
        public bool Exito      { get; set; } = true;

        /// <summary>Sonido al producirse un error.</summary>
        public bool Error      { get; set; } = true;

        /// <summary>Sonido al navegar entre secciones.</summary>
        public bool Navegacion { get; set; } = true;

        /// <summary>Volumen global (0.0 û 1.0).</summary>
        public double Volumen  { get; set; } = 0.8;
    }
}