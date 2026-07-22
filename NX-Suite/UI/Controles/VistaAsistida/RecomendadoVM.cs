using NX_Swite.Models;
using System.Linq;
using System.Windows;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// ViewModel de un m�dulo recomendado en el panel ASISTIDO COMPLETO.
    /// Combina la definici�n del m�dulo (<see cref="Modulo"/>) con la entrada
    /// de configuraci�n del Gist (<see cref="Config"/>) y expone propiedades
    /// listas para bindear (versi�n a instalar, badge, nota, visibilidades).
    /// </summary>
    public class RecomendadoVM
    {
        public ModuloConfig      Modulo  { get; init; } = null!;
        public ModuloRecomendado Config  { get; init; } = null!;

        /// <summary>
        /// Versi�n que se instalar�: la fijada en Config.Version o la primera disponible.
        /// </summary>
        public string VersionAInstalar =>
            Config.Version ?? Modulo.Versiones?.FirstOrDefault()?.Version ?? "�";

        public string EtiquetaVersion =>
            Config.Version != null ? $"v{Config.Version} fijada" : "�ltima";

        public string Nota => Config.Nota;

        /// <summary>Badge de versi�n fijada visible si hay versi�n expl�cita en el JSON.</summary>
        public Visibility VersionFijadaVisible =>
            Config.Version != null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NotaVisible =>
            string.IsNullOrWhiteSpace(Config.Nota) ? Visibility.Collapsed : Visibility.Visible;
    }
}
