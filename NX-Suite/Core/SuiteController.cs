using NX_Suite.Models;
using NX_Suite.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public class SuiteController
    {
        private readonly GistParser _gistParser = new GistParser();
        private readonly GestorCache _gestorCache;
        private readonly DetectorVersionesLogic _detectorVersiones = new DetectorVersionesLogic();

        // 🏗️ MOTORES CENTRALIZADOS EN EL CEREBRO
        private readonly ReglasLogic _motorReglas = new ReglasLogic();
        private readonly UninstallLogic _motorDesinstalacion = new UninstallLogic();

        public SuiteController(GestorCache gestorCache)
        {
            _gestorCache = gestorCache;
        }

        public async Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD)
        {
            var datosGist = await _gistParser.ObtenerTodoElGistAsync(urlGist);

            if (datosGist == null || datosGist.Modulos.Count == 0)
                return null;

            _gestorCache.ActualizarEstadoCache(datosGist.Modulos);

            if (!string.IsNullOrEmpty(letraSD))
            {
                foreach (var modulo in datosGist.Modulos)
                    modulo.VersionInstalada = _detectorVersiones.DeterminarVersionInstalada(letraSD, modulo);
            }
            else
            {
                foreach (var modulo in datosGist.Modulos)
                    modulo.VersionInstalada = "Sin SD conectada";
            }

            return datosGist;
        }

        public InfoPanelDerecho ObtenerInfoPanel(NX_Suite.Hardware.SDInfo unidad, List<ModuloConfig> modulos)
        {
            var info = new InfoPanelDerecho();

            if (unidad != null)
            {
                info.Capacidad = unidad.CapacidadTotal + " GB";
                info.Formato = unidad.Formato;
                info.Serial = unidad.Serial;

                var moduloAtmos = modulos?.Find(m => m.Categoria.Equals("Atmosphere", StringComparison.OrdinalIgnoreCase));
                if (moduloAtmos != null)
                {
                    info.VersionAtmos = _detectorVersiones.DeterminarVersionInstalada(unidad.Letra, moduloAtmos);
                }
            }
            return info;
        }

        // =======================================================
        // 🚀 NUEVO: EL CEREBRO CONTROLA LA INSTALACIÓN
        // =======================================================
        public async Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
        {
            if (modulo == null || modulo.Versiones == null || modulo.Versiones.Count == 0)
                return (false, "El módulo no tiene versiones instalables.");

            // El cerebro delega la orden al músculo de reglas
            return await _motorReglas.EjecutarPipelineAsync(modulo.Versiones[0].PipelineInstalacion, letraSD, progreso);
        }

        // =======================================================
        // 🧹 NUEVO: EL CEREBRO CONTROLA EL BORRADO
        // =======================================================
        public async Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
        {
            if (modulo == null || modulo.RutasDesinstalacion == null || modulo.RutasDesinstalacion.Count == 0)
                return false;

            // El cerebro delega la orden al músculo de borrado
            return await _motorDesinstalacion.DesinstalarAsync(modulo.RutasDesinstalacion, letraSD);
        }
    }
}