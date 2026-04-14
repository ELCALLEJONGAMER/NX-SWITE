using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    // Usamos INotifyPropertyChanged para que la interfaz brille 
    // cuando el usuario seleccione una categoría (efecto neón).
    public class CategoriaModelo : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Nombre { get; set; }
        public string IconoUrl { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}