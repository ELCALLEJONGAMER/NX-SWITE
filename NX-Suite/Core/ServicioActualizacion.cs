using NX_Swite.Core.Configuracion;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Swite.Core
{
    /// <summary>
    /// Servicio observable que mantiene el estado de la actualizaci�n de la app.
    /// Accesible desde XAML mediante <c>Servicios.Actualizacion</c>.
    /// </summary>
    public class ServicioActualizacion : INotifyPropertyChanged
    {
        private bool   _hayActualizacion;
        private string _versionActual   = string.Empty;
        private string _versionRemota   = string.Empty;
        private string _urlDescarga     = string.Empty;
        private string _notasVersion    = string.Empty;

        public bool HayActualizacion
        {
            get => _hayActualizacion;
            private set { _hayActualizacion = value; OnPropertyChanged(); }
        }

        public string VersionActual
        {
            get => _versionActual;
            private set { _versionActual = value; OnPropertyChanged(); }
        }

        public string VersionRemota
        {
            get => _versionRemota;
            private set { _versionRemota = value; OnPropertyChanged(); }
        }

        public string UrlDescarga
        {
            get => _urlDescarga;
            private set { _urlDescarga = value; OnPropertyChanged(); }
        }

        public string NotasVersion
        {
            get => _notasVersion;
            private set { _notasVersion = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Eval�a si hay una versi�n m�s reciente disponible y actualiza las propiedades.
        /// </summary>
        public void Evaluar(string versionRemota, string urlDescarga, string notas)
        {
            VersionActual    = ConfiguracionLocal.VersionActual;
            VersionRemota    = versionRemota ?? string.Empty;
            UrlDescarga      = urlDescarga   ?? string.Empty;
            NotasVersion     = notas         ?? string.Empty;
            HayActualizacion = GestorActualizacion.EsVersionNueva(VersionActual, VersionRemota);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
