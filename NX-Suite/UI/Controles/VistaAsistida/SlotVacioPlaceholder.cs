namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Marcador de "slot vacío" en una subcategoría del modo asistido.
    /// Se renderiza como tarjeta "+" para que el usuario añada un módulo.
    /// </summary>
    public class SlotVacioPlaceholder
    {
        public SubcategoriaVM Subcategoria { get; }
        public SlotVacioPlaceholder(SubcategoriaVM sub) { Subcategoria = sub; }
    }
}
