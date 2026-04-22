using NX_Suite.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace NX_Suite.Core
{
    public class GestorQueue : INotifyPropertyChanged
    {
        public static GestorQueue Instancia { get; } = new();
        private GestorQueue() { }

        public ObservableCollection<ItemQueue> Items { get; } = new();

        public int  ContadorActivos => Items.Count(i => i.EsActivo);
        public bool TieneActivos    => ContadorActivos > 0;
        public bool TieneItems      => Items.Count > 0;

        // ?? Operaciones ??????????????????????????????????????????????????

        public ItemQueue AgregarItem(string titulo)
        {
            var item = new ItemQueue { Titulo = titulo, Estado = EstadoQueue.Pendiente };
            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Insert(0, item);
                NotificarCambios();
            });
            return item;
        }

        public void ActualizarItem(ItemQueue item, double progreso, string mensaje)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Estado       = EstadoQueue.EnProceso;
                item.Progreso     = progreso;
                item.MensajeEstado = mensaje;
                OnPropertyChanged(nameof(ContadorActivos));
                OnPropertyChanged(nameof(TieneActivos));
            });
        }

        public void CompletarItem(ItemQueue item)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Progreso      = 100;
                item.Estado        = EstadoQueue.Completado;
                item.MensajeEstado = "Completado";
                NotificarCambios();
            });
        }

        public void ErrorItem(ItemQueue item, string mensaje)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Estado        = EstadoQueue.Error;
                item.MensajeEstado = string.IsNullOrWhiteSpace(mensaje) ? "Error desconocido" : mensaje;
                NotificarCambios();
            });
        }

        public void CancelarItem(ItemQueue item)
        {
            item.Cancelar();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (item.EsActivo)
                {
                    item.Estado        = EstadoQueue.Cancelado;
                    item.MensajeEstado = "Cancelado por el usuario";
                }
                NotificarCambios();
            });
        }

        public void LimpiarCompletados()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var i in Items.Where(i => i.EstaCompletado).ToList())
                    Items.Remove(i);
                NotificarCambios();
            });
        }

        private void NotificarCambios()
        {
            OnPropertyChanged(nameof(ContadorActivos));
            OnPropertyChanged(nameof(TieneActivos));
            OnPropertyChanged(nameof(TieneItems));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
