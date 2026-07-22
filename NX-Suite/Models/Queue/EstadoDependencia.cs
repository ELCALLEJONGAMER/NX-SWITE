namespace NX_Swite.Models
{
    /// <summary>Estado calculado de una dependencia declarada en un m�dulo del cat�logo.</summary>
    public enum EstadoDependencia
    {
        /// <summary>No est� en la SD en absoluto.</summary>
        NoInstalada,

        /// <summary>Est� en la SD pero la instalaci�n est� incompleta.</summary>
        Parcial,

        /// <summary>Est� instalada pero existe una versi�n m�s reciente.</summary>
        Desactualizada,

        /// <summary>Instalada y en su �ltima versi�n. No requiere acci�n.</summary>
        OK
    }
}
