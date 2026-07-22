using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para las tarjetas de una secci�n Hekate:
    /// distingue entre m�dulos reales y el placeholder "a�adir".
    /// </summary>
    public class HekateSeccionCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate  { get; set; }
        public DataTemplate? AgregarTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is HekateAgregarPlaceholder ? AgregarTemplate : ModuloTemplate;
    }
}
