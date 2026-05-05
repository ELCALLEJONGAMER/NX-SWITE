using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Reglas de validación de contenido para un módulo de tipo "configuracion".
    /// Se declaran en el JSON del catálogo y se evalúan en tiempo de ejecución
    /// contra el archivo real en la SD mediante <c>ValidadorConfiguracion</c>.
    /// </summary>
    public class ReglasConfig
    {
        /// <summary>Ruta relativa del archivo en la SD. Ej: "bootloader/hekate_ipl.ini"</summary>
        public string RutaSD { get; set; } = string.Empty;

        /// <summary>Formato del archivo: "ini" | "txt" | "hosts"</summary>
        public string Formato { get; set; } = "ini";

        /// <summary>Lista de reglas individuales a evaluar.</summary>
        public List<ReglaConfig> Reglas { get; set; } = new();
    }

    /// <summary>
    /// Una regla individual de validación de contenido.
    /// Soporta validación por valor esperado o por valor prohibido.
    /// </summary>
    public class ReglaConfig
    {
        /// <summary>Sección del INI. Vacío para archivos planos (txt/hosts).</summary>
        public string Seccion { get; set; } = string.Empty;

        /// <summary>Clave a validar.</summary>
        public string Clave { get; set; } = string.Empty;

        /// <summary>
        /// Valor que la clave DEBE tener. Si el valor actual no coincide ? hallazgo.
        /// Mutuamente excluyente con <see cref="ValorProhibido"/>.
        /// </summary>
        public string? ValorEsperado { get; set; }

        /// <summary>
        /// Valor que la clave NO debe tener. Si coincide ? hallazgo.
        /// Mutuamente excluyente con <see cref="ValorEsperado"/>.
        /// </summary>
        public string? ValorProhibido { get; set; }

        /// <summary>"Critica" | "Recomendada". Solo los críticos degradan el estado a Parcial.</summary>
        public string Severidad { get; set; } = "Recomendada";

        /// <summary>Mensaje legible que explica por qué esta regla importa.</summary>
        public string Mensaje { get; set; } = string.Empty;
    }
}
