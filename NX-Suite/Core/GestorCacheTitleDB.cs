using System;
using System.IO;
using System.Threading.Tasks;
using NX_Swite.Core.Configuracion;

namespace NX_Swite.Core
{
    /// <summary>
    /// Caché en disco del TitleDB remoto (metadata TitleId ? nombre de juego).
    /// Vive en una carpeta independiente de la bóveda de módulos
    /// (<see cref="ConfiguracionLocal.RutaCacheTitleDB"/>), junto a un sidecar
    /// de ETag para revalidación condicional HTTP.
    ///
    /// Reglas de robustez:
    ///  - Escritura atómica: primero se escribe a un archivo temporal y solo
    ///    se reemplaza el caché válido si la escritura fue exitosa.
    ///  - Si la descarga nueva no puede parsearse, el caché previo NO se toca.
    /// </summary>
    public class GestorCacheTitleDB
    {
        public string RutaCacheJson { get; }
        private string RutaETag => RutaCacheJson + ".etag";
        private string RutaTemp => RutaCacheJson + ".tmp";

        public GestorCacheTitleDB()
        {
            RutaCacheJson = ConfiguracionLocal.RutaCacheTitleDBJson;

            string carpeta = Path.GetDirectoryName(RutaCacheJson) ?? ConfiguracionLocal.RutaCacheTitleDB;
            if (!Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);
        }

        /// <summary>Indica si existe un JSON de TitleDB guardado localmente.</summary>
        public bool TieneCache => File.Exists(RutaCacheJson);

        /// <summary>Carga el JSON cacheado, o null si no existe o no puede leerse.</summary>
        public async Task<string?> CargarJsonAsync()
        {
            try
            {
                if (!File.Exists(RutaCacheJson)) return null;
                return await File.ReadAllTextAsync(RutaCacheJson);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Guarda el JSON de forma atómica: escribe a un temporal y solo
        /// reemplaza el caché existente si la escritura fue exitosa. El
        /// llamador debe validar que el contenido parsea ANTES de invocar esto,
        /// para nunca sobreescribir un caché válido con contenido corrupto.
        /// </summary>
        public async Task GuardarJsonAtomicoAsync(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return;

            try
            {
                await File.WriteAllTextAsync(RutaTemp, jsonContent);

                if (File.Exists(RutaCacheJson))
                    File.Replace(RutaTemp, RutaCacheJson, null);
                else
                    File.Move(RutaTemp, RutaCacheJson);
            }
            catch
            {
                // No crítico: el caché previo (si existe) sigue siendo válido.
                try { if (File.Exists(RutaTemp)) File.Delete(RutaTemp); } catch { }
            }
        }

        public async Task<string?> CargarETagAsync()
        {
            try
            {
                if (!File.Exists(RutaETag)) return null;
                return (await File.ReadAllTextAsync(RutaETag)).Trim();
            }
            catch { return null; }
        }

        public async Task GuardarETagAsync(string etag)
        {
            if (string.IsNullOrWhiteSpace(etag)) return;
            try { await File.WriteAllTextAsync(RutaETag, etag); } catch { }
        }
    }
}
