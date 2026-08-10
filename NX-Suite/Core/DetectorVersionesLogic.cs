using System;
using System.Collections.Generic;
using System.IO;
using NX_Swite.Models;

namespace NX_Swite.Core
{
    public class DetectorVersionesLogic
    {
        private readonly SHA256Logic _shaTool = new SHA256Logic();

        /// <summary>
        /// Construye, a partir del cat�logo completo actualmente cargado, un mapa de
        /// "ruta normalizada" ? conjunto de <see cref="ModuloConfig.Id"/> que la declaran
        /// en alguna de sus <see cref="FirmaDeteccion"/> — CON o SIN SHA256. Debe
        /// calcularse UNA VEZ por refresco/sincronizaci�n del cat�logo (no por m�dulo) y
        /// pasarse a <see cref="DeterminarEstadoInstalacion"/>: se usa tanto para el
        /// fallback de "contenido no reconocido" (rutas con SHA) como para la identidad
        /// estructural de firmas sin SHA. Una ruta es exclusiva de un m�dulo cuando el
        /// conjunto asociado contiene �nicamente su Id.
        /// </summary>
        public static Dictionary<string, HashSet<string>> ConstruirMapaExclusividad(
            IEnumerable<ModuloConfig>? catalogo)
        {
            var mapa = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (catalogo == null) return mapa;

            foreach (var modulo in catalogo)
            {
                if (modulo?.FirmasDeteccion == null) continue;

                foreach (var firma in modulo.FirmasDeteccion)
                {
                    if (firma?.Archivos == null) continue;

                    foreach (var archivo in firma.Archivos)
                    {
                        if (string.IsNullOrWhiteSpace(archivo?.Ruta)) continue;

                        string rutaNormalizada = NormalizarRuta(archivo.Ruta);

                        if (!mapa.TryGetValue(rutaNormalizada, out var ids))
                        {
                            ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            mapa[rutaNormalizada] = ids;
                        }
                        ids.Add(modulo.Id);
                    }
                }
            }

            return mapa;
        }

        private static string NormalizarRuta(string ruta)
            => ruta.Trim().TrimStart('/', '\\').Replace('\\', '/');

        // ── SD Intelligence 2: índice limitado de /switch/*.nro ──────────────

        /// <summary>Carpeta raíz (relativa a la SD) bajo la que se permite la búsqueda
        /// alternativa. Limitada a homebrew en esta primera implementación.</summary>
        private const string CarpetaHomebrewSwitch = "switch";

        /// <summary>
        /// Construye, UNA VEZ por refresco de la SD (no por módulo ni por versión), un
        /// índice de todos los archivos ".nro" que existen físicamente bajo
        /// "&lt;raízSD&gt;/switch/", con profundidad máxima de 2 niveles
        /// (switch/archivo.nro y switch/carpeta/archivo.nro). El índice se agrupa por
        /// nombre de archivo (case-insensitive) para poder localizar candidatos de
        /// forma O(1) sin volver a recorrer el disco por cada FirmaDeteccion.
        /// No recorre Nintendo/, atmosphere/contents/, firmware ni ninguna otra zona.
        /// </summary>
        public static Dictionary<string, List<string>> ConstruirIndiceHomebrewSwitch(string rutaRaizSD)
        {
            var indice = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rutaRaizSD)) return indice;

            string carpetaSwitch = Path.Combine(rutaRaizSD, CarpetaHomebrewSwitch);
            if (!Directory.Exists(carpetaSwitch)) return indice;

            void IndexarArchivosNro(string carpeta)
            {
                string[] archivos;
                try { archivos = Directory.GetFiles(carpeta, "*.nro"); }
                catch { return; }

                foreach (var archivo in archivos)
                {
                    string nombre = Path.GetFileName(archivo);
                    if (!indice.TryGetValue(nombre, out var lista))
                    {
                        lista = new List<string>();
                        indice[nombre] = lista;
                    }
                    lista.Add(archivo);
                }
            }

