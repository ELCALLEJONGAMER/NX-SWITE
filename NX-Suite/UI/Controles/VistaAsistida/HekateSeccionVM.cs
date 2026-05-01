using NX_Suite.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// ViewModel de una sección agrupada del panel de personalización Hekate.
    /// Expone <see cref="SlotsVisiblesCard"/> como mezcla de módulos
    /// seleccionados + el placeholder de "añadir".
    /// </summary>
    public class HekateSeccionVM : INotifyPropertyChanged
    {
        public string Etiqueta  { get; init; } = string.Empty;
        public string Titulo    { get; init; } = string.Empty;
        public string ColorNeon { get; init; } = "#00D2FF";

        public ObservableCollection<ModuloConfig> Seleccionados { get; } = new();

        public IEnumerable<object> SlotsVisiblesCard
        {
            get
            {
                foreach (var m in Seleccionados) yield return m;
                yield return new HekateAgregarPlaceholder(this);
            }
        }

        public HekateSeccionVM()
        {
            Seleccionados.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SlotsVisiblesCard));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
