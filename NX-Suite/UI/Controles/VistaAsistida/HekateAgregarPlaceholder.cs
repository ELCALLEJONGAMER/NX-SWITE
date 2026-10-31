namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Marcador de "añadir módulo" dentro de una sección agrupada del panel
    /// de personalización de Hekate.
    /// </summary>
    public class HekateAgregarPlaceholder
    {
        public HekateSeccionVM Seccion { get; }
        public HekateAgregarPlaceholder(HekateSeccionVM s) { Seccion = s; }
    }
}
