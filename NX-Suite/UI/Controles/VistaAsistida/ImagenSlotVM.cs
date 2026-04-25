using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Slot de selección de imagen (ej: fondo NYX, logo Hekate). Mantiene la
    /// vista previa actual y notifica si hay imagen cargada para mostrar/ocultar
    /// el placeholder en la UI.
    /// </summary>
    public class ImagenSlotVM : INotifyPropertyChanged
    {
        public string Etiqueta   { get; init; } = string.Empty;
        public string Titulo     { get; init; } = string.Empty;
        public string Resolucion { get; init; } = string.Empty;

        private BitmapSource? _preview;
        public BitmapSource? Preview
        {
            get => _preview;
            set { _preview = value; OnPropertyChanged(); OnPropertyChanged(nameof(TieneImagen)); }
        }
        public bool TieneImagen => _preview != null;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
