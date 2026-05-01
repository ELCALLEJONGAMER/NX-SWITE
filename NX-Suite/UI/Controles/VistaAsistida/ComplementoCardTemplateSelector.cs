using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para las tarjetas de complementos de una
    /// subcategoría: módulo real o placeholder de "añadir".
    /// </summary>
    public class ComplementoCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate  { get; set; }
        public DataTemplate? AgregarTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? AgregarTemplate : ModuloTemplate;
    }
}
