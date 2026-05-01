using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Selector de plantilla para los slots de una subcategoría en VistaAsistida:
    /// usa <see cref="ModuloTemplate"/> para módulos reales y
    /// <see cref="VacioTemplate"/> para los placeholders de "añadir".
    /// </summary>
    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate { get; set; }
        public DataTemplate? VacioTemplate  { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? VacioTemplate : ModuloTemplate;
    }
}
