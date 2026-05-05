using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    /// <summary>
    /// Definición completa de un módulo del catálogo (CFW, bootloader, app homebrew, etc.).
    /// Mezcla datos venidos del JSON (identidad, versiones, firmas, pipeline) con
    /// estado en tiempo de ejecución (caché local, instalación en SD, progreso).
    /// </summary>
    public class ModuloConfig : INotifyPropertyChanged
    {
        // ?? Identidad ????????????????????????????????????????????????????
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string BannerUrl { get; set; } = string.Empty;
        public List<string> ScreenshotsUrl { get; set; } = new();
        public string UrlOficial { get; set; } = string.Empty;

        /// <summary>
        /// Etiquetas que identifican al módulo.
        /// Reemplaza los antiguos campos "Categoria" y "Mundo".
        /// Ejemplo: ["bootloader", "cfw"]
        /// </summary>
        public List<string> Etiquetas { get; set; } = new();

        // ?? Versiones ????????????????????????????????????????????????????
        public List<ModuloVersion> Versiones { get; set; } = new();

        // ?? Relaciones
        /// <summary>
        /// IDs o etiquetas de módulos complementos en el modo asistido.
        /// Ejemplo: hekate ? ["payload", "hekate.ipl.ini"]
        /// </summary>
        public List<string> Complementos { get; set; } = new();

        /// <summary>
        /// Tipo de editor visual por subcategoría. Valores: "selector" | "conversor_imagen"
        /// Ejemplo: { "background": "conversor_imagen", "payload": "selector" }
        /// </summary>
        public Dictionary<string, string> TiposEditorComplemento { get; set; } = new();

        /// <summary>IDs de módulos que deben instalarse antes que este.</summary>
        public List<string> Dependencias { get; set; } = new();

        /// <summary>IDs de módulos incompatibles con este.</summary>
        public List<string> IncompatibleCon { get; set; } = new();

        /// <summary>
        /// Hallazgos de la última validación de contenido. Calculado en runtime.
        /// Vacío si no hay reglas o si el archivo está correctamente configurado.
        /// </summary>
        public List<HallazgoConfig> HallazgosConfig { get; set; } = new();

        /// <summary>True si el módulo es de tipo configuración (ini, txt, hosts, etc.).</summary>
        public bool EsConfiguracion =>
            Etiquetas?.Exists(e => string.Equals(e, "configuracion", StringComparison.OrdinalIgnoreCase)) == true;

        /// <summary>
        /// Versión del módulo seleccionada en runtime según las dependencias instaladas.
        /// Calculada en ActualizarEstadosInstalados. Define qué pipeline y reglas se aplican.
        /// No proviene del JSON.
        /// </summary>
        public ModuloVersion? VersionCompatibleSeleccionada { get; set; }

        /// <summary>
        /// Contenido completo del hekate_ipl.ini que se escribirá en la SD al instalar.
        /// El campo icon= de cada sección debe coincidir con la ruta del BMP generado.
        /// Ejemplo: "[EMUMMC]\nemummcforce=1\nicon=bootloader/res/emummc.bmp\n{}"
        /// </summary>
        public string? HekateLaunchConfig { get; set; }

        // ?? Instalación ??????????????????????????????????????????????????
        public string GitHubRepo { get; set; } = string.Empty;
        public List<FirmaDeteccion> FirmasDeteccion { get; set; } = new();
        public List<string> RutasDesinstalacion { get; set; } = new();

        // ?? Estado en tiempo de ejecución (no viene del JSON) ????????????
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
                // Si no hay SD, la AccionRapida depende del cache
                if (EstadoSd == EstadoSdModulo.NoInstalado)
                {
                    AccionRapida = TieneCache
                        ? AccionRapidaModulo.EliminarCache
                        : AccionRapidaModulo.DescargarCache;
                }
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

        private bool _estaSeleccionadoAsistido;
        public bool EstaSeleccionadoAsistido
        {
            get => _estaSeleccionadoAsistido;
            set { if (_estaSeleccionadoAsistido == value) return; _estaSeleccionadoAsistido = value; OnPropertyChanged(); }
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

        // ?? Progreso de instalación en tarjeta ???????????????????????????
        private bool _estaInstalando;
        public bool EstaInstalando
        {
            get => _estaInstalando;
            set { if (_estaInstalando == value) return; _estaInstalando = value; OnPropertyChanged(); }
        }

        private double _progresoInstalacion;
        public double ProgresoInstalacion
        {
            get => _progresoInstalacion;
            set { if (_progresoInstalacion == value) return; _progresoInstalacion = value; OnPropertyChanged(); }
        }

        private string _textoProgreso = string.Empty;
        public string TextoProgreso
        {
            get => _textoProgreso;
            set { if (_textoProgreso == value) return; _textoProgreso = value; OnPropertyChanged(); }
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

        // ?? Propiedades calculadas ????????????????????????????????????????
        public bool   TieneArchivosFaltantes => ArchivosFaltantesDeteccion.Count > 0;
        public bool   TieneCache             => EstadoCache != EstadoCacheModulo.NoDescargado;
        public bool   EstaEnCache
        {
            get => EstadoCache != EstadoCacheModulo.NoDescargado;
            set => EstadoCache = value ? EstadoCacheModulo.EnCache : EstadoCacheModulo.NoDescargado;
        }
        public bool   EstaInstaladoEnSd      => EstadoSd == EstadoSdModulo.Instalado;
        public bool   TieneActualizacion     => EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion;
        public bool   TieneComplementos      => Complementos.Count > 0;
        public bool   MostrarAccionRapida    => AccionRapida != AccionRapidaModulo.Ninguna;
        public double CacheOpacity           => TieneCache ? 1.0 : 0.15;
        public string MensajeCacheActual     => TieneCache ? "En caché local" : "No descargado";

        public string IconoCacheActual => string.IsNullOrWhiteSpace(ConfiguracionRemota.Ui?.IconoCacheUrl)
            ? string.Empty
            : ConfiguracionRemota.Ui.IconoCacheUrl;

        public string TextoAccionRapida => AccionRapida switch
        {
            AccionRapidaModulo.Instalar        => "INSTALAR",
            AccionRapidaModulo.Reinstalar      => "REINSTALAR",
            AccionRapidaModulo.Actualizar      => "ACTUALIZAR",
            AccionRapidaModulo.Eliminar        => "ELIMINAR",
            AccionRapidaModulo.DescargarCache  => "DESCARGAR EN PC",
            AccionRapidaModulo.EliminarCache   => "ELIMINAR CACHE",
            _                                 => string.Empty
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
