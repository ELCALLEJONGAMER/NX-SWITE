using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Suite.Models
{
    // ── Configuración y datos remotos ────────────────────────────────────

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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Configuración de sonidos remotos ────────────────────────────────

    /// <summary>
    /// URLs de los archivos .wav descargables desde el Gist.
    /// Cada campo puede estar vacío (sin sonido para ese evento).
    /// </summary>
    public class SonidosConfig
    {
        public string Intro      { get; set; } = string.Empty;
        public string Cerrar     { get; set; } = string.Empty;
        public string Click      { get; set; } = string.Empty;
        public string Hover      { get; set; } = string.Empty;
        public string Instalar   { get; set; } = string.Empty;
        public string Exito      { get; set; } = string.Empty;
        public string Error      { get; set; } = string.Empty;
        public string Navegacion { get; set; } = string.Empty;
    }

    public class GistData
    {
        public ConfiguracionUI         ConfiguracionUI      { get; set; } = new();
        public NyxConfigColors         NyxConfigColors      { get; set; } = new();
        public BrandingConfig          GlobalBranding       { get; set; } = new();
        public SonidosConfig         Sonidos              { get; set; } = new();
        public List<MundoMenuConfig> MundosMenu           { get; set; } = new();
        public List<FiltroMandoConfig> FiltrosCentroMando { get; set; } = new();
        public List<NodoDiagramaConfig> DiagramaNodos     { get; set; } = new();
        public List<ModuloConfig>    Modulos              { get; set; } = new();
        public List<TemaConfig>      Temas                { get; set; } = new();
    }

    public class BrandingConfig
    {
        public string NombrePrograma { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string ColorAcentoGlobal { get; set; } = "#00D2FF";
        public string BannerPorDefectoUrl { get; set; } = string.Empty;
    }

    // ── Filtros del panel lateral ─────────────────────────────────────────

    public class FiltroMandoConfig
    {
        public string Titulo { get; set; } = "Filtro";
        public string Nombre { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public List<string> Mundos { get; set; } = new();
    }

    // ── Nodo visual del asistente ─────────────────────────────────────────

    /// <summary>
    /// Define un slot visual en el modo asistido.
    /// Solo describe estructura de pantalla; la lógica de módulos vive en ModuloConfig.
    /// </summary>
    public class NodoDiagramaConfig
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Tipo de slot: "nucleo" | "complemento".</summary>
        public string Tipo { get; set; } = "nucleo";

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";

        /// <summary>Si true, el usuario debe seleccionar un módulo en este slot.</summary>
        public bool EsObligatorio { get; set; }

        /// <summary>
        /// Etiquetas que filtran qué módulos aparecen al pulsar "+" en este slot.
        /// </summary>
        public List<string> EtiquetasFiltro { get; set; } = new();
    }

    

    // ── Progreso e información de panel ──────────────────────────────────

    public class EstadoProgreso
    {
        public double Porcentaje { get; set; }
        public string TareaActual { get; set; } = string.Empty;
        public int PasoActual { get; set; }
    }

    public class InfoPanelDerecho
    {
        public string Capacidad { get; set; } = "--";
        public string Formato { get; set; } = "--";
        public string VersionAtmos { get; set; } = "Desconocido";
        public string Serial { get; set; } = "N/A";
    }

    // ── Enums de estado ───────────────────────────────────────────────────

    public enum EstadoCacheModulo
    {
        NoDescargado,
        ZipLocal,
        Preparado,
        EnCache
    }

    public enum EstadoSdModulo
    {
        NoInstalado,
        ParcialmenteInstalado,
        Instalado
    }

    public enum EstadoActualizacionModulo
    {
        SinCambios,
        NuevaVersion,
        Incompatible
    }

    public enum AccionRapidaModulo
    {
        Ninguna,
        Instalar,
        Reinstalar,
        Actualizar,
        Eliminar
    }
}