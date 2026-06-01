using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX_Suite.Core.Configuracion
{
    /// <summary>
    /// Preferencias editables por el usuario que se persisten en
    /// <c>%AppData%\NX-Suite\preferencias.json</c>.
    ///
    /// ? REGLAS DE COMPATIBILIDAD:
    ///   • Nunca eliminar propiedades existentes — marcarlas [Obsolete] si ya no se usan.
    ///   • Las nuevas propiedades DEBEN tener un valor por defecto para no romper
    ///     archivos guardados con versiones anteriores.
    ///   • La deserialización usa <see cref="JsonIgnoreCondition.WhenWritingDefault"/>
    ///     + <see cref="JsonNumberHandling.AllowReadingFromString"/> para máxima tolerancia.
    ///
    /// Si se agrega una sección nueva, agregar también la propiedad aquí y
    /// actualizar CODEBASE_INDEX.md.
    /// </summary>
    public sealed class PreferenciasUsuario
    {
        // ?? Versión del esquema ??????????????????????????????????????????????
        // Incrementar solo si se hace un cambio INCOMPATIBLE (migración manual).
        public int SchemaVersion { get; set; } = 1;

        // ?? Sonido ??????????????????????????????????????????????????????????
        public SeccionSonido Sonido { get; set; } = new();

        // ?? Limpieza de Micro SD ?????????????????????????????????????????????
        public SeccionLimpiezaSD LimpiezaSD { get; set; } = new();
    }

    /// <summary>
    /// Ajustes de la sección Limpieza de Micro SD.
    /// Define qué carpetas y archivos de primer nivel NO se borran al limpiar la SD.
    /// </summary>
    public sealed class SeccionLimpiezaSD
    {
        /// <summary>
        /// Nombres (sin ruta) de carpetas y archivos de primer nivel de la SD
        /// que se protegerán del borrado. La comparación es case-insensitive en Windows.
        /// </summary>
        public List<string> EntradasProtegidas { get; set; } =
            new() { "emuMMC", "Nintendo", "roms" };
    }

    /// <summary>
    /// Ajustes de la sección Sonido.
    /// Todos los campos tienen valores por defecto seguros (todo activado).
    /// </summary>
    public sealed class SeccionSonido
    {
        /// <summary>Master switch: si es false, ningún sonido se reproduce.</summary>
        public bool Activo     { get; set; } = true;

        /// <summary>Sonido de arranque de la aplicación.</summary>
        public bool Intro      { get; set; } = true;

        /// <summary>Sonido al cerrar la aplicación.</summary>
        public bool Cerrar     { get; set; } = true;

        /// <summary>Sonido al hacer clic en botones.</summary>
        public bool Click      { get; set; } = true;

        /// <summary>Sonido al pasar el mouse por encima de elementos.</summary>
        public bool Hover      { get; set; } = true;

        /// <summary>Sonido al iniciar una instalación.</summary>
        public bool Instalar   { get; set; } = true;

        /// <summary>Sonido al completar una instalación con éxito.</summary>
        public bool Exito      { get; set; } = true;

        /// <summary>Sonido al producirse un error.</summary>
        public bool Error      { get; set; } = true;

        /// <summary>Sonido al navegar entre secciones.</summary>
        public bool Navegacion { get; set; } = true;

        /// <summary>Volumen global (0.0 – 1.0).</summary>
        public double Volumen  { get; set; } = 0.8;
    }
}
