using NX_Swite.Models;

namespace NX_Swite.Core.Configuracion
{
    /// <summary>
    /// Mundos de navegación de la app, definidos en código.
    /// <see cref="Visibles"/> es el menú lateral izquierdo (en orden).
    /// <see cref="Todos"/> incluye además las secciones a las que se navega
    /// desde las tarjetas del hub CFW (asistido, catálogo, personalización).
    /// Para añadir un mundo nuevo al menú: declararlo aquí y añadirlo a Visibles.
    /// </summary>
    internal static class MundosApp
    {
        public static readonly MundoMenuConfig Cfw = new()
        {
            Id        = "CFW",
            Nombre    = "CFW",
            IconoUrl  = "https://github.com/ELCALLEJONGAMER/NX-SWITE.ASSETS/blob/main/assets/ui/mundos/cfw.png?raw=true",
            ColorNeon = "#00CCFF",
            Tipo      = "cfw_hub",
        };

        public static readonly MundoMenuConfig Asistido = new()
        {
            Id            = "asistido",
            Nombre        = "ASISTIDO",
            Subtitulo     = "Crea tu propio paquete de archivos de forma asistida.",
            Tipo          = "asistido",
            ModoAsistente = "libre",
        };

        public static readonly MundoMenuConfig Catalogo = new()
        {
            Id        = "catalogo",
            Nombre    = "CATALOGO",
            Subtitulo = "Catalogo libre. Recomendado para conocedores del tema",
            Tipo      = "catalogo",
        };

        public static readonly MundoMenuConfig Personalizacion = new()
        {
            Id              = "personalizacion",
            Nombre          = "PERSONALIZACIÓN",
            Tipo            = "personalizacion",
            EtiquetasFiltro = new() { "hekatetheme", "launchicon", "background", "bootlogo", "temas" },
        };

        public static readonly MundoMenuConfig EmuMmc = new()
        {
            Id        = "emummc",
            Nombre    = "emuMMC",
            IconoUrl  = "https://img.icons8.com/?size=100&id=12171&format=png&color=00CCFF",
            ColorNeon = "#00CCFF",
            Tipo      = "emummc",
        };

        /// <summary>Menú lateral izquierdo, en orden.</summary>
        public static IReadOnlyList<MundoMenuConfig> Visibles { get; } = new[] { Cfw, EmuMmc };

        /// <summary>Todos los mundos conocidos (visibles y secciones del hub).</summary>
        public static IReadOnlyList<MundoMenuConfig> Todos { get; } = new[] { Cfw, EmuMmc, Asistido, Catalogo, Personalizacion };
    }
}
