using NX_Suite.Hardware;
using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public class SuiteControllerFacade : ISuiteController
    {
        private readonly SuiteController _inner;

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

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
            => _inner.ActualizarEstadoCacheCatalogo(catalogo);

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
            => _inner.FiltrarPorEtiqueta(modulos, etiqueta);

        public IEnumerable<ModuloConfig> FiltrarPorTexto(IEnumerable<ModuloConfig> modulos, string busqueda)
            => _inner.FiltrarPorTexto(modulos, busqueda);

        public Task RefrescarEstadosSinRedAsync(IEnumerable<ModuloConfig> modulos, string letraSD)
            => _inner.RefrescarEstadosSinRedAsync(modulos, letraSD);
    }
}