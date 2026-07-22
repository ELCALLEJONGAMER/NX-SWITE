using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para las tarjetas de complementos de una
    /// subcategor�a: m�dulo real o placeholder de "a�adir".
    /// </summary>
    public class ComplementoCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate  { get; set; }
        public DataTemplate? AgregarTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? AgregarTemplate : ModuloTemplate;
    }
}
