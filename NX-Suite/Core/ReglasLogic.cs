using NX_Swite.Core.Configuracion;
using NX_Swite.Core.Pipeline;
using NX_Swite.Models;
using NX_Swite.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Orquestador del pipeline declarativo del JSON. Recibe la lista de
    /// <see cref="PasoPipeline"/>, prepara el <see cref="ContextoPipeline"/>
    /// compartido y delega la ejecuci�n de cada paso en el handler
    /// correspondiente registrado en <see cref="RegistroPasos"/>.
    ///
    /// Esta clase NO contiene l�gica de pasos: para a�adir o modificar una
    /// acci�n, ver <c>Core/Pipeline/Pasos/</c>.
    /// </summary>
    public class ReglasLogic
    {
        private readonly DownloadLogic  _motorDescarga = new();
        private readonly ZipLogic       _motorZip      = new();
        private readonly RegistroPasos  _registro      = new();

        public async Task<Resultado> EjecutarPipelineAsync(
            List<PasoPipeline>       pipeline,
            string                   letraSD,
            IProgress<EstadoProgreso>? progreso = null,
            CancellationToken        ct = default,
            string                   versionModulo = "",
            string                   nombreModulo = "")
        {
            if (pipeline == null || pipeline.Count == 0) return Resultado.Ok();

            // ?? Preparaci�n de carpetas locales ???????????????????????????
            string rutaCacheZips       = ConfiguracionLocal.RutaCacheZips;
            string rutaCacheExtraccion = ConfiguracionLocal.RutaCacheExtraccion;
            string rutaBackups         = ConfiguracionLocal.RutaBackups;

            Directory.CreateDirectory(rutaCacheZips);
            Directory.CreateDirectory(rutaCacheExtraccion);
            Directory.CreateDirectory(rutaBackups);

            // ?? Contexto compartido (inmutable durante todo el pipeline) ?
            var ctx = new ContextoPipeline
            {
                LetraSD             = letraSD,
                RutaCacheZips       = rutaCacheZips,
                RutaCacheExtraccion = rutaCacheExtraccion,
                RutaBackups         = rutaBackups,
                MotorDescarga       = _motorDescarga,
                MotorZip            = _motorZip,
                Progreso            = progreso,
                VersionModulo       = versionModulo,
                // Validador h�brido: solo act�a en URLs de GitHub y nunca bloquea la instalaci�n.
                ValidadorAsset      = new GitHubAssetValidator(Configuracion.TokenGitHub.Cargar()),
            };

            // Calcular pesos por tipo de paso y rangos globales acumulados
            var pesos = pipeline.Select(p => PesoPaso(p.TipoAccion)).ToList();
            double totalPeso = pesos.Sum();
            var rangos = new (double Inicio, double Fin)[pipeline.Count];
            double acum = 0;
            for (int i = 0; i < pipeline.Count; i++)
            {
                double w = pesos[i] / totalPeso * 100.0;
                rangos[i] = (acum, acum + w);
                acum += w;
            }

            string modLabel = string.IsNullOrWhiteSpace(nombreModulo) ? "Pipeline" : nombreModulo;

            if (!string.IsNullOrWhiteSpace(nombreModulo))
                Logger.InstalacionIniciada(nombreModulo, versionModulo, letraSD);

            return await Task.Run(async () =>
            {
                string pasoActivo = string.Empty;
                string tipoActivo = string.Empty;
                try
                {
                    for (int i = 0; i < pipeline.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var paso = pipeline[i];
                        pasoActivo = paso.MensajeUI ?? paso.TipoAccion;
                        tipoActivo = paso.TipoAccion;
                        var (inicio, fin) = rangos[i];

                        // Reportar inicio del paso con su porcentaje global real
                        progreso?.Report(new EstadoProgreso
                        {
                            Porcentaje  = inicio,
                            TareaActual = paso.MensajeUI,
                            PasoActual  = i + 1
                        });

                        // Sin SD: omitir pasos que operan exclusivamente sobre la tarjeta
                        if (string.IsNullOrWhiteSpace(ctx.LetraSD) && EsPasoSoloSD(paso.TipoAccion))
                        {
                            progreso?.Report(new EstadoProgreso { Porcentaje = fin, TareaActual = paso.MensajeUI, PasoActual = i + 1 });
                            continue;
                        }

                        IPasoPipeline? handler = _registro.Obtener(paso.TipoAccion);
                        if (handler == null)
                            throw new InvalidOperationException($"Tipo de acci�n desconocido en el pipeline: '{paso.TipoAccion}'.");

                        // Wrappear el progreso para que los reportes internos del paso
                        // (0-100 %) se mapeen al rango global [inicio, fin].
                        double rango = fin - inicio;
                        ctx.Progreso = progreso == null ? null : new Progress<EstadoProgreso>(estado =>
                        {
                            double pctGlobal = inicio + Math.Clamp(estado.Porcentaje, 0, 100) / 100.0 * rango;
                            progreso.Report(new EstadoProgreso
                            {
                                Porcentaje  = pctGlobal,
                                TareaActual = estado.TareaActual,
                                PasoActual  = i + 1
                            });
                        });

                        await handler.EjecutarAsync(ctx, paso.Parametros, ct);

                        // Confirmar el 100 % del paso al terminarlo
                        progreso?.Report(new EstadoProgreso
                        {
                            Porcentaje  = fin,
                            TareaActual = paso.MensajeUI,
                            PasoActual  = i + 1
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(nombreModulo))
                        Logger.InstalacionCompletada(nombreModulo, versionModulo);
                    return Resultado.Ok();
                }
                catch (OperationCanceledException)
                {
                    if (!string.IsNullOrWhiteSpace(nombreModulo))
                        Logger.InstalacionCancelada(nombreModulo, versionModulo);
                    return Resultado.Error("Operaci�n cancelada");
                }
                catch (Exception ex)
                {
                    string contexto = string.IsNullOrEmpty(pasoActivo)
                        ? string.Empty
                        : $" (paso: {pasoActivo})";
                    string errorMsg = $"{ex.Message}{contexto}";
                    if (!string.IsNullOrWhiteSpace(nombreModulo))
                        Logger.InstalacionFallida(nombreModulo, versionModulo, errorMsg);
                    return Resultado.Error(errorMsg);
                }
            }, ct);
        }

        /// <summary>
        /// Peso relativo de cada tipo de paso en el progreso global.
        /// Los pasos de descarga y copia pesan m�s por ser los m�s lentos.
        /// El resto de pasos (creaci�n de INI, edici�n, borrado, etc.) se distribuyen
        /// equitativamente con un peso m�nimo.
        /// </summary>
        private static double PesoPaso(string tipoAccion) => tipoAccion.ToUpperInvariant() switch
        {
            "DESCARGAR" => 38,
            "EXTRAER"   => 22,
            "COPIARSD"  => 30,
            _           => 3,
        };

        /// <summary>
        /// Pasos que operan exclusivamente sobre la SD y deben omitirse si no hay unidad conectada.
        /// </summary>
        private static bool EsPasoSoloSD(string tipoAccion) => tipoAccion.ToUpperInvariant() switch
        {
            "COPIARSD"       => true,
            "BORRARARCHIVOS" => true,
            "BORRARCARPETAS" => true,
            "CREARCARPETA"   => true,
            "CREARINI"       => true,
            "EDITARINI"      => true,
            "CREARTXT"       => true,
            "MOVERARCHIVO"   => true,
            "HEKATEICOSET"   => true,
            "HEKATESET"      => true,
            "FORMATEARSD"    => true,
            _                => false,
        };
    }
}
