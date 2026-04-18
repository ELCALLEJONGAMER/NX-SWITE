using NX_Suite.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;

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
        public List<PasoPipeline> PipelineInstalacion { get; set; } = new();
        public List<PasoPipeline> PipelineDesinstalacion { get; set; } = new();
    }

    public class ModuloConfig : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Mundo { get; set; } = string.Empty;
        public List<string> Etiquetas { get; set; } = new();
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string UrlOficial { get; set; } = string.Empty;
        public List<string> Dependencias { get; set; } = new();
        public List<string> IncompatibleCon { get; set; } = new();
        public List<ModuloVersion> Versiones { get; set; } = new();
        public List<FirmaDeteccion> FirmasDeteccion { get; set; } = new();
        public List<string> RutasDesinstalacion { get; set; } = new();

        /// <summary>
        /// Pantallas de complementos que se muestran después de seleccionar este módulo
        /// en el flujo asistido. Si está vacío, se salta la pantalla de complementos.
        /// </summary>
        public List<SubcategoriaConfig> Subcategorias { get; set; } = new();

        /// <summary>
        /// true si este módulo tiene al menos una subcategoría de complementos definida.
        /// </summary>
        public bool TieneSubcategorias => Subcategorias != null && Subcategorias.Count > 0;

        private string _versionInstalada = "No detectado";
        public string VersionInstalada
        {
            get => _versionInstalada;
            set
            {
                if (_versionInstalada == value) return;
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
                    return false;

                return Versiones?.Count > 0 &&
                       !string.Equals(Versiones[0].Version, VersionInstalada, StringComparison.OrdinalIgnoreCase);
            }
        }

        public string AlertaSeguridad { get; set; } = string.Empty;
        public bool TieneUrlOficial => !string.IsNullOrEmpty(UrlOficial);

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
                OnPropertyChanged(nameof(MensajeCacheActual));
                OnPropertyChanged(nameof(EstaEnCache));
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
                OnPropertyChanged(nameof(TextoEstadoSd));
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
                OnPropertyChanged(nameof(TextoEstadoActualizacion));
                OnPropertyChanged(nameof(MostrarBadgeActualizacion));
                OnPropertyChanged(nameof(AccionRapida));
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
                OnPropertyChanged(nameof(MostrarAccionRapida));
                OnPropertyChanged(nameof(TextoAccionRapida));
            }
        }

        private string _rutaCacheZip = string.Empty;
        public string RutaCacheZip
        {
            get => _rutaCacheZip;
            set { if (_rutaCacheZip == value) return; _rutaCacheZip = value; OnPropertyChanged(); }
        }

        private string _rutaCacheCarpeta = string.Empty;
        public string RutaCacheCarpeta
        {
            get => _rutaCacheCarpeta;
            set { if (_rutaCacheCarpeta == value) return; _rutaCacheCarpeta = value; OnPropertyChanged(); }
        }

        private string _tooltipCache = string.Empty;
        public string TooltipCache
        {
            get => _tooltipCache;
            set { if (_tooltipCache == value) return; _tooltipCache = value; OnPropertyChanged(); }
        }

        private List<string> _archivosFaltantesDeteccion = new();
        public List<string> ArchivosFaltantesDeteccion
        {
            get => _archivosFaltantesDeteccion;
            set
            {
                if (_archivosFaltantesDeteccion == value) return;
                _archivosFaltantesDeteccion = value ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneArchivosFaltantes));
            }
        }

        public bool TieneArchivosFaltantes => ArchivosFaltantesDeteccion.Count > 0;

        public bool EstaEnCache
        {
            get => EstadoCache != EstadoCacheModulo.NoDescargado;
            set => EstadoCache = value ? EstadoCacheModulo.ZipLocal : EstadoCacheModulo.NoDescargado;
        }

        public bool TieneCache           => EstadoCache != EstadoCacheModulo.NoDescargado;
        public bool EstaInstaladoEnSd    => EstadoSd == EstadoSdModulo.Instalado;
        public bool TieneActualizacion   => EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion;

        public string IconoCacheActual => string.IsNullOrWhiteSpace(UIConfigService.Current?.IconoCacheUrl)
            ? string.Empty
            : UIConfigService.Current.IconoCacheUrl;

        public Brush ColorCategoriaBrush
        {
            get
            {
                var color = UIConfigService.Current?.ColorTextoCategoria ?? "#A0A0A0";
                return (Brush)new BrushConverter().ConvertFromString(color)!;
            }
        }

        public double CacheOpacity => EstadoCache switch
        {
            EstadoCacheModulo.NoDescargado => 0.18,
            EstadoCacheModulo.ZipLocal     => 0.75,
            EstadoCacheModulo.Preparado    => 1.0,
            _                              => 0.18
        };

        public bool   MostrarAccionRapida => AccionRapida != AccionRapidaModulo.Ninguna;

        public string TextoAccionRapida => AccionRapida switch
        {
            AccionRapidaModulo.Instalar   => "INSTALAR",
            AccionRapidaModulo.Reinstalar => "REINSTALAR",
            AccionRapidaModulo.Actualizar => "ACTUALIZAR",
            AccionRapidaModulo.Eliminar   => "ELIMINAR",
            _                             => string.Empty
        };

        public string TextoEstadoSd => EstadoSd switch
        {
            EstadoSdModulo.NoInstalado          => "NO SD",
            EstadoSdModulo.ParcialmenteInstalado => "PARCIAL",
            EstadoSdModulo.Instalado            => "EN SD",
            _                                   => string.Empty
        };

        public string TextoEstadoActualizacion => EstadoActualizacion switch
        {
            EstadoActualizacionModulo.NuevaVersion => "NUEVA ACT.",
            EstadoActualizacionModulo.Incompatible => "INCOMPAT.",
            _                                      => string.Empty
        };

        public bool MostrarBadgeActualizacion => EstadoActualizacion != EstadoActualizacionModulo.SinCambios;

        public bool EsHerramienta =>
            string.Equals(Mundo, "microsd", StringComparison.OrdinalIgnoreCase) &&
            (Categoria.Contains("Formato", StringComparison.OrdinalIgnoreCase) ||
             Categoria.Contains("Particion", StringComparison.OrdinalIgnoreCase));

        public string CacheEstadoTexto => EstadoCache switch
        {
            EstadoCacheModulo.NoDescargado => "No descargado",
            EstadoCacheModulo.ZipLocal     => "ZIP local",
            EstadoCacheModulo.Preparado    => "Preparado",
            _                              => string.Empty
        };

        public string MensajeCacheActual => CacheEstadoTexto;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FirmaDeteccion
    {
        public string Version { get; set; } = string.Empty;
        public List<ArchivoCritico> Archivos { get; set; } = new();
    }

    public class ArchivoCritico
    {
        public string Ruta { get; set; } = string.Empty;
        public string SHA256 { get; set; } = string.Empty;
    }
}