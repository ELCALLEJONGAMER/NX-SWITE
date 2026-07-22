using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Reglas de validaci�n de contenido para un m�dulo de tipo "configuracion".
    /// Se declaran en el JSON del cat�logo y se eval�an en tiempo de ejecuci�n
    /// contra el archivo real en la SD mediante <c>ValidadorConfiguracion</c>.
    /// </summary>
    public class ReglasConfig
    {
        /// <summary>Ruta relativa del archivo en la SD. Ej: "bootloader/hekate_ipl.ini"</summary>
        public string RutaSD { get; set; } = string.Empty;

        /// <summary>Formato del archivo: "ini" | "txt" | "hosts" | "exacto"</summary>
        public string Formato { get; set; } = "ini";

        /// <summary>
        /// Solo para Formato="exacto". Contenido completo que debe tener el archivo en la SD.
        /// Si el archivo real no coincide (normalizado) se genera un HallazgoConfig cr�tico.
        /// Corresponde al mismo valor del campo Contenido del paso CREARTXT del pipeline.
        /// </summary>
        public string? ContenidoEsperado { get; set; }

        /// <summary>Lista de reglas individuales a evaluar. Vac�o cuando Formato="exacto".</summary>
        public List<ReglaConfig> Reglas { get; set; } = new();
    }

    /// <summary>
    /// Una regla individual de validaci�n de contenido.
    /// Soporta validaci�n por valor esperado o por valor prohibido.
    /// </summary>
    public class ReglaConfig
    {
        /// <summary>Secci�n del INI. Vac�o para archivos planos (txt/hosts).</summary>
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

        /// <summary>"Critica" | "Recomendada". Solo los cr�ticos degradan el estado a Parcial.</summary>
        public string Severidad { get; set; } = "Recomendada";

        /// <summary>Mensaje legible que explica por qu� esta regla importa.</summary>
        public string Mensaje { get; set; } = string.Empty;
    }
}
