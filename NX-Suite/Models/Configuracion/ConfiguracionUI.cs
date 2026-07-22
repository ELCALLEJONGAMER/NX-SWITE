using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NX_Swite.Models
{
    /// <summary>
    /// Configuraci�n global de UI venida del Gist (URLs de iconos, colores y URLs de
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
        private string _iconoMicroSDUrl          = string.Empty;
        private string _iconoPaintUrl            = string.Empty;
        private string _iconoEjectUrl             = string.Empty;
        private string _iconoConfigUrl            = string.Empty;

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

        public string IconoMicroSDUrl
        {
            get => _iconoMicroSDUrl;
            set { _iconoMicroSDUrl = value; OnPropertyChanged(); }
        }

        public string IconoPaintUrl
        {
            get => _iconoPaintUrl;
            set { _iconoPaintUrl = value; OnPropertyChanged(); }
        }

        public string IconoEjectUrl
        {
            get => _iconoEjectUrl;
            set { _iconoEjectUrl = value; OnPropertyChanged(); }
        }

        public string IconoConfigUrl
        {
            get => _iconoConfigUrl;
            set { _iconoConfigUrl = value; OnPropertyChanged(); }
        }

        private string _iconoInfoUrl = string.Empty;
        public string IconoInfoUrl
        {
            get => _iconoInfoUrl;
            set { _iconoInfoUrl = value; OnPropertyChanged(); }
        }

        private string _iconoCarpetaUrl = string.Empty;
        public string IconoCarpetaUrl
        {
            get => _iconoCarpetaUrl;
            set { _iconoCarpetaUrl = value; OnPropertyChanged(); }
        }

        private string _iconoArchivoUrl = string.Empty;
        public string IconoArchivoUrl
        {
            get => _iconoArchivoUrl;
            set { _iconoArchivoUrl = value; OnPropertyChanged(); }
        }

        private string _iconoShieldUrl = string.Empty;
        public string IconoShieldUrl
        {
            get => _iconoShieldUrl;
            set { _iconoShieldUrl = value; OnPropertyChanged(); }
        }

        private string _iconoLogUrl = string.Empty;
        public string IconoLogUrl
        {
            get => _iconoLogUrl;
            set { _iconoLogUrl = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// URL del ZIP que contiene fat32format.exe.
        /// Se usa en el proceso de Asistido Completo para formatear la SD como FAT32.
        /// </summary>
        public string UrlFat32Format { get; set; } = string.Empty;

        private string _versionCompatible = string.Empty;
        /// <summary>
        /// Versi�n de firmware/CFW para la que est� pensado el m�todo asistido.
        /// Ejemplo: "21.2.0"
        /// </summary>
        public string VersionCompatible
        {
            get => _versionCompatible;
            set { _versionCompatible = value; OnPropertyChanged(); }
        }

        private string _iconoRp2040Url = string.Empty;
        /// <summary>URL del icono del chip RP2040/Picofly para la TopBar.</summary>
        [JsonPropertyName("icono_rp2040_url")]
        public string IconoRp2040Url
        {
            get => _iconoRp2040Url;
            set { _iconoRp2040Url = value; OnPropertyChanged(); }
        }

        /// <summary>URL directa de descarga del firmware .uf2 para Picofly.</summary>
        [JsonPropertyName("url_firmware_rp2040")]
        public string UrlFirmwareRp2040 { get; set; } = string.Empty;

        /// <summary>Versi�n del firmware publicada en el Gist (para comparar con la instalada).</summary>
        [JsonPropertyName("version_firmware_rp2040")]
        public string VersionFirmwareRp2040 { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
