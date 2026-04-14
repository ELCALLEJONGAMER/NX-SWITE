using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.Network;

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
            GistParser gistParser = null,
            DetectorVersionesLogic detectorVersiones = null,
            ReglasLogic motorReglas = null,
            UninstallLogic motorDesinstalacion = null,
            DiskMaster diskMaster = null)
        {
            _gestorCache = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
            _gistParser = gistParser ?? new GistParser();
            _detectorVersiones = detectorVersiones ?? new DetectorVersionesLogic();
            _motorReglas = motorReglas ?? new ReglasLogic();
            _motorDesinstalacion = motorDesinstalacion ?? new UninstallLogic();
            _diskMaster = diskMaster ?? new DiskMaster();
        }

        public async Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD)
        {
            return await SincronizarTodoAsync(urlGist, letraSD, CancellationToken.None);
        }

        public async Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var datosGist = await _gistParser.ObtenerTodoElGistAsync(urlGist);
            if (datosGist == null || datosGist.Modulos.Count == 0) return null;

            _gestorCache.ActualizarEstadoCache(datosGist.Modulos);

            if (!string.IsNullOrEmpty(letraSD))
            {
                foreach (var modulo in datosGist.Modulos)
                {
                    modulo.VersionInstalada = _detectorVersiones.DeterminarVersionInstalada(letraSD, modulo);
                }
            }
            else
            {
                foreach (var modulo in datosGist.Modulos)
                {
                    modulo.VersionInstalada = "Sin SD conectada";
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return datosGist;
        }

        public async Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync()
        {
            return await Task.Run(() => _diskMaster.ObtenerUnidadesRemovibles());
        }

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
        {
            var info = new InfoPanelDerecho();
            if (unidad == null) return info;

            info.Capacidad = unidad.CapacidadTotal + " GB";
            info.Formato = unidad.Formato;
            info.Serial = unidad.Serial;

            var moduloAtmos = modulos?.Find(m => string.Equals(m.Categoria, "Atmosphere", StringComparison.OrdinalIgnoreCase));
            if (moduloAtmos != null)
            {
                info.VersionAtmos = _detectorVersiones.DeterminarVersionInstalada(unidad.Letra, moduloAtmos);
            }

            return info;
        }

        public async Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
        {
            if (modulo == null || modulo.Versiones == null || modulo.Versiones.Count == 0)
                return (false, "El módulo no tiene versiones instalables.");

            return await _motorReglas.EjecutarPipelineAsync(modulo.Versiones[0].PipelineInstalacion, letraSD, progreso);
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
            {
                throw new InvalidOperationException("No se pudieron borrar todos los archivos de caché. Pueden estar en uso.");
            }
        }

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
        {
            _gestorCache.ActualizarEstadoCache(catalogo);
        }

        public IEnumerable<ModuloConfig> FiltrarPorMundo(IEnumerable<ModuloConfig> modulos, string mundoId)
        {
            return FiltroLogic.FiltrarPorMundo(modulos, mundoId);
        }

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
        {
            return FiltroLogic.FiltrarPorEtiqueta(modulos, etiqueta);
        }
    }
}