namespace NX_Suite.Models
{
    /// <summary>
    /// Resultado de una regla de validación que falló al analizar el archivo
    /// de configuración en la SD. Generado en runtime por <c>ValidadorConfiguracion</c>
    /// y almacenado en <see cref="ModuloConfig.HallazgosConfig"/>.
    /// </summary>
    public class HallazgoConfig
    {
        /// <summary>Sección del INI donde se encontró el problema. Vacío para archivos planos.</summary>
        public string Seccion { get; set; } = string.Empty;

        /// <summary>Clave que generó el hallazgo.</summary>
        public string Clave { get; set; } = string.Empty;

        /// <summary>Valor que tiene actualmente el archivo en la SD. Null si la clave no existe.</summary>
        public string? ValorActual { get; set; }

        /// <summary>Valor que debería tener la clave (para mostrar en UI).</summary>
        public string? ValorEsperado { get; set; }

        /// <summary>"Critica" | "Recomendada".</summary>
        public string Severidad { get; set; } = "Recomendada";

        /// <summary>Mensaje legible para mostrar en el panel de diagnóstico.</summary>
        public string Mensaje { get; set; } = string.Empty;

        public bool EsCritico =>
            string.Equals(Severidad, "Critica", System.StringComparison.OrdinalIgnoreCase);
    }
}
