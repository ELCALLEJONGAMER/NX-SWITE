using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace NX_Swite.Core
{
    /// <summary>
    /// Evaluador puro de restricciones de versión declaradas en el Gist.
    /// Soporta prefijos &lt;=, &gt;=, &lt;, &gt;. Sin prefijo se trata como &gt;=.
    /// Extraído de MainWindow.Diagnostico.cs para reutilizarse en otros
    /// escaneos de compatibilidad (ver Fuente D en EscanearIncompatibilidades).
    /// </summary>
    public static class VersionConstraintLogic
    {
        /// <summary>
        /// Parsea una expresion de constraint. Soporta prefijos: &lt;=, &gt;=, &lt;, &gt;.
        /// Sin prefijo se trata como &gt;=.
        /// </summary>
        public static (string Operador, Version Version)? ParseConstraintVersion(string expr)
        {
            expr = expr.Trim();
            string op, verStr;

            if      (expr.StartsWith("<=")) { op = "<="; verStr = expr[2..]; }
            else if (expr.StartsWith(">=")) { op = ">="; verStr = expr[2..]; }
            else if (expr.StartsWith("<"))  { op = "<";  verStr = expr[1..]; }
            else if (expr.StartsWith(">"))  { op = ">";  verStr = expr[1..]; }
            else                            { op = ">="; verStr = expr; }

            return Version.TryParse(NormalizarVersion(verStr.Trim()), out var ver)
                ? (op, ver)
                : null;
        }

        /// <summary>Devuelve true si la version instalada viola el constraint.</summary>
        public static bool ViolaConstraint(Version instalada, string operador, Version requerida) =>
            operador switch
            {
                "<=" => instalada > requerida,
                "<"  => instalada >= requerida,
                ">"  => instalada <= requerida,
                _    => instalada < requerida    // >= o sin prefijo
            };

        /// <summary>
        /// Normaliza una cadena de version tolerando sufijos comunes:
        /// - Prefijo "v"/"V" (ej. "v1.11.1").
        /// - Sufijos no numericos sin separador (ej. "1.11.1+", "1.11.1-dev").
        /// - Sufijos de hotfix con digitos al final (ej. "1.3.7hotfix1", "1.3.7hotfix2"):
        ///   se anaden como cuarto segmento de version ("1.3.7.1", "1.3.7.2") para que
        ///   System.Version pueda compararlas y para que 1.3.7 &lt; 1.3.7hotfix1 &lt; 1.3.7hotfix2.
        /// Si no hay match numerico valido, devuelve la cadena tal cual (dejara que
        /// Version.TryParse falle explicitamente en el llamador).
        /// </summary>
        public static string NormalizarVersion(string v)
        {
            v = v.TrimStart('v', 'V').Trim();

            // Extrae la version base "X.Y.Z" (o "X.Y", "X") seguida opcionalmente
            // de un sufijo textual con digitos al final (ej. "hotfix1").
            var match = Regex.Match(v, @"^(?<base>\d+(?:\.\d+){0,3})(?<sufijo>[^.\d].*?)?(?<hotfix>\d+)?$");
            if (!match.Success)
                return v;

            string baseVer = match.Groups["base"].Value;
            string hotfix  = match.Groups["hotfix"].Success && match.Groups["sufijo"].Success
                ? match.Groups["hotfix"].Value
                : string.Empty;

            if (baseVer.Count(c => c == '.') == 0)
                baseVer += ".0";

            if (!string.IsNullOrEmpty(hotfix) && baseVer.Count(c => c == '.') < 3)
                baseVer += "." + hotfix;

            return baseVer;
        }

        /// <summary>
        /// Quita el prefijo de operador (&lt;=, &gt;=, &lt;, &gt;) de una expresion de
        /// constraint para mostrar solo el numero de version al usuario
        /// (ej. "&lt;=1.11.1" -&gt; "1.11.1"). Las tarjetas de diagnostico usan
        /// etiquetas descriptivas (maximo/minimo/requerido) en vez del operador crudo.
        /// </summary>
        public static string LimpiarVersionMostrar(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return expr;
            expr = expr.Trim();

            if      (expr.StartsWith("<=")) return expr[2..].Trim();
            else if (expr.StartsWith(">=")) return expr[2..].Trim();
            else if (expr.StartsWith("<"))  return expr[1..].Trim();
            else if (expr.StartsWith(">"))  return expr[1..].Trim();
            return expr;
        }

        /// <summary>
        /// Construye la etiqueta descriptiva (sin operadores) para el limite
        /// declarado por un constraint, dado el nombre base (ej. "Firmware",
        /// "Atmosphere", o el nombre de un modulo dependencia).
        /// </summary>
        public static string EtiquetaLimitePorOperador(string operador, string nombreBase) =>
            operador switch
            {
                "<=" or "<" => $"{nombreBase} máximo compatible",
                ">=" or ">" => $"{nombreBase} mínimo requerido",
                _           => $"{nombreBase} requerido"
            };
    }
}
