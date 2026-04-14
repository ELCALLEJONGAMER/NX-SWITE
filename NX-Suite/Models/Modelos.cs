using System.Collections.Generic;

namespace NX_Suite.Models
{
    // 1. EL MOLDE PARA TU UI DINÁMICA
    public class ConfiguracionUI
    {
        public string IconoCacheDescargado { get; set; }
        public string IconoCacheNoDescargado { get; set; }
        public string ColorTextoCategoria { get; set; }
    }

    // 2. LA CAJA MAESTRA
    public class GistData
    {
        public ConfiguracionUI ConfiguracionUI { get; set; }
        // Eliminamos CategoriasConfig viejo, dejamos lo nuevo:
        public List<ModuloConfig> Modulos { get; set; } = new List<ModuloConfig>();
        public BrandingConfig GlobalBranding { get; set; } = new BrandingConfig();
        public List<MundoMenuConfig> MundosMenu { get; set; } = new List<MundoMenuConfig>();
        public List<FiltroMandoConfig> FiltrosCentroMando { get; set; } = new List<FiltroMandoConfig>();
    }

    // 3. EL MENSAJERO PARA EL OVERLAY GLOBAL DE CARGA
    public class EstadoProgreso
    {
        public double Porcentaje { get; set; }
        public string TareaActual { get; set; }
        public int PasoActual { get; set; }
    }

    // 4. INFO DEL PANEL DERECHO
    public class InfoPanelDerecho
    {
        public string Capacidad { get; set; } = "--";
        public string Formato { get; set; } = "--";
        public string VersionAtmos { get; set; } = "Desconocido";
        public string Serial { get; set; } = "N/A";
    }

    // 5. NUEVAS CLASES DE DISEÑO
    public class BrandingConfig
    {
        public string NombrePrograma { get; set; } = "NX-SUITE";
        public string LogoUrl { get; set; }
        public string ColorAcentoGlobal { get; set; } = "#00D2FF";
        public string BannerPorDefectoUrl { get; set; }
    }

    public class MundoMenuConfig
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string IconoUrl { get; set; }
        public string ColorNeon { get; set; } = "#00D2FF";
    }

    public class FiltroMandoConfig
    {
        public string Nombre { get; set; }
        public string Tag { get; set; }
    }
}