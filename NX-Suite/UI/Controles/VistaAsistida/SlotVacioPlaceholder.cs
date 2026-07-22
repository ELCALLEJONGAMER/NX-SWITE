namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Marcador de "slot vac�o" en una subcategor�a del modo asistido.
    /// Se renderiza como tarjeta "+" para que el usuario a�ada un m�dulo.
    /// </summary>
    public class SlotVacioPlaceholder
    {
        public SubcategoriaVM Subcategoria { get; }
        public SlotVacioPlaceholder(SubcategoriaVM sub) { Subcategoria = sub; }
    }
}
