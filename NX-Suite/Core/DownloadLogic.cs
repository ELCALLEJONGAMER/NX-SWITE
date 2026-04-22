using NX_Suite.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public class DownloadLogic
    {
        private readonly HttpClient _httpClient;

        public DownloadLogic()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Descarga un archivo con reporte de progreso detallado (EstadoProgreso).
        /// </summary>
        public async Task DescargarArchivoAsync(string url, string rutaDestino, IProgress<EstadoProgreso> progreso = null, CancellationToken ct = default)
        {
            using (var respuesta = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                respuesta.EnsureSuccessStatusCode();

                var totalBytes = respuesta.Content.Headers.ContentLength ?? -1L;
                var bytesLeidos = 0L;

                using (var contenidoStream = await respuesta.Content.ReadAsStreamAsync(ct))
                using (var archivoStream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int lectura;

                    while ((lectura = await contenidoStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await archivoStream.WriteAsync(buffer, 0, lectura);
                        bytesLeidos += lectura;

                        if (totalBytes != -1 && progreso != null)
                        {
                            double porcentaje = (double)bytesLeidos / totalBytes * 100;

                            // Calculamos a Megabytes para darle un toque premium a la interfaz
                            double mbLeidos = bytesLeidos / 1048576.0;
                            double mbTotal = totalBytes / 1048576.0;

                            progreso.Report(new EstadoProgreso
                            {
                                Porcentaje = porcentaje,
                                TareaActual = $"Descargando: {mbLeidos:F1} MB / {mbTotal:F1} MB",
                                PasoActual = 1 // <---- AÑADE ESTA LÍNEA
                            });
                        }
                    }
                }
            }
        }
    }
}