using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Parser y editor de archivos .ini compatibles con el formato de Hekate.
    /// Preserva comentarios, l�neas vac�as y el orden original del archivo al guardar.
    /// Tambi�n funciona como editor gen�rico para cualquier .ini est�ndar clave=valor.
    /// </summary>
    public class HekateIniManager
    {
        private readonly string _filePath;

        // Modelo en memoria: secci�n ? (clave ? valor)
        private readonly Dictionary<string, Dictionary<string, string>> _sections
            = new(StringComparer.OrdinalIgnoreCase);

        // L�neas originales del archivo para preservar comentarios y orden
        private readonly List<string> _lines = new();

        // Delimitador de apertura por secci�n: '[' para nyx.ini, '{' para hekate_ipl.ini
        private readonly Dictionary<string, char> _sectionDelim
            = new(StringComparer.OrdinalIgnoreCase);

        public HekateIniManager(string filePath) => _filePath = filePath;

        // ?? Helpers ????????????????????????????????????????????????????

        private static bool EsEncabezadoSeccion(string t, out string nombre, out char delim)
        {
            if (t.StartsWith('[') && t.EndsWith(']')) { nombre = t[1..^1]; delim = '['; return true; }
            if (t.StartsWith('{') && t.EndsWith('}')) { nombre = t[1..^1]; delim = '{'; return true; }
            nombre = string.Empty; delim = '['; return false;
        }

        // ?? Carga ?????????????????????????????????????????????????????????

        public async Task LoadAsync()
        {
            _lines.Clear();
            _sections.Clear();
            if (!File.Exists(_filePath)) return;

            string? sec = null;
            foreach (var line in await File.ReadAllLinesAsync(_filePath, new UTF8Encoding(false)))
            {
                _lines.Add(line);
                var t = line.Trim();

                if (EsEncabezadoSeccion(t, out var nombre, out var delim))
                {
                    sec = nombre;
                    _sectionDelim[sec] = delim;
                    if (!_sections.ContainsKey(sec))
                        _sections[sec] = new(StringComparer.OrdinalIgnoreCase);
                }
                else if (sec != null && t.Contains('=')
                         && !t.StartsWith(';') && !t.StartsWith('#'))
                {
                    var parts = t.Split('=', 2);
                    _sections[sec][parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        // ?? API p�blica ???????????????????????????????????????????????????

        /// <summary>Obtiene un valor. Devuelve null si la secci�n o clave no existen.</summary>
        public string? GetValue(string section, string key)
            => _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : null;

        /// <summary>Establece o crea una clave en una secci�n. Crea la secci�n si no existe.</summary>
        public void SetValue(string section, string key, string value)
        {
            if (!_sections.ContainsKey(section))
                _sections[section] = new(StringComparer.OrdinalIgnoreCase);
            _sections[section][key] = value;
        }

        /// <summary>Devuelve los nombres de secciones que contienen la clave con el valor indicado.</summary>
        public List<string> ObtenerSeccionesConClave(string clave, string? valor = null)
            => _sections
                .Where(s => s.Value.TryGetValue(clave, out var v)
                            && (valor == null || string.Equals(v, valor, System.StringComparison.OrdinalIgnoreCase)))
                .Select(s => s.Key)
                .ToList();

        // ?? Guardado preservando estructura original ???????????????????????

        /// <summary>
        /// Guarda los cambios preservando comentarios y orden del archivo original.
        /// Si el archivo no exist�a, lo crea desde cero.
        /// Claves nuevas se a�aden al final de su secci�n.
        /// Secciones nuevas se a�aden al final del archivo.
        /// </summary>
        public async Task SaveAsync()
        {
            var sb = new StringBuilder();

            // Rastrear qu� claves de cada secci�n ya fueron escritas
            // (para poder a�adir las nuevas al final de cada secci�n)
            var escritasPorSeccion = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            // Rastrear qu� secciones aparecieron en el archivo original
            var seccionesOriginales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? sec = null;

            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                var t    = line.Trim();

                if (t.StartsWith('[') && t.EndsWith(']'))
                {
                    // Antes de cambiar de secci�n, volcar claves nuevas de la secci�n anterior
                    if (sec != null)
                        EscribirClavesPendientes(sb, sec, escritasPorSeccion);

                    sec = t[1..^1];
                    seccionesOriginales.Add(sec);

                    if (!escritasPorSeccion.ContainsKey(sec))
                        escritasPorSeccion[sec] = new(StringComparer.OrdinalIgnoreCase);

                    sb.AppendLine(line);
                }
                else if (sec != null && t.Contains('=')
                         && !t.StartsWith(';') && !t.StartsWith('#'))
                {
                    var parts  = t.Split('=', 2);
                    var clave  = parts[0].Trim();

                    if (_sections.TryGetValue(sec, out var modeloSec) && modeloSec.TryGetValue(clave, out var nuevoVal))
                    {
                        // Actualizar valor en el lugar
                        sb.AppendLine($"{clave}={nuevoVal}");
                        escritasPorSeccion[sec].Add(clave);
                    }
                    else
                    {
                        // Clave que no est� en el modelo (no se toc�): copiar tal cual
                        sb.AppendLine(line);
                    }
                }
                else
                {
                    // Comentario, l�nea vac�a u otra l�nea � preservar
                    sb.AppendLine(line);
                }
            }

            // Volcar claves pendientes de la �ltima secci�n del archivo
            if (sec != null)
                EscribirClavesPendientes(sb, sec, escritasPorSeccion);

            // A�adir secciones completamente nuevas al final
            foreach (var (nombre, claves) in _sections)
            {
                if (seccionesOriginales.Contains(nombre)) continue;
                char d = _sectionDelim.TryGetValue(nombre, out var dl) ? dl : '[';
                char dc = d == '[' ? ']' : '}';
                sb.AppendLine($"{d}{nombre}{dc}");
                foreach (var (k, v) in claves)
                    sb.AppendLine($"{k}={v}");
                sb.AppendLine();
            }

            // Crear directorio si no existe y escribir
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(_filePath, sb.ToString(), new UTF8Encoding(false));
        }

        // ?? Helpers privados ??????????????????????????????????????????????

        /// <summary>
        /// Escribe al final de una secci�n las claves nuevas que SetValue a�adi�
        /// pero que no aparec�an en el archivo original.
        /// </summary>
        private void EscribirClavesPendientes(
            StringBuilder sb,
            string seccion,
            Dictionary<string, HashSet<string>> escritas)
        {
            if (!_sections.TryGetValue(seccion, out var modeloSec)) return;

            var yaEscritas = escritas.TryGetValue(seccion, out var set) ? set : new HashSet<string>();
            foreach (var (k, v) in modeloSec)
            {
                if (!yaEscritas.Contains(k))
                    sb.AppendLine($"{k}={v}");
            }
        }
    }
}
