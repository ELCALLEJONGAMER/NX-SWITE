using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    /// <summary>
    /// Resultado del análisis de una dependencia individual.
    /// Expone propiedades de presentación (color, texto) para binding directo en XAML.
    /// </summary>
    public class ResultadoDependencia : INotifyPropertyChanged
    {
        public ModuloConfig Modulo { get; init; } = null!;
        public EstadoDependencia Estado { get; init; }

        private bool _seleccionada;
        public bool Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (_seleccionada == value) return;
                _seleccionada = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Solo las deps que no están OK pueden marcarse/desmarcarse.</summary>
        public bool EsAccionable => Estado != EstadoDependencia.OK;

        public string TextoEstado => Estado switch
        {
            EstadoDependencia.NoInstalada    => "No instalada — se instalará automáticamente",
            EstadoDependencia.Parcial        => "Instalación incompleta — se completará",
            EstadoDependencia.Desactualizada => "Desactualizada — se actualizará a la última versión",
            EstadoDependencia.OK             => "Instalada y actualizada",
            _                               => string.Empty
        };

        /// <summary>Color hexadecimal del estado, listo para usar con HexToBrushConverter.</summary>
        public string ColorHexEstado => Estado switch
        {
            EstadoDependencia.NoInstalada    => "#FF5555",
            EstadoDependencia.Parcial        => "#FFD54A",
            EstadoDependencia.Desactualizada => "#FFD54A",
            EstadoDependencia.OK             => "#40C057",
            _                               => "#505060"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
