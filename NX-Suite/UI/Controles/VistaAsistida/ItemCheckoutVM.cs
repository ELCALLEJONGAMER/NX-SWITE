using NX_Suite.Models;
using System.Windows;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Item del checkout de VistaAsistida: representa visualmente un módulo
    /// (núcleo o complemento) en el resumen previo a la instalación.
    /// </summary>
    public class ItemCheckoutVM
    {
        public ModuloConfig Modulo        { get; init; } = null!;
        public string       PasoTitulo    { get; init; } = string.Empty;
        public string       ColorNeon     { get; init; } = "#00D2FF";
        public bool         EsComplemento { get; init; }

        public string    Nombre        => Modulo.Nombre;
        public string    Version       => Modulo.Versiones?.Count > 0 ? $"v{Modulo.Versiones[0].Version}" : string.Empty;
        public string    IconoUrl      => Modulo.IconoUrl;

        // Margen indentado para complementos en el resumen
        public Thickness MargenResumen => EsComplemento ? new Thickness(32, 0, 0, 6) : new Thickness(0, 0, 0, 6);
    }
}
