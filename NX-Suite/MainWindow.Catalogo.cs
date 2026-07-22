using NX_Swite.Core;
using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Tarjetas del cat�logo: hover, click y acciones r�pidas
    /// (instalar, actualizar, reinstalar, eliminar, limpiar cach�).
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Sem�foro que garantiza acceso exclusivo a la SD.
        /// Solo una instalaci�n o eliminaci�n puede escribir en la tarjeta a la vez;
        /// el resto espera en cola sin bloquear el hilo de UI.
        /// </summary>
        private readonly SemaphoreSlim _semaforoSD = new(1, 1);
        private void Catalogo_HoverTarjeta(object sender, MouseEventArgs e)
        {
            if (_cargandoCatalogoInicial) return;
            Servicios.Sonidos.Reproducir(EventoSonido.Hover);
        }

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo)
                AbrirDetalleModulo(modulo);
        }

        private async void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

            switch (modulo.AccionRapida)
            {
                case AccionRapidaModulo.Instalar:
                case AccionRapidaModulo.Actualizar:
                case AccionRapidaModulo.Reinstalar:
                case AccionRapidaModulo.Reparar:
                    // No se reproduce Click � Instalar sound lo cubre
                    if (string.IsNullOrEmpty(letraSD))
                    {
                        Dialogos.Advertencia("No hay ninguna SD seleccionada.");
                        return;
                    }
                    await EjecutarInstalacionRapidaAsync(modulo, letraSD);
                    break;

                case AccionRapidaModulo.Eliminar:
                    Servicios.Sonidos.Reproducir(EventoSonido.Click);
                    if (string.IsNullOrEmpty(letraSD)) return;
                    await EjecutarEliminacionRapidaAsync(modulo, letraSD);
                    break;

                case AccionRapidaModulo.DescargarCache:
                    // Descarga a cache local sin instalar en SD
                    await EjecutarInstalacionRapidaAsync(modulo, string.Empty);
                    break;

                case AccionRapidaModulo.EliminarCache:
                    Servicios.Sonidos.Reproducir(EventoSonido.Click);
                    ConfirmarLimpiezaCache(modulo);
                    break;

                default:
                    Servicios.Sonidos.Reproducir(EventoSonido.Click);
                    ConfirmarLimpiezaCache(modulo);
                    break;
            }
        }

        private async Task EjecutarInstalacionRapidaAsync(
            ModuloConfig modulo,
            string letraSD,
            bool resolverDependencias = true)
        {
            // ?? Resoluci�n de dependencias (no toca la SD, no necesita el sem�foro) ??
            if (resolverDependencias
                && !string.IsNullOrEmpty(letraSD)
                && modulo.Dependencias is { Count: > 0 }
                && _catalogoModulos != null)
            {
                // Refrescar solo el m�dulo y sus dependencias directas antes de
                // evaluar el estado, para no usar datos cacheados si el usuario
                // borr� archivos de la SD manualmente entre operaciones.
                var modulosImplicados = _catalogoModulos
                    .Where(m => m.Id == modulo.Id
                             || (modulo.Dependencias?.Contains(m.Id) ?? false))
                    .ToList();
                await _cerebro.RefrescarEstadosSinRedAsync(modulosImplicados, letraSD);

                var deps = AnalizadorDependencias.AnalizarTransitivo(modulo, _catalogoModulos);
                var depsConAccion = deps.Where(d => d.Estado != EstadoDependencia.OK).ToList();

                if (depsConAccion.Any())
                {
                    bool exito = await MostrarCrafteoYInstalarAsync(
                        modulo, depsConAccion, letraSD);

                    if (exito)
                    {
                        // Refrescar estado SD del principal + todas las deps del overlay
                        // antes de volver al cat�logo, para que las tarjetas reflejen
                        // el estado real de la SD sin esperar a la sincronizaci�n completa.
                        if (_catalogoModulos != null && !string.IsNullOrEmpty(letraSD))
                        {
                            var modulosParaRefrescar = _catalogoModulos
                                .Where(m => m.Id == modulo.Id ||
                                            (_depsActuales?.Any(d => d.Id == m.Id) ?? false))
                                .ToList();

                            if (modulosParaRefrescar.Count > 0)
                            {
                                OverlayRefrescandoCatalogo.Visibility = Visibility.Visible;
                                try
                                {
                                    await _cerebro.RefrescarEstadosSinRedAsync(
                                        modulosParaRefrescar, letraSD);
                                }
                                finally
                                {
                                    OverlayRefrescandoCatalogo.Visibility = Visibility.Collapsed;
                                }
                            }
                        }

                        if (_catalogoModulos != null)
                            _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                        await ActualizarListaUnidadesAsync();
                        RefrescarVistaActual();
                    }
                    return;
                }
            }

            // Refrescar el estado del m�dulo antes de instalar para no usar datos
            // cacheados cuando no tiene dependencias (o todas ya estaban OK).
            if (!string.IsNullOrEmpty(letraSD) && _catalogoModulos != null)
                await _cerebro.RefrescarEstadosSinRedAsync(new[] { modulo }, letraSD);

            const double VelocidadBase = 0.0018;
            const double VelocidadMax  = 0.032;

            // ? Feedback visual INMEDIATO: el m�dulo entra en cola (verde, sin relleno a�n)
            modulo.EstaInstalando      = true;
            modulo.ProgresoInstalacion = 0.0;

            Servicios.Sonidos.Reproducir(EventoSonido.Instalar);
            var itemQueue = Servicios.Cola.AgregarItem($"En cola: {modulo.Nombre}");

            // ? Esperar turno en la cola serial de la SD
            try
            {
                await _semaforoSD.WaitAsync(itemQueue.Token);
            }
            catch (OperationCanceledException)
            {
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                Servicios.Cola.CancelarItem(itemQueue);
                return;
            }

            // ? Turno obtenido: comenzar instalaci�n real
            Servicios.Cola.ActualizarItem(itemQueue, 0, $"Instalando {modulo.Nombre}...");

            double targetProgress = 0.0;
            double velocidad      = VelocidadBase;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += (_, _) =>
            {
                double diff = targetProgress - modulo.ProgresoInstalacion;

                if (diff <= 0.0005)
                {
                    modulo.ProgresoInstalacion = targetProgress;
                    return;
                }

                double vObjetivo = Math.Clamp(diff * 0.18, VelocidadBase, VelocidadMax);
                velocidad += (vObjetivo - velocidad) * 0.10;
                modulo.ProgresoInstalacion = Math.Min(targetProgress, modulo.ProgresoInstalacion + velocidad);
            };
            timer.Start();

            var progreso = new Progress<EstadoProgreso>(estado =>
            {
                // Nunca retroceder: solo avanzar hacia porcentajes mayores
                targetProgress = Math.Max(targetProgress, estado.Porcentaje / 100.0);
                Servicios.Cola.ActualizarItem(itemQueue, estado.Porcentaje, estado.TareaActual);
            });

            try
            {
                var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso, itemQueue.Token);

                // Llevar al 100% y esperar que el relleno llegue visualmente (m�x 2s)
                targetProgress = 1.0;
                var limite = DateTime.Now.AddSeconds(2);
                while (modulo.ProgresoInstalacion < 0.995 && DateTime.Now < limite)
                    await Task.Delay(16);

                timer.Stop();
                modulo.ProgresoInstalacion = 1.0;
                await Task.Delay(300);

                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;

                if (_catalogoModulos != null)
                {
                    if (!string.IsNullOrEmpty(letraSD))
                        await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, letraSD);
                    else
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                }

                if (resolverDependencias)
                {
                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                }

                if (resultado.Exito && EsModuloAtmosphere(modulo))
                    RefrescarVersionAtmos();

                if (!resultado.Exito)
                {
                    Servicios.Sonidos.Reproducir(EventoSonido.Error);
                    Servicios.Cola.ErrorItem(itemQueue, resultado.MensajeError);
                    Dialogos.Error($"Error:\n{resultado.MensajeError}", "Fallo");
                }
                else
                {
                    Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                    Servicios.Cola.CompletarItem(itemQueue);
                }
            }
            catch (OperationCanceledException)
            {
                timer.Stop();
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                Servicios.Cola.CancelarItem(itemQueue);
            }
            catch (Exception ex)
            {
                timer.Stop();
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                Servicios.Cola.ErrorItem(itemQueue, ex.Message);
                Dialogos.Error(ex.Message);
            }
            finally
            {
                // ? Liberar la SD para el siguiente en cola
                _semaforoSD.Release();
            }
        }

        private async Task EjecutarEliminacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            // ? Feedback visual inmediato
            modulo.EstaEliminando = true;

            var itemQueue = Servicios.Cola.AgregarItem($"En cola (eliminar): {modulo.Nombre}");
            Servicios.Cola.ActualizarItem(itemQueue, 0, "Esperando turno...");

            // ? Esperar turno en la cola serial de la SD
            try
            {
                await _semaforoSD.WaitAsync(itemQueue.Token);
            }
            catch (OperationCanceledException)
            {
                modulo.EstaEliminando = false;
                Servicios.Cola.CancelarItem(itemQueue);
                return;
            }

            Servicios.Cola.ActualizarItem(itemQueue, 0, "Eliminando archivos de la SD...");

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(modulo, letraSD);

                // Mantener el rojo visible un breve instante para que se perciba el cambio
                await Task.Delay(400);
                modulo.EstaEliminando = false;

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                // Si el m�dulo desinstalado afecta a Atmosphere, refrescar su versi�n en el panel
                if (exito && EsModuloAtmosphere(modulo))
                    RefrescarVersionAtmos();

                if (!exito)
                {
                    Servicios.Cola.ErrorItem(itemQueue, "Error al eliminar algunos archivos");
                    Dialogos.Advertencia("Hubo un error al eliminar algunos archivos.");
                }
                else
                {
                    Servicios.Cola.CompletarItem(itemQueue);
                }
            }
            catch (Exception ex)
            {
                modulo.EstaEliminando = false;
                Servicios.Cola.ErrorItem(itemQueue, ex.Message);
                Dialogos.Error(ex.Message);
            }
            finally
            {
                // ? Liberar la SD para el siguiente en cola
                _semaforoSD.Release();
            }
        }

        private void ConfirmarLimpiezaCache(ModuloConfig modulo)
        {
            if (!Dialogos.Confirmar($"�Eliminar cach� local de {modulo.Nombre}?", "Limpiar Cach�"))
                return;

            try
            {
                _cerebro.LimpiarCacheModulo(modulo);
                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
            }
            catch (Exception ex)
            {
                Dialogos.Error(ex.Message);
            }
        }

        /// <summary>
        /// Devuelve true si el m�dulo tiene la etiqueta "atmosphere" o "atmosphere_mod".
        /// </summary>
        private static bool EsModuloAtmosphere(ModuloConfig modulo) =>
            modulo.Etiquetas != null &&
            modulo.Etiquetas.Any(t =>
                string.Equals(t, "atmosphere",     StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "atmosphere_mod", StringComparison.OrdinalIgnoreCase));

            }
        }
