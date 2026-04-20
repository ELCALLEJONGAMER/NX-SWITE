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

        public Task<(bool Exito, string MensajeError)> InstalarModuloAsync(
            ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
            => _inner.InstalarModuloAsync(modulo, letraSD, progreso);

        public Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
            => _inner.DesinstalarModuloAsync(modulo, letraSD);

        public void LimpiarCacheModulo(ModuloConfig modulo)
            => _inner.LimpiarCacheModulo(modulo);

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
            => _inner.ActualizarEstadoCacheCatalogo(catalogo);

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
            => _inner.FiltrarPorEtiqueta(modulos, etiqueta);
    }
}