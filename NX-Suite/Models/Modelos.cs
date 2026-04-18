using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace NX_Suite.Models
{
    public class ConfiguracionUI
    {
        public string IconoCacheUrl { get; set; } = string.Empty;
        public string ColorTextoCategoria { get; set; } = "#A0A0A0";
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

    public class EstadoProgreso
    {
        public double Porcentaje { get; set; }
        public string TareaActual { get; set; } = string.Empty;
        public int PasoActual { get; set; } = 0;
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
        public string NombrePrograma { get; set; } = string.Empty;
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

        /// <summary>
        /// Tipo de mundo. Valores: "catalogo" | "diagrama" | "asistido"
        /// </summary>
        public string Tipo { get; set; } = "catalogo";

        /// <summary>
        /// Solo aplica cuando Tipo == "asistido".
        /// Valores: "libre" | "forzado"
        /// </summary>
        public string ModoAsistente { get; set; } = "libre";
    }

    /// <summary>
    /// Define una subcategoría de complementos dentro de un ModuloConfig.
    /// Ejemplo: Hekate → Payloads, Diseño, Configuraciones.
    /// </summary>
    public class SubcategoriaConfig
    {
        /// <summary>Nombre visible de la subcategoría. Ej: "Payloads", "Diseño".</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Lista de valores de Categoria o Etiqueta que deben tener los ModuloConfig
        /// para aparecer en esta subcategoría.
        /// </summary>
        public List<string> CategoriasFiltro { get; set; } = new();

        /// <summary>
        /// true  → el usuario puede seleccionar varias tarjetas (Homebrew, Temas).
        /// false → solo puede seleccionar una (Bootloader, SubSistema).
        /// </summary>
        public bool PermiteMultiseleccion { get; set; } = false;
    }

    public class FiltroMandoConfig
    {
        public string Titulo { get; set; } = "Filtro";
        public string Nombre { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public List<string> Mundos { get; set; } = new();
        public string Tipo { get; set; } = "catalogo";
    }

    public class NodoDiagramaConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Mundo { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de nodo. Valores sugeridos: "nucleo" (slot del asistente), "complemento".
        /// </summary>
        public string Tipo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";

        /// <summary>
        /// En modo "forzado" el usuario debe seleccionar este slot obligatoriamente.
        /// En modo "libre" puede omitirlo.
        /// </summary>
        public bool EsObligatorio { get; set; }

        public bool SaltarChequeoFirmware { get; set; }

        public string? FW { get; set; }
        public string? CFW { get; set; }

        /// <summary>
        /// Categorías de ModuloConfig que se muestran al pulsar "+" en este slot.
        /// Ej: ["payload"] mostrará todas las tarjetas con Categoria == "payload".
        /// </summary>
        public List<string> CategoriasFiltro { get; set; } = new();

        public List<string> Hijos { get; set; } = new();
        public List<string> Requiere { get; set; } = new();
        public List<string> Habilita { get; set; } = new();
        public List<string> IncompatibleCon { get; set; } = new();
        public List<string> RutasInstalacion { get; set; } = new();
    }

    public enum EstadoCacheModulo
    {
        NoDescargado,
        ZipLocal,
        Preparado
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