using NX_Suite.Core;
using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Tarjetas del catálogo: hover, click y acciones rápidas
    /// (instalar, actualizar, reinstalar, eliminar, limpiar caché).
    /// </summary>
    public partial class MainWindow
    {
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
                    // No se reproduce Click — Instalar sound lo cubre
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
            // ?? Resolución de dependencias ????????????????????????????????????
            if (resolverDependencias
                && !string.IsNullOrEmpty(letraSD)
                && modulo.Dependencias is { Count: > 0 }
                && _catalogoModulos != null)
            {
                var deps = AnalizadorDependencias.Analizar(modulo, _catalogoModulos);
                var depsConAccion = deps.Where(d => d.Estado != EstadoDependencia.OK).ToList();

                if (depsConAccion.Any())
                {
                    // La mesa de crafteo instala deps (B,C) Y el módulo principal (A).
                    // Devuelve true = todo instalado por el overlay,
                    //          false = usuario canceló haciendo clic fuera.
                    bool exito = await MostrarCrafteoYInstalarAsync(
                        modulo, depsConAccion, letraSD);

                    if (exito)
                    {
                        // A ya fue instalado por el overlay. Solo refrescar estados.
                        if (_catalogoModulos != null)
                            _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                        await ActualizarListaUnidadesAsync();
                        RefrescarVistaActual();
                    }
                    // En ambos casos no hay que instalar A de nuevo
                    return;
                }
            }

            const double VelocidadBase = 0.0018;
            const double VelocidadMax  = 0.032;

            double targetProgress = 0.0;
            double velocidad      = VelocidadBase;

            modulo.EstaInstalando      = true;
            modulo.ProgresoInstalacion = 0.0;

            Servicios.Sonidos.Reproducir(EventoSonido.Instalar);

            var itemQueue = Servicios.Cola.AgregarItem($"Instalando {modulo.Nombre}");

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

                // Velocidad objetivo: rápida si hay mucho gap, mínima si está cerca
                double vObjetivo = Math.Clamp(diff * 0.18, VelocidadBase, VelocidadMax);

                // La velocidad se suaviza sola (sin acelerones ni frenazos bruscos)
                velocidad += (vObjetivo - velocidad) * 0.10;

                modulo.ProgresoInstalacion = Math.Min(targetProgress, modulo.ProgresoInstalacion + velocidad);
            };
            timer.Start();

            var progreso = new Progress<EstadoProgreso>(estado =>
            {
                targetProgress = estado.Porcentaje / 100.0;
                Servicios.Cola.ActualizarItem(itemQueue, estado.Porcentaje, estado.TareaActual);
            });

            try
            {
                var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso, itemQueue.Token);

                // Llevar al 100% y esperar que el relleno llegue visualmente (máx 2s)
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
                    // Con SD válida: escanear el sistema de archivos para actualizar EstadoSd
                    // en todos los módulos (no sólo el caché local). Imprescindible para que
                    // el overlay de dependencias detecte qué deps ya están instaladas y pueda
                    // desbloquear el módulo principal sin necesitar un re-sync por red.
                    if (!string.IsNullOrEmpty(letraSD))
                        _cerebro.RefrescarEstadosSinRed(_catalogoModulos, letraSD);
                    else
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                }

                // Solo refrescar la lista de unidades y la vista cuando se instala el módulo
                // principal (no durante la instalación silenciosa de dependencias).
                // Así los objetos ModuloConfig del catálogo no se reemplazan por nuevas
                // instancias mientras el bucle de dependencias aún los necesita.
                if (resolverDependencias)
                {
                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                }

                // Si el módulo instalado afecta a Atmosphere, refrescar su versión en el panel
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
        }

        private async Task EjecutarEliminacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            var itemQueue = Servicios.Cola.AgregarItem($"Eliminando {modulo.Nombre}");
            Servicios.Cola.ActualizarItem(itemQueue, 0, "Eliminando archivos de la SD...");

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(modulo, letraSD);
                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                // Si el módulo desinstalado afecta a Atmosphere, refrescar su versión en el panel
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
                Servicios.Cola.ErrorItem(itemQueue, ex.Message);
                Dialogos.Error(ex.Message);
            }
        }

        private void ConfirmarLimpiezaCache(ModuloConfig modulo)
        {
            if (!Dialogos.Confirmar($"¿Eliminar caché local de {modulo.Nombre}?", "Limpiar Caché"))
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
        /// Devuelve true si el módulo tiene la etiqueta "atmosphere" o "atmosphere_mod".
        /// </summary>
        private static bool EsModuloAtmosphere(ModuloConfig modulo) =>
            modulo.Etiquetas != null &&
            modulo.Etiquetas.Any(t =>
                string.Equals(t, "atmosphere",     StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "atmosphere_mod", StringComparison.OrdinalIgnoreCase));

            }
        }
