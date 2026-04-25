using NX_Suite.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Item del panel multi-selección flotante: encapsula un módulo y un flag
    /// <see cref="Seleccionado"/> bindable a un checkbox.
    /// </summary>
    public class ItemMultiSeleccionVM : INotifyPropertyChanged
    {
        public ModuloConfig Modulo { get; init; } = null!;

        private bool _seleccionado;
        public bool Seleccionado
        {
            get => _seleccionado;
            set { _seleccionado = value; OnPropertyChanged(); }
        }

        public string Nombre   => Modulo.Nombre;
        public string IconoUrl => Modulo.IconoUrl;
        public string Version  => Modulo.Versiones?.Count > 0 ? $"v{Modulo.Versiones[0].Version}" : string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
