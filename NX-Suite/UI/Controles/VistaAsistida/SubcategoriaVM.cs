using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// ViewModel de una subcategoría del modo asistido (ej: "Bootloader",
    /// "CFW"). Contiene la lista de módulos seleccionados y expone
    /// <see cref="SlotsVisibles"/> como mezcla de módulos + un placeholder
    /// para añadir más cuando aplique.
    /// </summary>
    public class SubcategoriaVM : INotifyPropertyChanged
    {
        public string Etiqueta              { get; }
        public string Nombre                { get; }
        public bool   PermiteMultiseleccion { get; init; } = true;

        public ObservableCollection<ModuloConfig> Seleccionados { get; } = new();

        public IEnumerable<object> SlotsVisibles
        {
            get
            {
                foreach (var m in Seleccionados) yield return m;
                if (PermiteMultiseleccion || Seleccionados.Count == 0)
                    yield return new SlotVacioPlaceholder(this);
            }
        }

        public SubcategoriaVM(string etiqueta, bool permiteMultiseleccion = true)
            : this(etiqueta, etiqueta, permiteMultiseleccion) { }

        public SubcategoriaVM(string etiqueta, string nombre, bool permiteMultiseleccion = true)
        {
            Etiqueta              = etiqueta ?? throw new ArgumentNullException(nameof(etiqueta));
            Nombre                = nombre ?? etiqueta;
            PermiteMultiseleccion = permiteMultiseleccion;
            Seleccionados.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SlotsVisibles));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
