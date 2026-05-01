using SixLabors.ImageSharp;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Bmp;

namespace NX_Suite.Core
{
    public static class ImageConverter
    {
        /// <summary>
        /// Convierte una imagen al formato BMP 24-bit compatible con Hekate.
        /// Soporta cualquier resolución de destino (bootlogo, background, iconos).
        /// </summary>
        public static void ConvertirParaHekate(string rutaOrigen, string rutaDestino, int ancho, int alto, int bits = 24)
        {
            var dirDestino = Path.GetDirectoryName(rutaDestino);
            if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                Directory.CreateDirectory(dirDestino);

            using var imagen = Image.Load(rutaOrigen);
            imagen.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ancho, alto),
                Mode = ResizeMode.Stretch
            }));

            var bitsPerPixel = bits == 32
                ? BmpBitsPerPixel.Pixel32
                : BmpBitsPerPixel.Pixel24;

            imagen.Save(rutaDestino, new BmpEncoder { BitsPerPixel = bitsPerPixel });
        }

        /// <summary>
        /// Convierte una imagen a un formato de icono compatible con Hekate (BMP de 24 bits).
        /// </summary>
        public static async Task<bool> ConvertToHekateIcon(string inputImagePath, string outputImagePath, int size = 192)
        {
            try
            {
                if (string.IsNullOrEmpty(inputImagePath) || string.IsNullOrEmpty(outputImagePath))
                {
                    return false;
                }

                // Asegurarse de que el directorio de salida exista
                var outputDirectory = Path.GetDirectoryName(outputImagePath);
                if (outputDirectory != null && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Cargar la imagen desde el archivo de entrada
                using (Image image = await Image.LoadAsync(inputImagePath))
                {
                    // Redimensionar la imagen a las dimensiones especificadas
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(size, size),
                        Mode = ResizeMode.Stretch // Estirar para asegurar el tamaño exacto
                    }));

                    // Crear un codificador para BMP con 24 bits por píxel
                    var encoder = new BmpEncoder
                    {
                        BitsPerPixel = BmpBitsPerPixel.Pixel24
                    };

                    // Guardar la imagen convertida
                    await image.SaveAsync(outputImagePath, encoder);
                }

                return true;
            }
            catch (Exception ex)
            {
                // Aquí podrías registrar el error si tienes un sistema de logging
                Console.WriteLine($"Error al convertir la imagen: {ex.Message}");
                return false;
            }
        }
    }
}