            // Nivel 0: switch/archivo.nro
            IndexarArchivosNro(carpetaSwitch);

            // Nivel 1: switch/carpeta/archivo.nro (profundidad máxima permitida)
            string[] subcarpetas;
            try { subcarpetas = Directory.GetDirectories(carpetaSwitch); }
            catch { subcarpetas = Array.Empty<string>(); }

            foreach (var subcarpeta in subcarpetas)
                IndexarArchivosNro(subcarpeta);

            return indice;
        }

        public (string Version, EstadoSdModulo EstadoSd) DeterminarEstadoInstalacion(
            string rutaRaizSD, ModuloConfig modulo,
            Dictionary<string, HashSet<string>>? mapaExclusividad = null,
            Dictionary<string, List<string>>? indiceHomebrewSwitch = null)
        {
            if (modulo == null)
                return ("Desconocido", EstadoSdModulo.NoInstalado);

            modulo.ArchivosFaltantesDeteccion = new List<string>();
            modulo.RutaFisicaAlternativa      = null;
            modulo.EstaEnUbicacionNoEstandar  = false;
            modulo.TieneMultiplesUbicaciones  = false;

            if (modulo.FirmasDeteccion == null || modulo.FirmasDeteccion.Count == 0)
                return ("Desconocido", EstadoSdModulo.NoInstalado);

            // Los módulos de configuración conservan el comportamiento previo:
            // su segunda capa de validación real (ValidadorConfiguracion/ReglasConfig)
            // ya se encarga de decidir si el contenido es correcto, así que una firma
            // sin SHA256 puede seguir afirmando "Instalado + firma.Version" para ellos.
            if (modulo.EsConfiguracion)
                return DeterminarEstadoInstalacionConfiguracion(rutaRaizSD, modulo);

            return DeterminarEstadoInstalacionEstandar(rutaRaizSD, modulo, mapaExclusividad, indiceHomebrewSwitch);
        }

        /// <summary>Comportamiento previo, sin distinguir SHA, reservado a módulos EsConfiguracion == true.</summary>
        private (string Version, EstadoSdModulo EstadoSd) DeterminarEstadoInstalacionConfiguracion(
            string rutaRaizSD, ModuloConfig modulo)
        {
            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count == 0)
                    continue;

                if (!FirmaIdentificadaPorSha(rutaRaizSD, firma))
                    continue;

                var archivosFaltantes = ArchivosFaltantesDe(rutaRaizSD, firma);
                modulo.ArchivosFaltantesDeteccion = archivosFaltantes;

                if (archivosFaltantes.Count == 0)
                    return (firma.Version, EstadoSdModulo.Instalado);

                if (archivosFaltantes.Count < firma.Archivos.Count)
                    return (firma.Version, EstadoSdModulo.ParcialmenteInstalado);
            }

            return ("No instalado", EstadoSdModulo.NoInstalado);
        }

        /// <summary>
        /// Detección para módulos normales (EsConfiguracion == false). Separa IDENTIDAD,
        /// VERSIÓN e INTEGRIDAD: solo una firma con SHA256 coincidente puede afirmar una
        /// versión exacta. Las firmas sin SHA256 solo aportan evidencia ESTRUCTURAL
        /// (presencia), nunca una versión exacta.
        /// </summary>
        private (string Version, EstadoSdModulo EstadoSd) DeterminarEstadoInstalacionEstandar(
            string rutaRaizSD, ModuloConfig modulo, Dictionary<string, HashSet<string>>? mapaExclusividad,
            Dictionary<string, List<string>>? indiceHomebrewSwitch = null)
        {
            (string Version, EstadoSdModulo EstadoSd, List<string> Faltantes)? mejorParcialConocido = null;

            // ── Fase 1: firmas con al menos un SHA256 declarado ──────────────
            // Una versión solo queda IDENTIFICADA cuando sus evidencias SHA coinciden.
            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count == 0)
                    continue;
                if (!TieneAlgunSha(firma))
                    continue;

                if (!FirmaIdentificadaPorSha(rutaRaizSD, firma))
                    continue; // SHA no coincide → esta firma no es la versión instalada

                var archivosFaltantes = ArchivosFaltantesDe(rutaRaizSD, firma);

                if (archivosFaltantes.Count == 0)
                {
                    // Instalado + versión conocida completa: máxima prioridad, se puede devolver ya.
                    modulo.ArchivosFaltantesDeteccion = archivosFaltantes;
                    return (firma.Version, EstadoSdModulo.Instalado);
                }

                bool algunoPresente = archivosFaltantes.Count < firma.Archivos.Count;
                if (algunoPresente)
                {
                    // Guardar el mejor candidato parcial CONOCIDO y seguir comprobando
                    // el resto de firmas: una versión conocida completa posterior debe
                    // tener prioridad sobre este parcial.
                    mejorParcialConocido ??= (firma.Version, EstadoSdModulo.ParcialmenteInstalado, archivosFaltantes);
                }
            }

            // ── Fase 1.5: búsqueda alternativa LIMITADA (SD Intelligence 2) ──
            // Solo se ejecuta cuando la detección exacta de la Fase 1 NO identificó
            // ni una versión completa ni un parcial conocido en la ruta estándar.
            // La detección exacta SIEMPRE tiene prioridad sobre esta búsqueda.
            if (mejorParcialConocido == null && indiceHomebrewSwitch != null && indiceHomebrewSwitch.Count > 0)
            {
                var resultadoAlternativo = BuscarPorUbicacionAlternativa(rutaRaizSD, modulo, indiceHomebrewSwitch);
                if (resultadoAlternativo != null)
                {
                    modulo.ArchivosFaltantesDeteccion = new List<string>();
                    modulo.RutaFisicaAlternativa     = resultadoAlternativo.Value.RutaEncontrada;
                    modulo.EstaEnUbicacionNoEstandar  = true;
                    modulo.TieneMultiplesUbicaciones  = resultadoAlternativo.Value.MultiplesUbicaciones;
                    return (resultadoAlternativo.Value.Version, EstadoSdModulo.Instalado);
                }
            }

            // ── Fase 2: contenido presente pero no reconocido por SHA ────────
            // Evidencia fuerte de módulo presente con versión no identificable.
            // Tiene PRIORIDAD sobre un parcial conocido de la Fase 1: si el SHA físico
            // no coincide con ninguna versión conocida, no podemos juzgar su integridad
            // usando la estructura de una versión que no es la instalada realmente.
            if (mapaExclusividad != null &&
                TieneEvidenciaVersionDesconocida(rutaRaizSD, modulo, mapaExclusividad))
            {
                modulo.ArchivosFaltantesDeteccion = new List<string>();
                return (string.Empty, EstadoSdModulo.InstaladoVersionDesconocida);
            }

            if (mejorParcialConocido != null)
            {
                modulo.ArchivosFaltantesDeteccion = mejorParcialConocido.Value.Faltantes;
                return (mejorParcialConocido.Value.Version, mejorParcialConocido.Value.EstadoSd);
            }

            // ── Fase 3: firmas SIN SHA256, solo como evidencia ESTRUCTURAL ───
            // Nunca afirman una versión exacta (VersionInstalada queda vacía).
            (EstadoSdModulo EstadoSd, List<string> Faltantes)? mejorEstructuralParcial = null;

            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count == 0)
                    continue;
                if (TieneAlgunSha(firma))
                    continue; // ya evaluada en fase 1

                if (!TieneIdentidadEstructural(firma, modulo, mapaExclusividad))
                    continue; // sin evidencia suficiente de identidad, no aplica

                var archivosFaltantes = ArchivosFaltantesDe(rutaRaizSD, firma);

                if (archivosFaltantes.Count == 0)
                {
                    // Estructura completa presente, pero sin SHA no se puede afirmar
                    // la versión exacta declarada por la firma.
                    modulo.ArchivosFaltantesDeteccion = archivosFaltantes;
                    return (string.Empty, EstadoSdModulo.InstaladoVersionDesconocida);
                }

                bool algunoPresente = archivosFaltantes.Count < firma.Archivos.Count;
                if (algunoPresente)
                    mejorEstructuralParcial ??= (EstadoSdModulo.ParcialmenteInstalado, archivosFaltantes);
            }

            if (mejorEstructuralParcial != null)
            {
                modulo.ArchivosFaltantesDeteccion = mejorEstructuralParcial.Value.Faltantes;
                return (string.Empty, mejorEstructuralParcial.Value.EstadoSd);
            }

            // ── Fase 4: restos estructurales genéricos ───────────────────────
            // Ninguna firma (con o sin SHA) coincidió ni completa ni parcialmente.
            // Aun así, si quedan al menos 2 rutas declaradas del módulo presentes
            // físicamente y al menos una de ellas es exclusiva de este módulo en
            // el catálogo actual, hay evidencia suficiente de que quedan restos
            // del módulo (p.ej. se borró el archivo identificador principal pero
            // persisten otras carpetas/archivos declarados). No se afirma versión.
            if (mapaExclusividad != null &&
                TieneRestosEstructurales(rutaRaizSD, modulo, mapaExclusividad))
            {
                modulo.ArchivosFaltantesDeteccion = new List<string>();
                return (string.Empty, EstadoSdModulo.ParcialmenteInstalado);
            }

            return ("No instalado", EstadoSdModulo.NoInstalado);
        }

        /// <summary>
        /// Fallback genérico y simple (sin scoring ni porcentajes): recoge TODAS las
        /// rutas únicas declaradas por el módulo en cualquiera de sus FirmasDeteccion,
        /// cuenta cuántas existen físicamente y comprueba que al menos una de las
        /// existentes sea exclusiva de este módulo en el catálogo actual. Un módulo
        /// de un único archivo nunca puede satisfacer el mínimo de 2 rutas, por lo
        /// que si su único archivo desapareció sigue siendo NoInstalado.
        /// </summary>
        private static bool TieneRestosEstructurales(
            string rutaRaizSD, ModuloConfig modulo, Dictionary<string, HashSet<string>> mapaExclusividad)
        {
            var rutasUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null) continue;
                foreach (var archivo in firma.Archivos)
                {
                    if (string.IsNullOrWhiteSpace(archivo?.Ruta)) continue;
                    rutasUnicas.Add(NormalizarRuta(archivo.Ruta));
                }
            }

            int rutasPresentes = 0;
            bool algunaExclusivaPresente = false;

            foreach (var rutaNormalizada in rutasUnicas)
            {
                string rutaOriginal = rutaNormalizada;
                string rutaRelativa = rutaNormalizada.Replace('/', Path.DirectorySeparatorChar);
                string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                if (!ExisteRuta(rutaCompleta, rutaOriginal))
                    continue;

                rutasPresentes++;

                if (mapaExclusividad.TryGetValue(rutaNormalizada, out var ids) &&
                    ids.Count == 1 && ids.Contains(modulo.Id))
                {
                    algunaExclusivaPresente = true;
                }
            }

            return rutasPresentes >= 2 && algunaExclusivaPresente;
        }

        /// <summary>
        /// SD Intelligence 2 — búsqueda alternativa LIMITADA para módulos que declaran
        /// firmas con SHA256 bajo "/switch/" pero cuya ruta estándar no existe. Solo se
        /// invoca cuando la detección exacta (Fase 1) no encontró ni una versión
        /// completa ni un parcial conocido. Para cada firma con SHA256:
        ///
        ///  1. Se toma Path.GetFileName(Ruta) del archivo declarado (p.ej. "Goldleaf.nro").
        ///  2. Se busca ese nombre en el índice ya construido de /switch/ (sin volver a
        ///     recorrer disco).
        ///  3. Solo si existe un candidato se calcula su SHA256 real.
        ///  4. Si el SHA256 coincide con el de la firma, el módulo queda identificado
        ///     con esa versión y se marca como instalado en ubicación no estándar.
        ///
        /// Si hay más de un candidato válido (mismo SHA coincidente) se marca
        /// TieneMultiplesUbicaciones = true, pero NUNCA se decide cuál usar/borrar:
        /// se toma el primero solo a efectos de mostrar una ruta física en la UI.
        /// No aplica a módulos con firmas de múltiples archivos (solo archivo único),
        /// para mantener la búsqueda simple y conservadora en esta primera fase.
        /// </summary>
        private (string Version, string RutaEncontrada, bool MultiplesUbicaciones)? BuscarPorUbicacionAlternativa(
            string rutaRaizSD, ModuloConfig modulo, Dictionary<string, List<string>> indiceHomebrewSwitch)
        {
            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count != 1)
                    continue; // conservador: solo firmas de un único archivo

                var archivoFirma = firma.Archivos[0];
                if (string.IsNullOrWhiteSpace(archivoFirma?.SHA256) || string.IsNullOrWhiteSpace(archivoFirma.Ruta))
                    continue;

                // Solo tiene sentido buscar alternativas para rutas ya declaradas bajo /switch/.
                string rutaNormalizada = NormalizarRuta(archivoFirma.Ruta);
                if (!rutaNormalizada.StartsWith(CarpetaHomebrewSwitch + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Si la ruta estándar ya existe físicamente, no hace falta buscar alternativa.
                string rutaEstandarRelativa = rutaNormalizada.Replace('/', Path.DirectorySeparatorChar);
                string rutaEstandarCompleta = Path.Combine(rutaRaizSD, rutaEstandarRelativa);
                if (File.Exists(rutaEstandarCompleta))
                    continue;

                string nombreArchivo = Path.GetFileName(rutaNormalizada);
                if (string.IsNullOrWhiteSpace(nombreArchivo)) continue;
                if (!indiceHomebrewSwitch.TryGetValue(nombreArchivo, out var candidatos) || candidatos.Count == 0)
                    continue;

                string? primeraCoincidencia = null;
                int coincidenciasValidas = 0;

                foreach (var candidato in candidatos)
                {
                    string hashCandidato = _shaTool.ObtenerHashArchivo(candidato);
                    if (hashCandidato == "archivo_no_encontrado" || hashCandidato == "error_lectura")
                        continue;

                    if (!hashCandidato.Equals(archivoFirma.SHA256, StringComparison.OrdinalIgnoreCase))
                        continue; // nombre coincide pero SHA no: no se asume identidad por esta vía

                    coincidenciasValidas++;
                    primeraCoincidencia ??= candidato;
                }

                if (primeraCoincidencia != null)
                {
                    return (firma.Version, primeraCoincidencia, coincidenciasValidas > 1);
                }
            }

            return null;
        }

        private static bool TieneAlgunSha(FirmaDeteccion firma)
            => firma.Archivos.Exists(a => !string.IsNullOrWhiteSpace(a?.SHA256));

        private bool FirmaIdentificadaPorSha(string rutaRaizSD, FirmaDeteccion firma)
        {
            foreach (var archivoFirma in firma.Archivos)
            {
                if (string.IsNullOrWhiteSpace(archivoFirma.SHA256))
                    continue;

                string rutaOriginal = archivoFirma.Ruta ?? string.Empty;
                string rutaRelativa = rutaOriginal.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                string hashActual = _shaTool.ObtenerHashArchivo(rutaCompleta);

                if (hashActual == "archivo_no_encontrado" ||
                    hashActual == "error_lectura" ||
                    !hashActual.Equals(archivoFirma.SHA256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private static List<string> ArchivosFaltantesDe(string rutaRaizSD, FirmaDeteccion firma)
        {
            var archivosFaltantes = new List<string>();

            foreach (var archivoFirma in firma.Archivos)
            {
                string rutaOriginal = archivoFirma.Ruta ?? string.Empty;
                string rutaRelativa = rutaOriginal.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                if (!ExisteRuta(rutaCompleta, rutaOriginal))
                    archivosFaltantes.Add(rutaOriginal);
            }

            return archivosFaltantes;
        }

        /// <summary>
        /// Una firma sin SHA256 solo aporta evidencia de IDENTIDAD suficiente si incluye
        /// al menos una ruta EXCLUSIVA de este módulo dentro del catálogo actual (según
        /// el mapa dinámico de exclusividad, calculado sobre TODAS las rutas declaradas
        /// en FirmasDeteccion, con o sin SHA). Sin esto, la mera presencia de archivos
        /// genéricos no demuestra que el módulo esté presente.
        /// </summary>
        private static bool TieneIdentidadEstructural(
            FirmaDeteccion firma, ModuloConfig modulo, Dictionary<string, HashSet<string>>? mapaExclusividad)
        {
            if (mapaExclusividad == null) return false;

            foreach (var archivo in firma.Archivos)
            {
                if (string.IsNullOrWhiteSpace(archivo?.Ruta)) continue;

                string rutaNormalizada = NormalizarRuta(archivo.Ruta);
                if (mapaExclusividad.TryGetValue(rutaNormalizada, out var ids) &&
                    ids.Count == 1 && ids.Contains(modulo.Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Evalúa si existe una ruta exclusiva de <paramref name="modulo"/> cuyo archivo
        /// físico existe pero cuyo SHA256 no coincide con ninguno de los conocidos para
        /// esa misma ruta en ninguna versión del módulo. Cada ruta se comprueba una sola
        /// vez (sin recalcular SHA por versión) y solo se calcula el SHA si el archivo
        /// existe. No recorre carpetas ni hace búsqueda recursiva: solo evalúa las rutas
        /// ya declaradas en FirmasDeteccion.
        /// </summary>
        private bool TieneEvidenciaVersionDesconocida(
            string rutaRaizSD, ModuloConfig modulo, Dictionary<string, HashSet<string>> mapaExclusividad)
        {
            // rutaNormalizada → conjunto de SHA256 conocidos (de cualquier versión del módulo) para esa ruta.
            var shasConocidosPorRuta = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null) continue;
                foreach (var archivo in firma.Archivos)
                {
                    if (string.IsNullOrWhiteSpace(archivo?.SHA256) || string.IsNullOrWhiteSpace(archivo.Ruta))
                        continue;

                    string rutaNormalizada = NormalizarRuta(archivo.Ruta);
                    if (!shasConocidosPorRuta.TryGetValue(rutaNormalizada, out var shas))
                    {
                        shas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        shasConocidosPorRuta[rutaNormalizada] = shas;
                    }
                    shas.Add(archivo.SHA256);
                }
            }

            foreach (var (rutaNormalizada, shasConocidos) in shasConocidosPorRuta)
            {
                // Solo rutas exclusivas de este módulo en el catálogo actual.
                if (!mapaExclusividad.TryGetValue(rutaNormalizada, out var idsModulos))
                    continue;
                if (idsModulos.Count != 1 || !idsModulos.Contains(modulo.Id))
                    continue;

                string rutaRelativa = rutaNormalizada.Replace('/', Path.DirectorySeparatorChar);
                string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                if (!File.Exists(rutaCompleta))
                    continue;

                string hashActual = _shaTool.ObtenerHashArchivo(rutaCompleta);
                if (hashActual == "archivo_no_encontrado" || hashActual == "error_lectura")
                    continue;

                if (!shasConocidos.Contains(hashActual))
                    return true;
            }

            return false;
        }

        public string DeterminarVersionInstalada(string rutaRaizSD, ModuloConfig modulo)
        {
            return DeterminarEstadoInstalacion(rutaRaizSD, modulo).Version;
        }

        private static bool ExisteRuta(string rutaCompleta, string rutaOriginal)
        {
            if (rutaOriginal.EndsWith("/", StringComparison.Ordinal) ||
                rutaOriginal.EndsWith("\\", StringComparison.Ordinal))
            {
                return Directory.Exists(rutaCompleta);
            }

            return File.Exists(rutaCompleta) || Directory.Exists(rutaCompleta);
        }
    }
}
