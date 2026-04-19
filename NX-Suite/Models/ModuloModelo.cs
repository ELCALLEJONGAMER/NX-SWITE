using NX_Suite.Core;
using NX_Suite.UI;
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

    public class ModuloConfig : INotifyPropertyChanged
    {
        // ── Identidad ────────────────────────────────────────────────────
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string UrlOficial { get; set; } = string.Empty;

        /// <summary>
        /// Etiquetas que identifican al módulo.
        /// Reemplaza los antiguos campos "Categoria" y "Mundo".
        /// Ejemplo: ["bootloader", "cfw"]
        /// </summary>
        public List<string> Etiquetas { get; set; } = new();

        // ── Versiones ────────────────────────────────────────────────────
        public List<ModuloVersion> Versiones { get; set; } = new();

        /// <summary>Rango de Firmware compatible. Ej: ">=12.0.0" | "1.9.0 - 1.11.1"</summary>
        public string Firmware { get; set; } = string.Empty;

        /// <summary>Rango de Atmosphere compatible. Ej: ">=1.6.0"</summary>
        public string Atmos { get; set; } = string.Empty;

        // ── Relaciones ───────────────────────────────────────────────────
        /// <summary>
        /// IDs o etiquetas de módulos complementos en el modo asistido.
        /// Ejemplo: hekate → ["payload", "hekate.ipl.ini"]
        /// </summary>
        public List<string> Complementos { get; set; } = new();

        /// <summary>IDs de módulos que deben instalarse antes que este.</summary>
        public List<string> Dependencias { get; set; } = new();

        /// <summary>IDs de módulos incompatibles con este.</summary>
        public List<string> IncompatibleCon { get; set; } = new();

        // ── Instalación ──────────────────────────────────────────────────
        public string GitHubRepo { get; set; } = string.Empty;
        public List<FirmaDeteccion> FirmasDeteccion { get; set; } = new();
        public List<string> RutasDesinstalacion { get; set; } = new();

        // ── Estado en tiempo de ejecución (no viene del JSON) ────────────
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
                    VersionInstalada is "No detectado" or "No instalado")
                    return false;

                return Versiones?.Count > 0 &&
                       !string.Equals(Versiones[0].Version, VersionInstalada,
                           StringComparison.OrdinalIgnoreCase);
            }
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

        // ── Propiedades calculadas ────────────────────────────────────────
        public bool   TieneArchivosFaltantes => ArchivosFaltantesDeteccion.Count > 0;
        public bool   TieneCache             => EstadoCache == EstadoCacheModulo.EnCache;
        public bool   EstaEnCache
        {
            get => EstadoCache == EstadoCacheModulo.EnCache;
            set => EstadoCache = value ? EstadoCacheModulo.EnCache : EstadoCacheModulo.NoDescargado;
        }
        public bool   EstaInstaladoEnSd      => EstadoSd == EstadoSdModulo.Instalado;
        public bool   TieneActualizacion     => EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion;
        public bool   TieneComplementos      => Complementos.Count > 0;
        public bool   MostrarAccionRapida    => AccionRapida != AccionRapidaModulo.Ninguna;
        public double CacheOpacity           => TieneCache ? 1.0 : 0.15;
        public string MensajeCacheActual     => TieneCache ? "En caché local" : "No descargado";

        public string IconoCacheActual => string.IsNullOrWhiteSpace(UIConfigService.Current?.IconoCacheUrl)
            ? string.Empty
            : UIConfigService.Current.IconoCacheUrl;

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
            EstadoSdModulo.NoInstalado           => "NO SD",
            EstadoSdModulo.ParcialmenteInstalado => "PARCIAL",
            EstadoSdModulo.Instalado             => "EN SD",
            _                                    => string.Empty
        };

        public string CacheEstadoTexto => EstadoCache switch
        {
            EstadoCacheModulo.NoDescargado => "No descargado",
            EstadoCacheModulo.ZipLocal     => "ZIP local",
            EstadoCacheModulo.Preparado    => "Preparado",
            _                              => string.Empty
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}