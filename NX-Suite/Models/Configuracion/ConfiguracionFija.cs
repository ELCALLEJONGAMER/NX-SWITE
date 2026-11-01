using System.IO;
using System.Text.Json;

namespace NX_Swite.Models
{
    /// <summary>
    /// Estructura de la app que no se edita desde el catálogo remoto (menús,
    /// filtros del centro de mando, diagrama y presets de Nyx). Se lee del
    /// recurso embebido Assets/configuracion-fija.json y siempre prevalece
    /// sobre lo que traiga el catálogo, para que un JSON remoto antiguo o
    /// mal editado no pueda romper la navegación.
    /// </summary>
    internal static class ConfiguracionFija
    {
        private const string Recurso = "NX_Suite.configuracion-fija.json";

        private static readonly Lazy<GistData> Datos = new(Cargar);

        public static void Aplicar(GistData destino)
        {
            var fija = Datos.Value;
            destino.MundosMenu         = fija.MundosMenu;
            destino.FiltrosCentroMando = fija.FiltrosCentroMando;
            destino.DiagramaNodos      = fija.DiagramaNodos;
            destino.NyxConfigColors    = fija.NyxConfigColors;
        }

        private static GistData Cargar()
        {
            using Stream? s = typeof(ConfiguracionFija).Assembly.GetManifestResourceStream(Recurso);
            if (s == null) return new GistData();
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<GistDataSinFija>(s, opciones) ?? new GistData();
        }

        /// <summary>Evita reaplicar la configuración fija sobre sí misma al deserializarla.</summary>
        private sealed class GistDataSinFija : GistData
        {
            protected override bool AplicaConfiguracionFija => false;
        }
    }
}
