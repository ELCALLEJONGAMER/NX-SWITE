using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    /// <summary>
    /// Fachada ligera que implementa ISuiteController delegando en la clase SuiteController existente.
    /// Esto nos permite introducir la interfaz sin tocar el archivo SuiteController.cs ahora.
    /// </summary>
    public class SuiteControllerFacade : ISuiteController
    {
        private readonly SuiteController _inner;

        public SuiteControllerFacade(SuiteController inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD)
        {
            return _inner.SincronizarTodoAsync(urlGist, letraSD);
        }

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
        {
            return _inner.ObtenerInfoPanel(unidad, modulos);
        }

        public Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
        {
            return _inner.InstalarModuloAsync(modulo, letraSD, progreso);
        }

        public Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
        {
            return _inner.DesinstalarModuloAsync(modulo, letraSD);
        }
    }
}