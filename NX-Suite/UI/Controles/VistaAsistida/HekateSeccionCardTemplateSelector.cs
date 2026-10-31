using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para las tarjetas de una sección Hekate:
    /// distingue entre módulos reales y el placeholder "añadir".
    /// </summary>
    public class HekateSeccionCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate  { get; set; }
        public DataTemplate? AgregarTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is HekateAgregarPlaceholder ? AgregarTemplate : ModuloTemplate;
    }
}
