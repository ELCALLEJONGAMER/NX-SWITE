namespace NX_Suite.Models
{
    /// <summary>Estado calculado de una dependencia declarada en un módulo del catálogo.</summary>
    public enum EstadoDependencia
    {
        /// <summary>No está en la SD en absoluto.</summary>
        NoInstalada,

        /// <summary>Está en la SD pero la instalación está incompleta.</summary>
        Parcial,

        /// <summary>Está instalada pero existe una versión más reciente.</summary>
        Desactualizada,

        /// <summary>Instalada y en su última versión. No requiere acción.</summary>
        OK
    }
}
