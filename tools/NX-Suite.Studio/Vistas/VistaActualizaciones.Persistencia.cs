using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Guarda en disco el resultado de la última revisión/validación para mostrarlo
    /// al abrir NX-STUDIO sin volver a consultar GitHub ni descargar paquetes.
    /// </summary>
    internal sealed partial class VistaActualizaciones
    {
        private sealed record PropuestaGuardada(string VersionActual, string VersionNueva, string? UrlRelease, string? Notas,
            string ModuloPropuesto, List<string> SdVieja, List<string> SdNueva, List<string> Avisos, string? ErrorEstructura, bool EnlaceRoto);

        private sealed record TarjetaGuardada(string Id, Grupo Grupo, ResultadoModulo Resultado, string Detalle, string? Vista, PropuestaGuardada? Propuesta);

        private sealed record RevisionGuardada(string Estado, List<TarjetaGuardada> Tarjetas);

        private static string RutaRevision =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NX-Studio", "ultima-revision.json");

        private static void GuardarRevision(List<Tarjeta> tarjetas, string estado)
        {
            try
            {
                var datos = new RevisionGuardada(estado, tarjetas.Select(t => new TarjetaGuardada(
                    t.Informe.Id, t.Grupo, t.Informe.Resultado, t.Informe.Detalle, t.Informe.Vista,
                    t.Informe.Propuesta is { } p
                        ? new PropuestaGuardada(p.VersionActual, p.VersionNueva, p.UrlRelease, p.Notas, p.ModuloPropuesto.ToJsonString(),
                            p.SdVieja.ToList(), p.SdNueva.ToList(), p.Avisos.ToList(), p.ErrorEstructura, p.EnlaceRoto)
                        : null)).ToList());
                Directory.CreateDirectory(Path.GetDirectoryName(RutaRevision)!);
                File.WriteAllText(RutaRevision, JsonSerializer.Serialize(datos), new UTF8Encoding(false));
            }
            catch { /* opcional: si falla, solo se pierde el resultado al cerrar */ }
        }

        /// <summary>Reconstruye las tarjetas guardadas enlazándolas con los módulos actuales del catálogo.</summary>
        private static (List<Tarjeta> Tarjetas, string Estado)? CargarRevision(DocumentoCatalogo doc)
        {
            try
            {
                if (!File.Exists(RutaRevision)) return null;
                var datos = JsonSerializer.Deserialize<RevisionGuardada>(File.ReadAllText(RutaRevision, Encoding.UTF8));
                if (datos == null) return null;
                var porId = doc.Lista("Modulos").OfType<JsonObject>()
                    .GroupBy(m => DocumentoCatalogo.Texto(m["Id"]), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var tarjetas = new List<Tarjeta>();
                foreach (var t in datos.Tarjetas)
                {
                    if (!porId.TryGetValue(t.Id, out var modulo)) continue;
                    PropuestaActualizacion? p = null;
                    if (t.Propuesta is { } g && JsonNode.Parse(g.ModuloPropuesto) is JsonObject propuesto)
                    {
                        // Si el catálogo ya tiene la versión propuesta (aprobada y publicada), la tarjeta sobra.
                        string actual = (modulo["Versiones"] as JsonArray)?.FirstOrDefault() is JsonObject v ? DocumentoCatalogo.Texto(v["Version"]) : "";
                        if (g.VersionNueva != g.VersionActual && actual == g.VersionNueva) continue;
                        p = new PropuestaActualizacion(g.VersionActual, g.VersionNueva, g.UrlRelease, g.Notas, propuesto,
                            new SortedSet<string>(g.SdVieja, StringComparer.OrdinalIgnoreCase), new SortedSet<string>(g.SdNueva, StringComparer.OrdinalIgnoreCase),
                            g.Avisos, g.ErrorEstructura, g.EnlaceRoto);
                    }
                    tarjetas.Add(new Tarjeta(modulo, new InformeModulo(t.Id, t.Resultado, t.Detalle, t.Vista, p), t.Grupo));
                }
                string fecha = File.GetLastWriteTime(RutaRevision).ToString("dd/MM HH:mm");
                return (tarjetas, $"{datos.Estado}  ·  guardada el {fecha}");
            }
            catch { return null; }
        }
    }
}
