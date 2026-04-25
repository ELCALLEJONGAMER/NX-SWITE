using NX_Suite.Core.Configuracion;
using NX_Suite.Core.Pipeline;
using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    /// <summary>
    /// Orquestador del pipeline declarativo del JSON. Recibe la lista de
    /// <see cref="PasoPipeline"/>, prepara el <see cref="ContextoPipeline"/>
    /// compartido y delega la ejecución de cada paso en el handler
    /// correspondiente registrado en <see cref="RegistroPasos"/>.
    ///
    /// Esta clase NO contiene lógica de pasos: para añadir o modificar una
    /// acción, ver <c>Core/Pipeline/Pasos/</c>.
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
            CancellationToken        ct = default)
        {
            if (pipeline == null || pipeline.Count == 0) return Resultado.Ok();

            // ?? Preparación de carpetas locales ???????????????????????????
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
            };

            return await Task.Run(async () =>
            {
                try
                {
                    int totalPasos = pipeline.Count;
                    int pasoActual = 0;

                    foreach (var paso in pipeline)
                    {
                        ct.ThrowIfCancellationRequested();
                        pasoActual++;
                        Reportar(progreso, pasoActual, totalPasos, paso.MensajeUI);

                        IPasoPipeline? handler = _registro.Obtener(paso.TipoAccion);
                        if (handler == null)
                            throw new InvalidOperationException($"Tipo de acción desconocido en el pipeline: '{paso.TipoAccion}'.");

                        await handler.EjecutarAsync(ctx, paso.Parametros, ct);

                        await Task.Delay(500, ct);
                    }

                    return Resultado.Ok();
                }
                catch (OperationCanceledException)
                {
                    return Resultado.Error("Operación cancelada");
                }
                catch (Exception ex)
                {
                    return Resultado.Error(ex.Message);
                }
            }, ct);
        }

        private static void Reportar(IProgress<EstadoProgreso>? progreso, int pasoActual, int totalPasos, string mensajeUI)
        {
            if (progreso == null) return;
            double porcentaje = (double)pasoActual / totalPasos * 100;
            progreso.Report(new EstadoProgreso
            {
                Porcentaje  = porcentaje,
                TareaActual = mensajeUI,
                PasoActual  = pasoActual
            });
        }
    }
}
