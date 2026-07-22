using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Valida el contenido de archivos de configuraci�n en la SD contra las
    /// <see cref="ReglasConfig"/> declaradas en la versi�n del m�dulo del cat�logo.
    ///
    /// Uso:
    ///   var reglas = modulo.Versiones[0].ReglasConfig;
    ///   var hallazgos = await _validador.ValidarAsync(letraSD, reglas);
    ///   // Si hallazgos.Any(h => h.EsCritico) ? ParcialmenteInstalado
    ///
    /// Formatos soportados:
    ///   "ini"   ? usa HekateIniManager (secci�n + clave)
    ///   "txt"   ? clave=valor por l�nea, sin secciones
    ///   "hosts" ? l�neas "IP host", validaci�n por presencia de host
    ///   "exacto" ? compara el contenido completo del archivo contra ContenidoEsperado
    /// </summary>
    public class ValidadorConfiguracion
    {
        /// <summary>
        /// Eval�a todas las entradas de la lista de ReglasConfig contra los archivos reales en la SD.
        /// Agrega los hallazgos de todos los archivos en una �nica lista.
        /// </summary>
        public async Task<List<HallazgoConfig>> ValidarListaAsync(string letraSD, List<ReglasConfig> lista)
        {
            var todos = new List<HallazgoConfig>();
            foreach (var reglas in lista)
                todos.AddRange(await ValidarAsync(letraSD, reglas));
            return todos;
        }

        /// <summary>
        /// Eval�a una entrada de ReglasConfig contra el archivo real en la SD.
        /// Devuelve la lista de hallazgos (reglas que fallaron).
        /// </summary>
        public async Task<List<HallazgoConfig>> ValidarAsync(string letraSD, ReglasConfig reglas)
        {
            var hallazgos = new List<HallazgoConfig>();

            if (reglas == null) return hallazgos;

            string formato = reglas.Formato?.ToLowerInvariant() ?? "ini";

            if (formato != "exacto" && reglas.Reglas.Count == 0)
                return hallazgos;

            string rutaRelativa = reglas.RutaSD
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);
            string rutaAbsoluta = Path.Combine(letraSD, rutaRelativa);

            if (!File.Exists(rutaAbsoluta))
                return hallazgos;

            return formato switch
            {
                "ini"    => await ValidarIniAsync(rutaAbsoluta, reglas.Reglas),
                "txt"    => await ValidarTxtAsync(rutaAbsoluta, reglas.Reglas),
                "hosts"  => await ValidarHostsAsync(rutaAbsoluta, reglas.Reglas),
                "exacto" => await ValidarExactoAsync(rutaAbsoluta, reglas.RutaSD, reglas.ContenidoEsperado),
                _        => hallazgos
            };
        }

        // ?? INI ??????????????????????????????????????????????????????????

        private static async Task<List<HallazgoConfig>> ValidarIniAsync(string ruta, List<ReglaConfig> reglas)
        {
            var hallazgos = new List<HallazgoConfig>();
            var ini = new HekateIniManager(ruta);
            await ini.LoadAsync();

            foreach (var regla in reglas)
            {
                string? valorActual = ini.GetValue(regla.Seccion, regla.Clave);
                var hallazgo = EvaluarRegla(regla, valorActual);
                if (hallazgo != null) hallazgos.Add(hallazgo);
            }

            return hallazgos;
        }

        // ?? TXT (clave=valor sin secciones) ??????????????????????????????

        private static async Task<List<HallazgoConfig>> ValidarTxtAsync(string ruta, List<ReglaConfig> reglas)
        {
            var hallazgos = new List<HallazgoConfig>();
            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var linea in await File.ReadAllLinesAsync(ruta))
            {
                var t = linea.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith(';') || t.StartsWith('#'))
                    continue;

                var partes = t.Split('=', 2);
                if (partes.Length == 2)
                    mapa[partes[0].Trim()] = partes[1].Trim();
            }

            foreach (var regla in reglas)
            {
                string? valorActual = mapa.TryGetValue(regla.Clave, out var v) ? v : null;
                var hallazgo = EvaluarRegla(regla, valorActual);
                if (hallazgo != null) hallazgos.Add(hallazgo);
            }

            return hallazgos;
        }

        // ?? HOSTS (presencia de l�neas "IP host") ?????????????????????????

        private static async Task<List<HallazgoConfig>> ValidarHostsAsync(string ruta, List<ReglaConfig> reglas)
        {
            var hallazgos = new List<HallazgoConfig>();
            var lineas = await File.ReadAllLinesAsync(ruta);

            // Para hosts: Clave = nombre de host a buscar; ValorEsperado = IP esperada (opcional)
            foreach (var regla in reglas)
            {
                string? valorActual = null;

                foreach (var linea in lineas)
                {
                    var t = linea.Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;

                    var partes = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length >= 2 &&
                        string.Equals(partes[1], regla.Clave, StringComparison.OrdinalIgnoreCase))
                    {
                        valorActual = partes[0]; // la IP
                        break;
                    }
                }

                var hallazgo = EvaluarRegla(regla, valorActual);
                if (hallazgo != null) hallazgos.Add(hallazgo);
            }

            return hallazgos;
        }

        // ?? Evaluaci�n com�n ??????????????????????????????????????????????

        private static HallazgoConfig? EvaluarRegla(ReglaConfig regla, string? valorActual)
        {
            bool falla = false;
            string? valorEsperadoMostrar = regla.ValorEsperado;

            if (regla.ValorEsperado != null)
            {
                // La clave no existe O el valor no coincide
                falla = valorActual == null ||
                        !string.Equals(valorActual, regla.ValorEsperado, StringComparison.OrdinalIgnoreCase);
            }
            else if (regla.ValorProhibido != null)
            {
                // El valor existe Y coincide con el prohibido
                falla = valorActual != null &&
                        string.Equals(valorActual, regla.ValorProhibido, StringComparison.OrdinalIgnoreCase);
                valorEsperadoMostrar = $"? {regla.ValorProhibido}";
            }

            if (!falla) return null;

            return new HallazgoConfig
            {
                Seccion       = regla.Seccion,
                Clave         = regla.Clave,
                ValorActual   = valorActual,
                ValorEsperado = valorEsperadoMostrar,
                Severidad     = regla.Severidad,
                Mensaje       = regla.Mensaje
            };
        }

        // ?? Exacto (comparaci�n de contenido completo) ????????????????????

        private static async Task<List<HallazgoConfig>> ValidarExactoAsync(
            string rutaAbsoluta, string rutaSD, string? contenidoEsperado)
        {
            var hallazgos = new List<HallazgoConfig>();

            if (string.IsNullOrEmpty(contenidoEsperado))
                return hallazgos;

            string contenidoReal = await File.ReadAllTextAsync(rutaAbsoluta);

            // Normalizar: interpretar \n literales del JSON y unificar saltos de l�nea
            string esperadoNorm = contenidoEsperado
                .Replace("\\n", "\n")
                .Replace("\r\n", "\n")
                .Trim();
            string realNorm = contenidoReal
                .Replace("\r\n", "\n")
                .Trim();

            if (string.Equals(esperadoNorm, realNorm, StringComparison.Ordinal))
                return hallazgos;

            hallazgos.Add(new HallazgoConfig
            {
                Seccion       = string.Empty,
                Clave         = rutaSD,
                ValorActual   = "Contenido modificado o desactualizado",
                ValorEsperado = "Contenido oficial del m�dulo",
                Severidad     = "Critica",
                Mensaje       = $"El archivo '{rutaSD}' no coincide con el contenido esperado. Reinstala el m�dulo."
            });

            return hallazgos;
        }
    }
}
