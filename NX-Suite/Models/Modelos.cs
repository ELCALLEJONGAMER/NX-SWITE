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
        private string _iconoVolverUrl     = string.Empty;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class GistData
    {
        public ConfiguracionUI ConfiguracionUI { get; set; } = new();
        public BrandingConfig GlobalBranding { get; set; } = new();
        public List<MundoMenuConfig> MundosMenu { get; set; } = new();
        public List<FiltroMandoConfig> FiltrosCentroMando { get; set; } = new();
        public List<NodoDiagramaConfig> DiagramaNodos { get; set; } = new();
        public List<ModuloConfig> Modulos { get; set; } = new();
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