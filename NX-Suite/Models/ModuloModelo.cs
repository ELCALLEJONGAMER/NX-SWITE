using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NX_Suite.Models
{
    public class PasoPipeline
    {
        public int Paso { get; set; }
        public string TipoAccion { get; set; } = string.Empty;
        public string MensajeUI { get; set; } = string.Empty;
        public JsonElement Parametros { get; set; }
    }

    public class ModuloVersion
    {
        public string Version { get; set; } = string.Empty;
        public List<PasoPipeline> PipelineInstalacion { get; set; } = new List<PasoPipeline>();
        public List<PasoPipeline> PipelineDesinstalacion { get; set; } = new List<PasoPipeline>();
    }

    public class ModuloConfig : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Mundo { get; set; } = string.Empty;
        public List<string> Etiquetas { get; set; } = new List<string>();
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string UrlOficial { get; set; } = string.Empty;
        public List<string> Dependencias { get; set; } = new List<string>();
        public List<string> IncompatibleCon { get; set; } = new List<string>();
        public List<ModuloVersion> Versiones { get; set; } = new List<ModuloVersion>();

        private string _versionInstalada = "No detectado";
        public string VersionInstalada
        {
            get => _versionInstalada;
            set
            {
                if (_versionInstalada == value)
                    return;

                _versionInstalada = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequiereUpdate));
            }
        }

        public bool RequiereUpdate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VersionInstalada) ||
                    VersionInstalada == "No detectado" ||
                    VersionInstalada == "No instalado")
                {
                    return false;
                }

                if (Versiones != null && Versiones.Count > 0)
                {
                    return Versiones[0].Version != VersionInstalada;
                }

                return false;
            }
        }

        public string AlertaSeguridad { get; set; } = string.Empty;
        public bool TieneUrlOficial => !string.IsNullOrEmpty(UrlOficial);
        public List<string> RutasDesinstalacion { get; set; } = new List<string>();
        public List<FirmaDeteccion> FirmasDeteccion { get; set; } = new List<FirmaDeteccion>();

        private bool _estaEnCache = false;
        public bool EstaEnCache
        {
            get => _estaEnCache;
            set
            {
                if (_estaEnCache == value)
                    return;

                _estaEnCache = value;
                OnPropertyChanged(nameof(EstaEnCache));
                OnPropertyChanged(nameof(IconoCacheActual));
                OnPropertyChanged(nameof(MensajeCacheActual));
            }
        }

        public string IconoCacheActual => MainWindow.UIGlobal?.IconoCacheUrl ?? string.Empty;

        public string MensajeCacheActual => EstaEnCache
            ? "Listo en PC (Instalación Rápida)"
            : "Disponible en la Nube (Requiere Descarga)";

        public string ColorCategoria => MainWindow.UIGlobal?.ColorTextoCategoria ?? "#A0A0A0";

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private EstadoCacheModulo _estadoCache = EstadoCacheModulo.NoDescargado;
        public EstadoCacheModulo EstadoCache
        {
            get => _estadoCache;
            set
            {
                if (_estadoCache == value) return;
                _estadoCache = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneCache));
                OnPropertyChanged(nameof(IconoCacheActual));
                OnPropertyChanged(nameof(CacheOpacity));
                OnPropertyChanged(nameof(TooltipCache));
                OnPropertyChanged(nameof(CacheEstadoTexto));
            }
        }

        private EstadoSdModulo _estadoSd = EstadoSdModulo.NoInstalado;
        public EstadoSdModulo EstadoSd
        {
            get => _estadoSd;
            set
            {
                if (_estadoSd == value) return;
                _estadoSd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstaInstaladoEnSd));
            }
        }

        private EstadoActualizacionModulo _estadoActualizacion = EstadoActualizacionModulo.SinCambios;
        public EstadoActualizacionModulo EstadoActualizacion
        {
            get => _estadoActualizacion;
            set
            {
                if (_estadoActualizacion == value) return;
                _estadoActualizacion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneActualizacion));
            }
        }

        private AccionRapidaModulo _accionRapida = AccionRapidaModulo.Ninguna;
        public AccionRapidaModulo AccionRapida
        {
            get => _accionRapida;
            set
            {
                if (_accionRapida == value) return;
                _accionRapida = value;
                OnPropertyChanged();
            }
        }

        private string _rutaCacheZip = string.Empty;
        public string RutaCacheZip
        {
            get => _rutaCacheZip;
            set
            {
                if (_rutaCacheZip == value) return;
                _rutaCacheZip = value;
                OnPropertyChanged();
            }
        }

        private string _rutaCacheCarpeta = string.Empty;
        public string RutaCacheCarpeta
        {
            get => _rutaCacheCarpeta;
            set
            {
                if (_rutaCacheCarpeta == value) return;
                _rutaCacheCarpeta = value;
                OnPropertyChanged();
            }
        }

        private string _tooltipCache = string.Empty;
        public string TooltipCache
        {
            get => _tooltipCache;
            set
            {
                if (_tooltipCache == value) return;
                _tooltipCache = value;
                OnPropertyChanged();
            }
        }

        public bool TieneCache => EstadoCache != EstadoCacheModulo.NoDescargado;
        public bool EstaInstaladoEnSd => EstadoSd == EstadoSdModulo.Instalado;
        public bool TieneActualizacion => EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion;

        public double CacheOpacity => EstadoCache switch
        {
            EstadoCacheModulo.NoDescargado => 0.25,
            EstadoCacheModulo.ZipLocal => 0.70,
            EstadoCacheModulo.Preparado => 1.0,
            _ => 0.25
        };

        public string CacheEstadoTexto => EstadoCache switch
        {
            EstadoCacheModulo.NoDescargado => "No descargado",
            EstadoCacheModulo.ZipLocal => "ZIP local",
            EstadoCacheModulo.Preparado => "Preparado",
            _ => string.Empty
        };
    }

    public class FirmaDeteccion
    {
        public string Version { get; set; } = string.Empty;
        public List<ArchivoCritico> Archivos { get; set; } = new List<ArchivoCritico>();
    }

    public class ArchivoCritico
    {
        public string Ruta { get; set; } = string.Empty;
        public string SHA256 { get; set; } = string.Empty;
    }

    
}