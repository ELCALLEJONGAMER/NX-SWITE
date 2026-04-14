using NX_Suite.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows; // Necesario para mostrar la ventana de error

namespace NX_Suite.Network
{
    public class GistParser
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<GistData> ObtenerTodoElGistAsync(string urlGistRaw)
        {
            try
            {
                // El truco anti-caché
                string urlAntiCache = $"{urlGistRaw}?t={DateTime.Now.Ticks}";

                // 1. Descargamos el JSON
                string jsonContent = await client.GetStringAsync(urlAntiCache);

                var opciones = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                // 2. Intentamos traducirlo a C#
                var resultado = JsonSerializer.Deserialize<GistData>(jsonContent, opciones);

                return resultado ?? new GistData();
            }
            catch (JsonException jsonEx)
            {
                // Si el error es de sintaxis (una coma o llave mal puesta)
                MessageBox.Show($"Error de sintaxis en el JSON:\nLínea {jsonEx.LineNumber}, Posición {jsonEx.BytePositionInLine}\nDetalle: {jsonEx.Message}", "Error de Gist", MessageBoxButton.OK, MessageBoxImage.Error);
                return new GistData();
            }
            catch (Exception ex)
            {
                // Si el error es de internet o de enlace roto
                MessageBox.Show($"Error de conexión:\n{ex.Message}", "Error de Red", MessageBoxButton.OK, MessageBoxImage.Error);
                return new GistData();
            }
        }
    }
}