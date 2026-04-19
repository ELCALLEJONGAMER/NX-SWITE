using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.Network;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly DiskMaster _diskMaster;

        public SuiteController(
            GestorCache gestorCache,
            GistParser? gistParser = null,
            DetectorVersionesLogic? detectorVersiones = null,
            ReglasLogic? motorReglas = null,
            UninstallLogic? motorDesinstalacion = null,
            DiskMaster? diskMaster = null)
        {
            _gestorCache         = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
            _gistParser          = gistParser ?? new GistParser();
            _detectorVersiones   = detectorVersiones ?? new DetectorVersionesLogic();
            _motorReglas         = motorReglas ?? new ReglasLogic();
            _motorDesinstalacion = motorDesinstalacion ?? new UninstallLogic();
            _diskMaster          = diskMaster ?? new DiskMaster();
        }

        public async Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD)
            => await SincronizarTodoAsync(urlGist, letraSD, CancellationToken.None);

        public async Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
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
            => await Task.Run(() => _diskMaster.ObtenerUnidadesRemovibles());

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
        {
            var info = new InfoPanelDerecho();
            if (unidad == null) return info;

            info.Capacidad = unidad.CapacidadTotal + " GB";
            info.Formato   = unidad.Formato;
            info.Serial    = unidad.Serial;

            var moduloAtmos = modulos?.Find(m =>
                m.Etiquetas != null &&
                m.Etiquetas.Any(t => string.Equals(t, "atmosphere", StringComparison.OrdinalIgnoreCase)));

            if (moduloAtmos != null)
                info.VersionAtmos = _detectorVersiones.DeterminarVersionInstalada(unidad.Letra, moduloAtmos);

            return info;
        }

        public async Task<(bool Exito, string MensajeError)> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
        {
            if (modulo == null || modulo.Versiones == null || modulo.Versiones.Count == 0)
                return (false, "El módulo no tiene versiones instalables.");

            return await _motorReglas.EjecutarPipelineAsync(
                modulo.Versiones[0].PipelineInstalacion, letraSD, progreso);
        }

        public async Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
        {
            if (modulo == null || modulo.RutasDesinstalacion == null || modulo.RutasDesinstalacion.Count == 0)
                return false;

            return await _motorDesinstalacion.DesinstalarAsync(modulo.RutasDesinstalacion, letraSD);
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

        // ── Lógica privada ───────────────────────────────────────────────

        private void ActualizarEstadosInstalados(IEnumerable<ModuloConfig> modulos, string letraSD)
        {
            if (modulos == null) return;

            foreach (var modulo in modulos)
            {
                if (modulo == null) continue;

                if (string.IsNullOrWhiteSpace(letraSD))
                {
                    modulo.VersionInstalada    = "Sin SD conectada";
                    modulo.EstadoSd            = EstadoSdModulo.NoInstalado;
                    modulo.EstadoActualizacion = EstadoActualizacionModulo.SinCambios;
                    modulo.AccionRapida        = AccionRapidaModulo.Ninguna;
                    continue;
                }

                string version             = _detectorVersiones.DeterminarVersionInstalada(letraSD, modulo);
                modulo.VersionInstalada    = version;
                modulo.EstadoSd            = DeterminarEstadoSd(version);
                modulo.EstadoActualizacion = DeterminarEstadoActualizacion(modulo, version);
                modulo.AccionRapida        = DeterminarAccionRapida(modulo);
            }
        }

        private static EstadoSdModulo DeterminarEstadoSd(string version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                string.Equals(version, "No instalado", StringComparison.OrdinalIgnoreCase))
                return EstadoSdModulo.NoInstalado;

            if (string.Equals(version, "Desconocido", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version, "Instalación No Oficial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version, "Error lectura", StringComparison.OrdinalIgnoreCase))
                return EstadoSdModulo.ParcialmenteInstalado;

            return EstadoSdModulo.Instalado;
        }

        private static EstadoActualizacionModulo DeterminarEstadoActualizacion(ModuloConfig modulo, string version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                string.Equals(version, "No instalado", StringComparison.OrdinalIgnoreCase))
                return EstadoActualizacionModulo.SinCambios;

            if (string.Equals(version, "Desconocido", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version, "Instalación No Oficial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version, "Error lectura", StringComparison.OrdinalIgnoreCase))
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

            if (modulo.EstadoSd == EstadoSdModulo.ParcialmenteInstalado)
                return AccionRapidaModulo.Reinstalar;

            if (modulo.EstadoActualizacion == EstadoActualizacionModulo.NuevaVersion ||
                modulo.EstadoActualizacion == EstadoActualizacionModulo.Incompatible)
                return AccionRapidaModulo.Actualizar;

            return AccionRapidaModulo.Eliminar;
        }
    }
}