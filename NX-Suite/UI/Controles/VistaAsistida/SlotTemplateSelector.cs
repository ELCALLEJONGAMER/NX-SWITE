using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para los slots de una subcategor�a en VistaAsistida:
    /// usa <see cref="ModuloTemplate"/> para m�dulos reales y
    /// <see cref="VacioTemplate"/> para los placeholders de "a�adir".
    /// </summary>
    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate { get; set; }
        public DataTemplate? VacioTemplate  { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? VacioTemplate : ModuloTemplate;
    }
}
