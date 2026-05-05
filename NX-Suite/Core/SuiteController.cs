using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public class SuiteController : ISuiteController
    {
        private readonly GistParser _gistParser;
        private readonly GestorCache _gestorCache;
        private readonly DetectorVersionesLogic _detectorVersiones;
        private readonly ReglasLogic _motorReglas;
        private readonly UninstallLogic _motorDesinstalacion;
        private readonly EscanerDiscos _escanerDiscos;
        private readonly ValidadorConfiguracion _validadorConfig = new();

        public SuiteController(
            GestorCache gestorCache,
            GistParser? gistParser = null,
            DetectorVersionesLogic? detectorVersiones = null,
            ReglasLogic? motorReglas = null,
            UninstallLogic? motorDesinstalacion = null,
            EscanerDiscos? escanerDiscos = null)
        {
            _gestorCache         = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
            _gistParser          = gistParser ?? new GistParser(_gestorCache);
            _detectorVersiones   = detectorVersiones ?? new DetectorVersionesLogic();
            _motorReglas         = motorReglas ?? new ReglasLogic();
            _motorDesinstalacion = motorDesinstalacion ?? new UninstallLogic();
            _escanerDiscos       = escanerDiscos ?? new EscanerDiscos();
        }

        public async Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD)
            => await SincronizarTodoAsync(urlGist, letraSD, CancellationToken.None);

        public async Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var datosGist = await _gistParser.ObtenerTodoElGistAsync(urlGist);
            if (datosGist == null) return null;

            datosGist.Modulos ??= new List<ModuloConfig>();

            _gestorCache.ActualizarEstadoCache(datosGist.Modulos);
            ActualizarEstadosInstalados(datosGist.Modulos, letraSD);

            cancellationToken.ThrowIfCancellationRequested();
            return datosGist;
        }

        public async Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync()
            => await Task.Run(() => _escanerDiscos.ObtenerUnidadesRemovibles());

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
        {
            var info = new InfoPanelDerecho();
            if (unidad == null) return info;

            info.Capacidad = unidad.CapacidadTotal + " GB";
            info.Formato   = unidad.Formato;

            // ── Serial: leer desde atmosphere/automatic_backups/*_BISKEYS.bin o *_PRODINFO.bin ──
            info.Serial = LeerSerialDesdeBackups(unidad.Letra);

            // ── Versión Atmosphere: detección por catálogo + fallback desde package3 ──
            string? versionDetectada = null;

            var modulosAtmos = modulos?.FindAll(m =>
                m.Etiquetas != null &&
                m.Etiquetas.Any(t => string.Equals(t, "atmosphere", StringComparison.OrdinalIgnoreCase)));

            if (modulosAtmos != null)
            {
                foreach (var moduloAtmos in modulosAtmos)
                {
                    var ver = _detectorVersiones.DeterminarVersionInstalada(unidad.Letra, moduloAtmos);
                    if (!string.IsNullOrWhiteSpace(ver) &&
                        ver != "No instalado" && ver != "Desconocido")
                    {
                        versionDetectada = ver;
                        break;
                    }
                }
            }

            // Fallback: leer version desde atmosphere/package3
            if (versionDetectada == null)
                versionDetectada = LeerVersionAtmosphereDesdeSD(unidad.Letra);

            info.VersionAtmos = versionDetectada ?? "Desconocido";

            return info;
        }

        /// <summary>
        /// Lee el serial de la Nintendo Switch desde los volcados automáticos de Atmosphere.
        /// Busca archivos *_BISKEYS.bin o *_PRODINFO.bin en atmosphere/automatic_backups/.
        /// El prefijo del nombre de archivo (antes del primer '_') es el serial.
        /// </summary>
        private static string LeerSerialDesdeBackups(string letraSD)
        {
            try
            {
                string rutaBackups = Path.Combine(letraSD, "atmosphere", "automatic_backups");
                if (!Directory.Exists(rutaBackups))
                    return "Desconocido";

                string[] patrones = { "*_BISKEYS.bin", "*_PRODINFO.bin" };
                foreach (string patron in patrones)
                {
                    var archivos = Directory.GetFiles(rutaBackups, patron);
                    foreach (string archivo in archivos)
                    {
                        string nombre = Path.GetFileNameWithoutExtension(archivo);
                        // El serial es todo antes del último '_BISKEYS' o '_PRODINFO'
                        int idx = nombre.LastIndexOf('_');
                        if (idx > 0)
                        {
                            string serial = nombre[..idx];
                            if (!string.IsNullOrWhiteSpace(serial))
                                return serial;
                        }
                    }
                }
            }
            catch { }
            return "Desconocido";
        }

        /// <summary>
        /// Fallback: busca una cadena de versión semántica (X.Y.Z) dentro de los
        /// primeros bytes del binario atmosphere/package3.
        /// </summary>
        private static string? LeerVersionAtmosphereDesdeSD(string letraSD)
        {
            try
            {
                string rutaPackage = Path.Combine(letraSD, "atmosphere", "package3");
                if (!File.Exists(rutaPackage))
                    return null;

                byte[] buffer = new byte[0x4000];
                using var fs = new FileStream(rutaPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
                int leidos = fs.Read(buffer, 0, buffer.Length);

                string contenido = Encoding.ASCII.GetString(buffer, 0, leidos);
                var match = Regex.Match(contenido, @"\b(\d+\.\d+\.\d+)\b");
                if (match.Success)
                    return match.Value;
            }
            catch { }
            return null;
        }

        public async Task<Resultado> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
            => await InstalarModuloAsync(modulo, letraSD, progreso, CancellationToken.None);

        public async Task<Resultado> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso, CancellationToken ct)
        {
            if (modulo == null || modulo.Versiones == null || modulo.Versiones.Count == 0)
                return Resultado.Error("El módulo no tiene versiones instalables.");

            // Módulos de configuración: usar la versión compatible con las dependencias instaladas.
            // Así REINSTALAR en hekate_ipl_ini v1.0 no ejecuta el pipeline de v2.0.
            var versionAInstalar = (modulo.EsConfiguracion ? modulo.VersionCompatibleSeleccionada : null)
                                   ?? modulo.Versiones[0];

            var resultado = await _motorReglas.EjecutarPipelineAsync(
                versionAInstalar.PipelineInstalacion, letraSD, progreso, ct);

            // Si el módulo trae configuración de Hekate, escribirla en la SD
            if (resultado.Exito && !string.IsNullOrWhiteSpace(modulo.HekateLaunchConfig))
            {
                try
                {
                    string rutaIni = System.IO.Path.Combine(letraSD, "bootloader", "hekate_ipl.ini");
                    string? dir    = System.IO.Path.GetDirectoryName(rutaIni);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);
                    await System.IO.File.WriteAllTextAsync(rutaIni, modulo.HekateLaunchConfig);
                }
                catch (Exception ex)
                {
                    // No abortamos la instalación por esto, pero lo notificamos
                    return Resultado.Error($"Instalado, pero no se pudo escribir hekate_ipl.ini: {ex.Message}");
                }
            }

            return resultado;
        }

        public async Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
        {
            if (modulo == null) return false;

            // ── 1. Eliminar archivos declarados (RutasDesinstalacion o FirmasDeteccion) ──
            List<string> rutas = modulo.RutasDesinstalacion?.Count > 0
                ? modulo.RutasDesinstalacion
                : (modulo.FirmasDeteccion ?? Enumerable.Empty<FirmaDeteccion>())
                      .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
                      .Select(a => a.Ruta)
                      .Where(r => !string.IsNullOrWhiteSpace(r))
                      .Distinct()
                      .ToList();

            bool exito = false;
            if (rutas.Count > 0)
                exito = await _motorDesinstalacion.DesinstalarAsync(rutas, letraSD);

            // ── 2. Ejecutar PipelineDesinstalacion de la versión instalada (si existe) ──
            // Esto permite limpiar carpetas vacías específicas de esa versión de forma segura.
            if (!string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                modulo.VersionInstalada is not ("No detectado" or "No instalado") &&
                modulo.Versiones?.Count > 0)
            {
                var verInstalada = modulo.Versiones.FirstOrDefault(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase));

                if (verInstalada?.PipelineDesinstalacion?.Count > 0)
                {
                    var resultadoPipeline = await _motorReglas.EjecutarPipelineAsync(
                        verInstalada.PipelineDesinstalacion, letraSD);

                    // El pipeline complementario no invalida el éxito principal
                    if (!exito) exito = resultadoPipeline.Exito;
                }
            }

            return exito;
        }

        public void LimpiarCacheModulo(ModuloConfig modulo)
        {
            if (!_gestorCache.BorrarCacheModulo(modulo))
                throw new InvalidOperationException(
                    "No se pudieron borrar todos los archivos de caché. Pueden estar en uso.");
        }

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
            => _gestorCache.ActualizarEstadoCache(catalogo);

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
            => FiltroLogic.FiltrarPorEtiqueta(modulos, etiqueta);

        public IEnumerable<ModuloConfig> FiltrarPorTexto(IEnumerable<ModuloConfig> modulos, string busqueda)
            => FiltroLogic.FiltrarPorTexto(modulos, busqueda);

        public void RefrescarEstadosSinRed(IEnumerable<ModuloConfig> modulos, string letraSD)
        {
            if (modulos == null) return;
            _gestorCache.ActualizarEstadoCache(modulos);
            ActualizarEstadosInstalados(modulos, letraSD);
        }

        // ── Lógica privada ───────────────────────────────────────────────

        private void ActualizarEstadosInstalados(IEnumerable<ModuloConfig> modulos, string letraSD)
        {
            var lista = modulos?.ToList();
            if (lista == null) return;

            // Sin SD: reset rápido para todos los módulos
            if (string.IsNullOrWhiteSpace(letraSD))
            {
                foreach (var m in lista)
                {
                    if (m == null) continue;
                    m.VersionInstalada             = "Sin SD conectada";
                    m.EstadoSd                     = EstadoSdModulo.NoInstalado;
                    m.EstadoActualizacion          = EstadoActualizacionModulo.SinCambios;
                    m.HallazgosConfig              = new List<HallazgoConfig>();
                    m.VersionCompatibleSeleccionada = null;
                    m.AccionRapida = m.TieneCache
                        ? AccionRapidaModulo.EliminarCache
                        : AccionRapidaModulo.DescargarCache;
                }
                return;
            }

            // PASO 1: detectar módulos estándar primero (construyen el mapa de versiones)
            foreach (var modulo in lista)
            {
                if (modulo == null || modulo.EsConfiguracion) continue;
                ProcesarModuloEstandar(modulo, letraSD);
            }

            // Mapa { id → versionInstalada } para resolver VersionDependencia
            var versionesInstaladas = lista
                .Where(m => m != null)
                .ToDictionary(m => m.Id, m => m.VersionInstalada, StringComparer.OrdinalIgnoreCase);

            // PASO 2: módulos de configuración usando el mapa de dependencias
            foreach (var modulo in lista)
            {
                if (modulo == null || !modulo.EsConfiguracion) continue;
                ProcesarModuloConfiguracion(modulo, letraSD, versionesInstaladas);
            }
        }

        /// <summary>Procesa detección y estado para módulos estándar (no configuracion).</summary>
        private void ProcesarModuloEstandar(ModuloConfig modulo, string letraSD)
        {
            var (version, estadoSd) = _detectorVersiones.DeterminarEstadoInstalacion(letraSD, modulo);

            var versionDetectada = modulo.Versiones?.FirstOrDefault(v =>
                string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));

            if (estadoSd == EstadoSdModulo.Instalado && versionDetectada?.ReglasConfig != null)
            {
                modulo.HallazgosConfig = _validadorConfig.ValidarAsync(letraSD, versionDetectada.ReglasConfig).GetAwaiter().GetResult();
                if (modulo.HallazgosConfig.Exists(h => h.EsCritico))
                    estadoSd = EstadoSdModulo.ParcialmenteInstalado;
            }
            else
            {
                modulo.HallazgosConfig = new List<HallazgoConfig>();
            }

            modulo.VersionInstalada             = version;
            modulo.EstadoSd                     = estadoSd;
            modulo.VersionCompatibleSeleccionada = null;
            modulo.EstadoActualizacion          = DeterminarEstadoActualizacion(modulo, version, estadoSd);
            modulo.AccionRapida                 = DeterminarAccionRapida(modulo);
        }

        /// <summary>
        /// Procesa detección y estado para módulos de configuración.
        /// Selecciona la versión compatible con las dependencias instaladas y
        /// valida el contenido del archivo con las reglas de ESA versión.
        /// </summary>
        private void ProcesarModuloConfiguracion(ModuloConfig modulo, string letraSD,
            Dictionary<string, string> versionesInstaladas)
        {
            var (_, estadoSd) = _detectorVersiones.DeterminarEstadoInstalacion(letraSD, modulo);

            // Elegir la versión compatible más alta según dependencias instaladas
            var versionCompatible = SeleccionarVersionCompatible(modulo, versionesInstaladas);
            modulo.VersionCompatibleSeleccionada = versionCompatible;

            if (estadoSd == EstadoSdModulo.Instalado && versionCompatible?.ReglasConfig != null)
            {
                modulo.HallazgosConfig = _validadorConfig.ValidarAsync(letraSD, versionCompatible.ReglasConfig).GetAwaiter().GetResult();
                if (modulo.HallazgosConfig.Exists(h => h.EsCritico))
                    estadoSd = EstadoSdModulo.ParcialmenteInstalado;
            }
            else
            {
                modulo.HallazgosConfig = new List<HallazgoConfig>();
            }

            // Mostrar la versión compatible en el chip de la tarjeta
            modulo.VersionInstalada = estadoSd != EstadoSdModulo.NoInstalado && versionCompatible != null
                ? versionCompatible.Version
                : "No instalado";

            modulo.EstadoSd           = estadoSd;
            modulo.EstadoActualizacion = DeterminarEstadoActualizacion(modulo, modulo.VersionInstalada, estadoSd);
            modulo.AccionRapida       = DeterminarAccionRapida(modulo);
        }

        /// <summary>
        /// Selecciona la versión instalable más alta cuyas VersionDependencia
        /// son satisfechas por las versiones instaladas actuales.
        /// Fallback: primera versión no SoloDeteccion si ninguna cumple las restricciones.
        /// </summary>
        private static ModuloVersion? SeleccionarVersionCompatible(
            ModuloConfig modulo, Dictionary<string, string> versionesInstaladas)
        {
            if (modulo.Versiones == null || modulo.Versiones.Count == 0) return null;

            ModuloVersion? mejor    = null;
            Version?       mejorVer = null;

            foreach (var ver in modulo.Versiones)
            {
                if (ver.SoloDeteccion) continue;
                if (!SatisfaceVersionDependencia(ver.VersionDependencia, versionesInstaladas)) continue;

                if (Version.TryParse(ver.Version, out var v) && (mejorVer == null || v > mejorVer))
                {
                    mejor    = ver;
                    mejorVer = v;
                }
            }

            // Fallback: la primera versión no SoloDeteccion (sin restricciones de dependencia)
            return mejor ?? modulo.Versiones.FirstOrDefault(v => !v.SoloDeteccion);
        }

        /// <summary>
        /// Verifica que todas las restricciones de VersionDependencia estén satisfechas
        /// por las versiones instaladas actualmente.
        /// </summary>
        private static bool SatisfaceVersionDependencia(
            Dictionary<string, string>? deps, Dictionary<string, string> instaladas)
        {
            if (deps == null || deps.Count == 0) return true;

            foreach (var (depId, verMinStr) in deps)
            {
                if (!instaladas.TryGetValue(depId, out var verInstalada) ||
                    string.IsNullOrWhiteSpace(verInstalada) ||
                    verInstalada is "No detectado" or "No instalado" or "Sin SD conectada" or "Desconocido")
                    return false;

                // Si las versiones no son semánticas, basta con que la dependencia esté presente
                if (!Version.TryParse(verMinStr, out var minReq)) continue;
                if (!Version.TryParse(verInstalada, out var actual)) return false;
                if (actual < minReq) return false;
            }
            return true;
        }

        private static EstadoActualizacionModulo DeterminarEstadoActualizacion(ModuloConfig modulo, string version, EstadoSdModulo estadoSd)
        {
            if (estadoSd == EstadoSdModulo.NoInstalado)
                return EstadoActualizacionModulo.SinCambios;

            if (estadoSd == EstadoSdModulo.ParcialmenteInstalado)
                return EstadoActualizacionModulo.Incompatible;

            string versionRemota = modulo.Versiones?.Count > 0
                ? modulo.Versiones[0].Version
                : string.Empty;

            if (string.IsNullOrWhiteSpace(versionRemota))
                return EstadoActualizacionModulo.SinCambios;

            return string.Equals(versionRemota, version, StringComparison.OrdinalIgnoreCase)
                ? EstadoActualizacionModulo.SinCambios
                : EstadoActualizacionModulo.NuevaVersion;
        }

        private static AccionRapidaModulo DeterminarAccionRapida(ModuloConfig modulo)
        {
            if (modulo.EstadoSd == EstadoSdModulo.NoInstalado)
                return AccionRapidaModulo.Instalar;

            // Módulos de configuración (ini, txt, hosts): nunca muestran "REINSTALAR".
            // La acción es siempre sobrescribir el archivo, no descargar software.
            // ParcialmenteInstalado = archivo existe con valores incorrectos → "INSTALAR"
            // Instalado correctamente                                         → "ELIMINAR"
            if (modulo.EsConfiguracion)
                return modulo.EstadoSd == EstadoSdModulo.ParcialmenteInstalado
                    ? AccionRapidaModulo.Instalar
                    : AccionRapidaModulo.Eliminar;

            if (modulo.EstadoSd == EstadoSdModulo.ParcialmenteInstalado)
                return AccionRapidaModulo.Reinstalar;

            if (modulo.EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion ||
                modulo.EstadoActualizacion == EstadoActualizacionModulo.Incompatible)
                return AccionRapidaModulo.Actualizar;

            return AccionRapidaModulo.Eliminar;
        }
    }
}