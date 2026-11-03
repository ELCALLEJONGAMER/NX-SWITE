using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NX_Swite.Models;

namespace NX_Swite.Core
{
    /// <summary>
    /// Manifiesto local de instalaciones en la microSD (<c>/NX-Suite/instalados.json</c>).
    /// Tras instalar un módulo se guardan los SHA256 REALES de los archivos de su firma.
    /// Si el autor republica los binarios de una release, el catálogo puede quedar con
    /// hashes obsoletos; el detector usa este manifiesto para seguir reconociendo la
    /// versión que instaló NX-Suite mientras los archivos no cambien.
    /// </summary>
    public static class ManifiestoInstalacion
    {
        private const string RutaRelativa = "NX-Suite/instalados.json";

        public sealed class Entrada
        {
            public string Version { get; set; } = string.Empty;
            public Dictionary<string, string> Archivos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static string RutaManifiesto(string raizSD)
            => Path.Combine(raizSD, RutaRelativa.Replace('/', Path.DirectorySeparatorChar));

        public static Dictionary<string, Entrada> Leer(string raizSD)
        {
            try
            {
                string ruta = RutaManifiesto(raizSD);
                if (!File.Exists(ruta)) return new(StringComparer.OrdinalIgnoreCase);
                var datos = JsonSerializer.Deserialize<Dictionary<string, Entrada>>(File.ReadAllText(ruta));
                return datos == null
                    ? new(StringComparer.OrdinalIgnoreCase)
                    : new(datos, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Escribir(string raizSD, Dictionary<string, Entrada> datos)
        {
            try
            {
                string ruta = RutaManifiesto(raizSD);
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                File.WriteAllText(ruta, JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // El manifiesto es una ayuda de detección: un fallo no invalida la instalación.
            }
        }

        /// <summary>Registra los SHA256 reales de los archivos de la firma de <paramref name="version"/>.</summary>
        public static void Registrar(string raizSD, ModuloConfig modulo, string version)
        {
            if (modulo?.FirmasDeteccion == null || string.IsNullOrWhiteSpace(modulo.Id)) return;

            var firma = modulo.FirmasDeteccion.FirstOrDefault(f =>
                string.Equals(f?.Version, version, StringComparison.OrdinalIgnoreCase));
            if (firma?.Archivos == null || firma.Archivos.Count == 0) return;

            var sha = new SHA256Logic();
            var entrada = new Entrada { Version = version };
            foreach (var archivo in firma.Archivos)
            {
                if (string.IsNullOrWhiteSpace(archivo?.Ruta)) continue;
                string completa = Path.Combine(raizSD, archivo.Ruta.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                string hash = sha.ObtenerHashArchivo(completa);
                if (hash is "archivo_no_encontrado" or "error_lectura") return; // instalación no verificable
                entrada.Archivos[archivo.Ruta] = hash;
            }

            var datos = Leer(raizSD);
            datos[modulo.Id] = entrada;
            Escribir(raizSD, datos);
        }

        public static void Eliminar(string raizSD, string? moduloId)
        {
            if (string.IsNullOrWhiteSpace(moduloId)) return;
            var datos = Leer(raizSD);
            if (datos.Remove(moduloId)) Escribir(raizSD, datos);
        }

        /// <summary>
        /// Devuelve la versión registrada si todos los archivos anotados siguen existiendo
        /// con el mismo SHA256; en otro caso <c>null</c>.
        /// </summary>
        public static string? VersionVerificada(string raizSD, ModuloConfig modulo, SHA256Logic sha)
        {
            if (string.IsNullOrWhiteSpace(modulo?.Id)) return null;
            if (!Leer(raizSD).TryGetValue(modulo.Id, out var entrada) || entrada.Archivos.Count == 0)
                return null;

            foreach (var (ruta, hashEsperado) in entrada.Archivos)
            {
                string completa = Path.Combine(raizSD, ruta.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                if (!string.Equals(sha.ObtenerHashArchivo(completa), hashEsperado, StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            return entrada.Version;
        }
    }
}
