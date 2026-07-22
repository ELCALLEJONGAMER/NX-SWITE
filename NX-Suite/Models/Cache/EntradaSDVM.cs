using NX_Swite.Core;
using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_Swite.Models.Cache
{
    /// <summary>
    /// ViewModel de una entrada de primer nivel de la SD para el explorador
    /// de carpetas en la pesta�a "Carpetas Protegidas" de Ajustes.
    /// </summary>
    public sealed class EntradaSDVM : INotifyPropertyChanged
    {
        public string        Nombre    { get; }
        public EsTipoEntrada Tipo      { get; }
        /// <summary>True si es una carpeta cr�tica del sistema (emuMMC, Nintendo).</summary>
        public bool          EsCritico { get; }

        private bool _estaProtegido;
        public bool EstaProtegido
        {
            get => _estaProtegido;
            set { _estaProtegido = value; OnPropertyChanged(); }
        }

        public string IconoUrl => Tipo switch
        {
            EsTipoEntrada.Carpeta    => ConfiguracionRemota.Ui.IconoCarpetaUrl,
            EsTipoEntrada.Comprimido => ConfiguracionRemota.Ui.IconoZipUrl,
            _                        => ConfiguracionRemota.Ui.IconoArchivoUrl,
        };

        public EntradaSDVM(string nombre, EsTipoEntrada tipo, bool estaProtegido)
        {
            Nombre        = nombre;
            Tipo          = tipo;
            _estaProtegido = estaProtegido;
            EsCritico     = tipo == EsTipoEntrada.Carpeta &&
                            EntradaSD.NombresCriticos.Contains(nombre);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
