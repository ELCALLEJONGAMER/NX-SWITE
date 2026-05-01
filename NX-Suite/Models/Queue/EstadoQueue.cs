namespace NX_Suite.Models
{
    /// <summary>Estado de un trabajo en la cola global de la aplicación.</summary>
    public enum EstadoQueue
    {
        Pendiente,
        EnProceso,
        Completado,
        Error,
        Cancelado
    }
}
