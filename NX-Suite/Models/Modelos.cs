using System.Collections.Generic;

namespace NX_Suite.Models
{
    public class ConfiguracionUI
    {
        public string IconoCacheDescargado { get; set; } = string.Empty;
        public string IconoCacheNoDescargado { get; set; } = string.Empty;
        public string ColorTextoCategoria { get; set; } = "#A0A0A0";
    }

    public class GistData
    {
        public ConfiguracionUI ConfiguracionUI { get; set; } = new ConfiguracionUI();
        public List<ModuloConfig> Modulos { get; set; } = new List<ModuloConfig>();
        public BrandingConfig GlobalBranding { get; set; } = new BrandingConfig();
        public List<MundoMenuConfig> MundosMenu { get; set; } = new List<MundoMenuConfig>();
        public List<FiltroMandoConfig> FiltrosCentroMando { get; set; } = new List<FiltroMandoConfig>();
    }

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

    public class BrandingConfig
    {
        public string NombrePrograma { get; set; } = "NX-SUITE";
        public string LogoUrl { get; set; } = string.Empty;
        public string ColorAcentoGlobal { get; set; } = "#00D2FF";
        public string BannerPorDefectoUrl { get; set; } = string.Empty;
    }

    public class MundoMenuConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";
    }

    public class FiltroMandoConfig
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}