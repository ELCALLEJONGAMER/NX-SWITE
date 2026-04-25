using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    /// <summary>
    /// Configuración global de UI venida del Gist (URLs de iconos, colores y URLs de
    /// herramientas externas como fat32format).
    /// </summary>
    public class ConfiguracionUI : INotifyPropertyChanged
    {
        private string _iconoCacheUrl      = string.Empty;
        private string _colorTextoCategoria = "#A0A0A0";
        private string _iconoEliminarUrl   = string.Empty;
        private string _iconoAgregarUrl    = string.Empty;
        private string _iconoVolverUrl          = string.Empty;
        private string _iconoSiguienteUrl       = string.Empty;
        private string _iconoPaginaAnteriorUrl  = string.Empty;
        private string _iconoPaginaSiguienteUrl = string.Empty;
        private string _iconoZipUrl              = string.Empty;
        private string _iconoQueueUrl            = string.Empty;
        private string _iconoBellUrl             = string.Empty;
        private string _iconoMailUrl             = string.Empty;
        private string _iconoUpdateUrl           = string.Empty;

        public string IconoCacheUrl
        {
            get => _iconoCacheUrl;
            set { _iconoCacheUrl = value; OnPropertyChanged(); }
        }

        public string ColorTextoCategoria
        {
            get => _colorTextoCategoria;
            set { _colorTextoCategoria = value; OnPropertyChanged(); }
        }

        public string IconoEliminarUrl
        {
            get => _iconoEliminarUrl;
            set { _iconoEliminarUrl = value; OnPropertyChanged(); }
        }

        public string IconoAgregarUrl
        {
            get => _iconoAgregarUrl;
            set { _iconoAgregarUrl = value; OnPropertyChanged(); }
        }

        public string IconoVolverUrl
        {
            get => _iconoVolverUrl;
            set { _iconoVolverUrl = value; OnPropertyChanged(); }
        }

        public string IconoSiguienteUrl
        {
            get => _iconoSiguienteUrl;
            set { _iconoSiguienteUrl = value; OnPropertyChanged(); }
        }

        public string IconoPaginaAnteriorUrl
        {
            get => _iconoPaginaAnteriorUrl;
            set { _iconoPaginaAnteriorUrl = value; OnPropertyChanged(); }
        }

        public string IconoPaginaSiguienteUrl
        {
            get => _iconoPaginaSiguienteUrl;
            set { _iconoPaginaSiguienteUrl = value; OnPropertyChanged(); }
        }

        public string IconoZipUrl
        {
            get => _iconoZipUrl;
            set { _iconoZipUrl = value; OnPropertyChanged(); }
        }

        public string IconoQueueUrl
        {
            get => _iconoQueueUrl;
            set { _iconoQueueUrl = value; OnPropertyChanged(); }
        }

        public string IconoBellUrl
        {
            get => _iconoBellUrl;
            set { _iconoBellUrl = value; OnPropertyChanged(); }
        }

        public string IconoMailUrl
        {
            get => _iconoMailUrl;
            set { _iconoMailUrl = value; OnPropertyChanged(); }
        }

        public string IconoUpdateUrl
        {
            get => _iconoUpdateUrl;
            set { _iconoUpdateUrl = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// URL del ZIP que contiene fat32format.exe.
        /// Se usa en el proceso de Asistido Completo para formatear la SD como FAT32.
        /// </summary>
        public string UrlFat32Format { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
