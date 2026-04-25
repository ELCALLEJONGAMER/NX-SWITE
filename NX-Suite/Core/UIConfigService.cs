using NX_Suite.Models;
using System.Collections.Generic;

namespace NX_Suite.Core
{
    public static class UIConfigService
    {
        public static ConfiguracionUI         Current       { get; set; } = new();
        public static NyxConfigColors         NyxColors     { get; set; } = new();

        /// <summary>
        /// Módulos recomendados cargados desde el Gist, ordenados por ModuloRecomendado.Orden.
        /// </summary>
        public static List<ModuloRecomendado> Recomendados  { get; set; } = new();
    }
}