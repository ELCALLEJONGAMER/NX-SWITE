using System;
using System.Linq;

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

        public static string NormalizarVersion(string v)
        {
            v = v.TrimStart('v', 'V').Trim();
            return v.Count(c => c == '.') == 0 ? v + ".0" : v;
        }
    }
}
