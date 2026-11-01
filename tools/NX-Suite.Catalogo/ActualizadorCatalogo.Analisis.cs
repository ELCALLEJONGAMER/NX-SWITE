using System.Text.Json.Nodes;

namespace NX_Suite.Catalogo
{
    internal sealed partial class ActualizadorCatalogo
    {
        /// <summary>
        /// Descarga el paquete de una versión, simula la SD y devuelve cada archivo
        /// final con su SHA256 (null si es generado o no se pudo localizar en el paquete).
        /// </summary>
        public async Task<(List<ArchivoAnalizado> Archivos, List<string> Avisos)> AnalizarVersionAsync(JsonObject version)
        {
            var assets = Descargas(version)
                .Select(p => p["Url"]?.GetValue<string>() ?? "")
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                .Select(u => (Path.GetFileName(Uri.UnescapeDataString(new Uri(u).AbsolutePath)), u));
            var contenido = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var paquete = await LeerPaqueteAsync(assets, contenido);
            var avisos = new List<string>();
            var sd = SimuladorSD.Simular(Pasos(version), contenido, avisos);
            var renombrados = Renombrados(version);

            const string marcaGenerado = "  (generado)";
            var lista = new List<ArchivoAnalizado>();
            foreach (var r in sd)
            {
                if (r.EndsWith(marcaGenerado, StringComparison.Ordinal))
                    lista.Add(new(r[..^marcaGenerado.Length], null, true));
                else
                    lista.Add(new(r, HashEnPaquete(r, paquete, renombrados), false));
            }
            return (lista, avisos);
        }

        /// <summary>Busca el hash de una ruta de la SD en el paquete por el sufijo más largo que sea único.</summary>
        internal static string? HashEnPaquete(
            string rutaSd, IReadOnlyDictionary<string, string> paquete, IReadOnlyDictionary<string, string> renombrados)
        {
            string ruta = renombrados.TryGetValue(rutaSd.TrimStart('/'), out var origen) ? "/" + origen.TrimStart('/') : rutaSd;
            var segmentos = ruta.Replace('\\', '/').Trim('/').Split('/');
            for (int i = 0; i < segmentos.Length; i++)
            {
                string sufijo = "/" + string.Join('/', segmentos.Skip(i));
                var hashes = paquete.Where(h => ("/" + h.Key.Replace('\\', '/').TrimStart('/')).EndsWith(sufijo, StringComparison.OrdinalIgnoreCase))
                                    .Select(h => h.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (hashes.Count == 1) return hashes[0];
                if (hashes.Count > 1) return null;
            }
            return null;
        }
    }
}
