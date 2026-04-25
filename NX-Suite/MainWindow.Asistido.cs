﻿using NX_Suite.Core;
using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using NX_Suite.UI.Controles;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Handlers de la <see cref="VistaAsistida"/>: instalación
    /// secuencial de la sesión asistida y modo "Procesar Completo" (particionado
    /// + formateo + instalación masiva).
    /// </summary>
    public partial class MainWindow
    {
        private async void VistaAsistida_InstalacionSolicitada(object? sender, SesionAsistida sesion)
        {
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                Dialogos.Advertencia("No hay ninguna SD seleccionada.");
                return;
            }

            var todosAInstalar = sesion.Modulos;

            if (todosAInstalar.Count == 0) return;

            try
            {
                int total    = todosAInstalar.Count;
                int fallidos = 0;

                for (int i = 0; i < total; i++)
                {
                    var modulo = todosAInstalar[i];
                    _pantallaCarga.Mostrar($"Instalando {modulo.Nombre} ({i + 1}/{total})");

                    var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, _pantallaCarga.ObtenerReportador());

                    if (!resultado.Exito)
                    {
                        fallidos++;
                        bool continuar = Dialogos.Confirmar(
                            $"Error instalando {modulo.Nombre}:\n{resultado.MensajeError}\n\n¿Continuar con los demás?",
                            "Error parcial", MessageBoxImage.Warning);

                        if (!continuar) break;
                    }
                }

                await Task.Delay(500);
                _pantallaCarga.Ocultar();

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                string mensaje = fallidos == 0
                    ? $"¡Instalación completada! {total} módulo(s) instalado(s)."
                    : $"Instalación finalizada con {fallidos} error(es) de {total}.";

                if (fallidos == 0)
                    Dialogos.Info(mensaje, "Éxito");
                else
                    Dialogos.Advertencia(mensaje, "Completado con errores");
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error crítico: {ex.Message}");
            }
        }

        private async void VistaAsistida_ProcesarCompletoSolicitado(object? sender, NX_Suite.UI.Controles.ProcesarCompletoArgs args)
        {
            // La ventana ya se cerro — todos los datos vienen en args.
            string? letraSD     = args.LetraSD;
            int     numeroDisco = args.NumeroDisco;
            var     modulos     = args.Modulos;
            int     total       = modulos.Count;
            int     gbEmuMMC    = args.GbEmuMMC;

            if (string.IsNullOrEmpty(letraSD) || numeroDisco < 0)
            {
                Dialogos.Error("No se pudo identificar la SD o el disco fisico.");
                return;
            }

            // Abrir el panel de cola automaticamente
            PanelQueueOverlay.Visibility = Visibility.Visible;

            var itemPrincipal = Servicios.Cola.AgregarItem($"Asistido Completo — disco {numeroDisco}");
            Servicios.Sonidos.Reproducir(EventoSonido.Instalar);

            int fallidos = 0;
            try
            {
                // ?? FASE 1: Particionado y formateo ??????????????????????
                Servicios.Cola.ActualizarItem(itemPrincipal, 2,
                    $"Particionando disco {numeroDisco} — emuMMC: {gbEmuMMC} GB…");

                var partitioner = new ParticionadorDiscos();
                var progresoDisk = new Progress<(int Pct, string Msg)>(p =>
                {
                    Servicios.Cola.ActualizarItem(itemPrincipal, (int)(p.Pct * 0.45), p.Msg);
                });

                string urlFat32 = _datosGist?.ConfiguracionUI?.UrlFat32Format ?? string.Empty;
                await partitioner.ParticionarYFormatearAsync(numeroDisco, gbEmuMMC, urlFat32, progresoDisk);

                // Tras el particionado+formateo, Windows asigna la letra automáticamente.
                // Buscamos la nueva partición SWITCH SD por etiqueta o por disco físico.
                await Task.Delay(2000);
                await ActualizarListaUnidadesAsync();
                var unidades = new EscanerDiscos().ObtenerUnidadesRemovibles();
                var sdNueva  = unidades.FirstOrDefault(u =>
                    u.Etiqueta.Equals("SWITCH SD", StringComparison.OrdinalIgnoreCase) ||
                    u.DiscoFisico == numeroDisco);
                if (sdNueva?.Letra != null) letraSD = sdNueva.Letra;

                Servicios.Cola.ActualizarItem(itemPrincipal, 45, "Particionado OK. Instalando modulos…");

                // ?? FASE 2: Instalacion de modulos ???????????????????????
                for (int i = 0; i < total; i++)
                {
                    var modulo  = modulos[i];
                    int pctBase = 45 + (int)((double)i / total * 55);
                    int pctSig  = 45 + (int)((double)(i + 1) / total * 55);

                    Servicios.Cola.ActualizarItem(itemPrincipal, pctBase,
                        $"Instalando {modulo.Nombre} ({i + 1}/{total})…");

                    var progreso = new Progress<EstadoProgreso>(estado =>
                    {
                        int pct = pctBase + (int)((pctSig - pctBase) * estado.Porcentaje / 100.0);
                        Servicios.Cola.ActualizarItem(itemPrincipal, pct, estado.TareaActual);
                    });

                    var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso);
                    if (!resultado.Exito)
                    {
                        fallidos++;
                        Servicios.Cola.ActualizarItem(itemPrincipal, pctSig,
                            $"Error en {modulo.Nombre}: {resultado.MensajeError}");
                    }
                }

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (fallidos == 0)
                {
                    Servicios.Cola.CompletarItem(itemPrincipal);
                    Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                }
                else
                {
                    Servicios.Cola.ErrorItem(itemPrincipal,
                        $"Completado con {fallidos} error(es) de {total} modulos");
                    Servicios.Sonidos.Reproducir(EventoSonido.Error);
                }
            }
            catch (Exception ex)
            {
                Servicios.Cola.ErrorItem(itemPrincipal, ex.Message);
                Servicios.Sonidos.Reproducir(EventoSonido.Error);
            }
        }
    }
}
