using NX_Swite.Hardware;
using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    public class SuiteControllerFacade : ISuiteController
    {
        private readonly SuiteController _inner;

        public event Action<GistData>? GistActualizadoEnBackground
        {
            add    => _inner.GistActualizadoEnBackground += value;
            remove => _inner.GistActualizadoEnBackground -= value;
        }

        public SuiteControllerFacade(SuiteController inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD)
            => _inner.SincronizarTodoAsync(urlGist, letraSD);

        public Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
            => _inner.SincronizarTodoAsync(urlGist, letraSD, cancellationToken);

        public Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync()
            => _inner.ObtenerUnidadesRemoviblesAsync();

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
            => _inner.ObtenerInfoPanel(unidad, modulos);

        public Task<Resultado> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
            => _inner.InstalarModuloAsync(modulo, letraSD, progreso);

        public Task<Resultado> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso, CancellationToken ct)
            => _inner.InstalarModuloAsync(modulo, letraSD, progreso, ct);

        public Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
            => _inner.DesinstalarModuloAsync(modulo, letraSD);

        public void LimpiarCacheModulo(ModuloConfig modulo)
            => _inner.LimpiarCacheModulo(modulo);

        public void LimpiarTodaLaBoveda()
            => _inner.LimpiarTodaLaBoveda();

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
            => _inner.ActualizarEstadoCacheCatalogo(catalogo);

        public long ObtenerPesoCacheZips()       => _inner.ObtenerPesoCacheZips();
        public long ObtenerPesoCacheExtraccion() => _inner.ObtenerPesoCacheExtraccion();

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
            => _inner.FiltrarPorEtiqueta(modulos, etiqueta);

        public IEnumerable<ModuloConfig> FiltrarPorTexto(IEnumerable<ModuloConfig> modulos, string busqueda)
            => _inner.FiltrarPorTexto(modulos, busqueda);

        public Task RefrescarEstadosSinRedAsync(IEnumerable<ModuloConfig> modulos, string letraSD)
            => _inner.RefrescarEstadosSinRedAsync(modulos, letraSD);

        public AnalisisLimpiezaSD AnalizarLimpiezaSD(string letraSD, IEnumerable<string> protegidos)
            => _inner.AnalizarLimpiezaSD(letraSD, protegidos);

        public Task<Resultado> LimpiarMicroSDAsync(
            string letraSD,
            IEnumerable<string> protegidos,
            IProgress<EstadoProgreso>? progreso,
            CancellationToken ct)
            => _inner.LimpiarMicroSDAsync(letraSD, protegidos, progreso, ct);
    }
}