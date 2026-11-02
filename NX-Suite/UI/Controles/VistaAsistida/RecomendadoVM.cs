using NX_Swite.Models;
using System.Linq;
using System.Windows;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// ViewModel de un módulo recomendado en el panel ASISTIDO COMPLETO.
    /// Combina la definición del módulo (<see cref="Modulo"/>) con la entrada
    /// de configuración del Gist (<see cref="Config"/>) y expone propiedades
    /// listas para bindear (versión a instalar, badge, nota, visibilidades).
    /// </summary>
    public class RecomendadoVM : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private bool _seleccionado = true;
        /// <summary>Si el usuario mantiene el módulo en el paquete.</summary>
        public bool Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (_seleccionado == value) return;
                _seleccionado = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Seleccionado)));
                SeleccionCambiada?.Invoke(this, System.EventArgs.Empty);
            }
        }

        public event System.EventHandler? SeleccionCambiada;

        public string Descripcion => string.IsNullOrWhiteSpace(Modulo.Descripcion) ? Config.Nota ?? string.Empty : Modulo.Descripcion;

        public ModuloConfig      Modulo  { get; init; } = null!;
        public ModuloRecomendado Config  { get; init; } = null!;

        /// <summary>
        /// Versión que se instalará: la fijada en Config.Version o la primera disponible.
        /// </summary>
        public string VersionAInstalar =>
            Config.Version ?? Modulo.Versiones?.FirstOrDefault()?.Version ?? "—";

        public string EtiquetaVersion =>
            Config.Version != null ? $"v{Config.Version} fijada" : "última";

        public string Nota => Config.Nota;

        /// <summary>Badge de versión fijada visible si hay versión explícita en el JSON.</summary>
        public Visibility VersionFijadaVisible =>
            Config.Version != null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NotaVisible =>
            string.IsNullOrWhiteSpace(Config.Nota) ? Visibility.Collapsed : Visibility.Visible;
    }
}
