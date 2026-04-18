using NX_Suite.Models;

namespace NX_Suite.Core
{
    /// <summary>
    /// Servicio estático que desacopla la configuración de UI
    /// de la capa de presentación (MainWindow).
    /// </summary>
    public static class UIConfigService
    {
        public static ConfiguracionUI Current { get; set; } = new ConfiguracionUI();
    }
}