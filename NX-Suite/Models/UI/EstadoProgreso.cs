namespace NX_Suite.Models
{
    /// <summary>
    /// DTO de progreso reportado por los pipelines a la UI (overlay de carga,
    /// barra global, etc.). PasoActual se usa para colorear los pasos numerados.
    /// </summary>
    public class EstadoProgreso
    {
        public double Porcentaje { get; set; }
        public string TareaActual { get; set; } = string.Empty;
        public int PasoActual { get; set; }
    }
}
