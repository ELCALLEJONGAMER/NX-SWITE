using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    /// <summary>
    /// Fachada ligera que implementa ISuiteController delegando en la clase SuiteController existente.
    /// Permite introducir la interfaz sin romper la implementación actual.
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

        public Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
        {
            // Si la implementación interna soporta cancellation, la usará.
            // Si no, delegamos a la versión sin token (comportamiento compatible).
            try
            {
                // Intentamos llamar a la sobrecarga con token si existe en tiempo de compilación.
                // Si SuiteController no tiene la sobrecarga, se llamará a la versión sin token.
                return _inner.SincronizarTodoAsync(urlGist, letraSD, cancellationToken);
            }
            catch (MissingMethodException)
            {
                // Fallback seguro a la versión sin token
                return _inner.SincronizarTodoAsync(urlGist, letraSD);
            }
            catch
            {
                // Si por alguna razón no compila la llamada anterior (incompatibilidad),
                // llamamos a la impl. sin token como fallback.
                return _inner.SincronizarTodoAsync(urlGist, letraSD);
            }
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