using NX_Swite.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// ViewModel de una secci�n agrupada del panel de personalizaci�n Hekate.
    /// Expone <see cref="SlotsVisiblesCard"/> como mezcla de m�dulos
    /// seleccionados + el placeholder de "a�adir".
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
