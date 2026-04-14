using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NX_Suite.Models
{
    public class PasoPipeline
    {
        public int Paso { get; set; }
        public string TipoAccion { get; set; }
        public string MensajeUI { get; set; }
        public JsonElement Parametros { get; set; }
    }

    public class ModuloVersion
    {
        public string Version { get; set; }
        public List<PasoPipeline> PipelineInstalacion { get; set; } = new List<PasoPipeline>();
        public List<PasoPipeline> PipelineDesinstalacion { get; set; } = new List<PasoPipeline>();
    }

    public class ModuloConfig : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Categoria { get; set; }
        public string Mundo { get; set; }
        public List<string> Etiquetas { get; set; } = new List<string>();
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string IconoUrl { get; set; }
        public string UrlOficial { get; set; }
        public List<string> Dependencias { get; set; }
        public List<string> IncompatibleCon { get; set; }
        public List<ModuloVersion> Versiones { get; set; }
        private string _versionInstalada = "No detectado";
        public string VersionInstalada
        {
            get => _versionInstalada;
            set
            {
                _versionInstalada = value;
                OnPropertyChanged(); // Avisa a la tarjeta que debe redibujarse
                OnPropertyChanged(nameof(RequiereUpdate)); // Avisa que el neón podría encenderse
            }
        }
        public bool RequiereUpdate
        {
            get
            {
                if (string.IsNullOrEmpty(VersionInstalada) || VersionInstalada == "No detectado" || VersionInstalada == "No instalado")
                    return false;

                if (Versiones != null && Versiones.Count > 0)
                {
                    // Si la versión en el Gist es diferente a la detectada en la SD
                    return Versiones[0].Version != VersionInstalada;
                }
                return false;
            }
        }
        public string AlertaSeguridad { get; set; }
        public bool TieneUrlOficial => !string.IsNullOrEmpty(UrlOficial);
        public List<string> RutasDesinstalacion { get; set; }
        public List<FirmaDeteccion> FirmasDeteccion { get; set; } = new List<FirmaDeteccion>();

        private bool _estaEnCache = false;
        public bool EstaEnCache
        {
            get => _estaEnCache;
            set
            {
                if (_estaEnCache != value)
                {
                    _estaEnCache = value;
                    OnPropertyChanged(nameof(EstaEnCache));
                    OnPropertyChanged(nameof(IconoCacheActual));
                    OnPropertyChanged(nameof(MensajeCacheActual));
                }
            }
        }

        public string IconoCacheActual => EstaEnCache
            ? MainWindow.UIGlobal?.IconoCacheDescargado
            : MainWindow.UIGlobal?.IconoCacheNoDescargado;

        public string MensajeCacheActual => EstaEnCache
            ? "Listo en PC (Instalación Rápida)"
            : "Disponible en la Nube (Requiere Descarga)";

        public string ColorCategoria => MainWindow.UIGlobal?.ColorTextoCategoria ?? "#A0A0A0";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class FirmaDeteccion
    {
        public string Version { get; set; }
        public List<ArchivoCritico> Archivos { get; set; } = new List<ArchivoCritico>();
    }

    public class ArchivoCritico
    {
        public string Ruta { get; set; }   // Ej: "atmosphere/package3"
        public string SHA256 { get; set; } // El hash oficial de esa versión
    }

}