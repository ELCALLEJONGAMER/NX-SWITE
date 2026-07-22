namespace NX_Swite.Models
{
    /// <summary>Estado de un trabajo en la cola global de la aplicaci�n.</summary>
    public enum EstadoQueue
    {
        Pendiente,
        EnProceso,
        Completado,
        Error,
        Cancelado
    }
}
