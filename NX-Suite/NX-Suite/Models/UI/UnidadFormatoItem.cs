using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    /// <summary>
    /// Item visual para la lista de unidades extraíbles que pueden ser
    /// formateadas. Soporta selección, estado de operación y mensajes de
    /// progreso para el overlay de formateo FAT32.
    /// </summary>
    public class UnidadFormatoItem : INotifyPropertyChanged
    {
        public string Letra        { get; set; } = "";   // ej. "H:\"
        public string Etiqueta     { get; set; } = "";
        public string Capacidad    { get; set; } = "";   // ej. "116 GB"
        public string FormatoActual{ get; set; } = "";   // ej. "FAT32" o "RAW"
        public int    DiscoFisico  { get; set; }

        private bool _seleccionada;
        public bool Seleccionada
        {
            get => _seleccionada;
            set { _seleccionada = value; OnPropertyChanged(); }
        }

        private string _estado = "Listo";
        public string Estado
        {
            get => _estado;
            set { _estado = value; OnPropertyChanged(); }
        }

        private double _progreso;
        public double Progreso
        {
            get => _progreso;
            set { _progreso = value; OnPropertyChanged(); }
        }

        private bool _enProceso;
        public bool EnProceso
        {
            get => _enProceso;
            set { _enProceso = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
