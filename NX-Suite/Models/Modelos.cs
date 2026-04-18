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
        public string Tipo { get; set; } = "catalogo";
    }

    public enum EstadoNodoAsistente
    {
        Pendiente,
        Descargando,
        Parcial,
        Listo
    }

    public class NodoAsistenteConfig : INotifyPropertyChanged
    {
        private string _iconoUrl = string.Empty;
        private string _versionMostrada = string.Empty;
        private string _nombreMostrado = string.Empty;
        private double _progreso;
        private EstadoNodoAsistente _estado = EstadoNodoAsistente.Pendiente;

        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;

        public List<string> CategoriasObjetivo { get; set; } = new();
        public List<string> HijosIds { get; set; } = new();
        public List<string> RequiereIds { get; set; } = new();

        public bool Opcional { get; set; }
        public bool PermiteSeleccionMultiple { get; set; }
        public string ColorNeon { get; set; } = "#00D2FF";

        public string IconoUrl
        {
            get => _iconoUrl;
            set
            {
                if (_iconoUrl == value)
                    return;

                _iconoUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneContenidoSeleccionado));
            }
        }

        public string VersionMostrada
        {
            get => _versionMostrada;
            set
            {
                if (_versionMostrada == value)
                    return;

                _versionMostrada = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneContenidoSeleccionado));
            }
        }

        public string NombreMostrado
        {
            get => _nombreMostrado;
            set
            {
                if (_nombreMostrado == value)
                    return;

                _nombreMostrado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneContenidoSeleccionado));
            }
        }

        public double Progreso
        {
            get => _progreso;
            set
            {
                var nuevoValor = Math.Clamp(value, 0, 1);
                if (Math.Abs(_progreso - nuevoValor) < 0.0001)
                    return;

                _progreso = nuevoValor;
                OnPropertyChanged();
            }
        }

        public EstadoNodoAsistente Estado
        {
            get => _estado;
            set
            {
                if (_estado == value)
                    return;

                _estado = value;
                OnPropertyChanged();
            }
        }

        public bool TieneContenidoSeleccionado =>
            !string.IsNullOrWhiteSpace(IconoUrl) ||
            !string.IsNullOrWhiteSpace(VersionMostrada) ||
            !string.IsNullOrWhiteSpace(NombreMostrado);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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
        public string Tipo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";

        public bool EsObligatorio { get; set; }
        public bool SaltarChequeoFirmware { get; set; }

        public string? FW { get; set; }
        public string? CFW { get; set; }

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