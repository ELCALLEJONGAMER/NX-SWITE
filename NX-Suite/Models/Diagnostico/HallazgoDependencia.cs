using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Representa una dependencia rota agrupada por CAUSA PRINCIPAL: un unico
    /// modulo (ej. Hekate) que esta desactualizado/incompleto/faltante y que
    /// afecta a varios modulos instalados que dependen de el.
    /// Generado por el panel de Diagnostico Rapido SD al cruzar modulos instalados
    /// con el resultado de AnalizadorDependencias, agrupando por dependencia raiz.
    /// </summary>
    public class HallazgoDependencia
    {
        /// <summary>Modulo que origina el problema (la dependencia rota en si).</summary>
        public ModuloConfig ModuloCausante { get; init; } = null!;

        /// <summary>Estado de la dependencia causante (NoInstalada | Parcial | Desactualizada).</summary>
        public EstadoDependencia Estado { get; init; }

        /// <summary>Modulos instalados que requieren <see cref="ModuloCausante"/> y se ven afectados.</summary>
        public List<ModuloConfig> ModulosAfectados { get; init; } = new();

        /// <summary>Titulo de la tarjeta, ej. "HEKATE DESACTUALIZADA".</summary>
        public string TituloCausa => $"{ModuloCausante.Nombre.ToUpperInvariant()} {SufijoEstado}";

        private string SufijoEstado => Estado switch
        {
            EstadoDependencia.NoInstalada => "NO INSTALADA",
            EstadoDependencia.Parcial => "INSTALACION INCOMPLETA",
            EstadoDependencia.Desactualizada => "DESACTUALIZADA",
            _ => string.Empty
        };

        /// <summary>Descripcion explicativa de la causa raiz.</summary>
        public string MensajeCausa => Estado switch
        {
            EstadoDependencia.NoInstalada =>
                $"{ModuloCausante.Nombre} no esta instalado y es requerido por varios modulos dependientes.",
            EstadoDependencia.Parcial =>
                $"La instalacion de {ModuloCausante.Nombre} esta incompleta y afecta a varios modulos dependientes.",
            EstadoDependencia.Desactualizada =>
                $"Tu version actual de {ModuloCausante.Nombre} esta desactualizada y afecta a varios modulos dependientes.",
            _ => string.Empty
        };
    }
}
