using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NX_Suite.Models
{
    /// <summary>
    /// Trabajo individual de la cola global (instalación, formateo, etc.).
    /// Notifica cambios de estado/progreso a la UI vía INotifyPropertyChanged
    /// y permite cancelación cooperativa con su propio CancellationTokenSource.
    /// </summary>
    public class ItemQueue : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Titulo { get; init; } = string.Empty;

        private EstadoQueue _estado = EstadoQueue.Pendiente;
        public EstadoQueue Estado
        {
            get => _estado;
            set
            {
                if (_estado == value) return;
                _estado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EsActivo));
                OnPropertyChanged(nameof(EstaCompletado));
                OnPropertyChanged(nameof(TextoEstado));
            }
        }

        private double _progreso;
        public double Progreso
        {
            get => _progreso;
            set { if (Math.Abs(_progreso - value) < 0.05) return; _progreso = value; OnPropertyChanged(); }
        }

        private string _mensajeEstado = string.Empty;
        public string MensajeEstado
        {
            get => _mensajeEstado;
            set { if (_mensajeEstado == value) return; _mensajeEstado = value; OnPropertyChanged(); }
        }

        public CancellationTokenSource CancellationSource { get; } = new();
        public CancellationToken Token => CancellationSource.Token;

        public bool EsActivo      => Estado == EstadoQueue.Pendiente || Estado == EstadoQueue.EnProceso;
        public bool EstaCompletado => !EsActivo;

        public string TextoEstado => Estado switch
        {
            EstadoQueue.Pendiente  => "En cola",
            EstadoQueue.EnProceso  => "En proceso",
            EstadoQueue.Completado => "Completado",
            EstadoQueue.Error      => "Error",
            EstadoQueue.Cancelado  => "Cancelado",
            _                      => string.Empty
        };

        public void Cancelar()
        {
            if (!CancellationSource.IsCancellationRequested)
                CancellationSource.Cancel();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
