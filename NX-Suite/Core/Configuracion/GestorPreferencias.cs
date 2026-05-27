using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NX_Suite.Core.Configuracion
{
    /// <summary>
    /// Carga y guarda <see cref="PreferenciasUsuario"/> en
    /// <c>%AppData%\NX-Suite\preferencias.json</c>.
    ///
    /// Diseñado para ser tolerante a fallos: si el archivo está corrupto o
    /// tiene propiedades desconocidas (versión futura/pasada), se cargan los
    /// valores por defecto sin lanzar excepción.
    /// </summary>
    public sealed class GestorPreferencias
    {
        // ?? Opciones JSON robustas ???????????????????????????????????????????
        private static readonly JsonSerializerOptions _opcionesLectura = new()
        {
            ReadCommentHandling    = JsonCommentHandling.Skip,
            AllowTrailingCommas    = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling         = JsonNumberHandling.AllowReadingFromString,
            UnknownTypeHandling    = JsonUnknownTypeHandling.JsonElement,
            // Ignora propiedades que ya no existen en el modelo
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };

        private static readonly JsonSerializerOptions _opcionesEscritura = new()
        {
            WriteIndented          = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly string _rutaArchivo;

        public GestorPreferencias()
        {
            _rutaArchivo = ConfiguracionLocal.RutaPreferencias;
        }

        // ?? Carga ????????????????????????????????????????????????????????????

        /// <summary>
        /// Carga las preferencias desde disco. Si el archivo no existe o está
        /// dañado, devuelve una instancia con valores por defecto.
        /// Nunca lanza excepción.
        /// </summary>
        public async Task<PreferenciasUsuario> CargarAsync()
        {
            try
            {
                if (!File.Exists(_rutaArchivo))
                    return new PreferenciasUsuario();

                var json = await File.ReadAllTextAsync(_rutaArchivo);
                if (string.IsNullOrWhiteSpace(json))
                    return new PreferenciasUsuario();

                var preferencias = JsonSerializer.Deserialize<PreferenciasUsuario>(json, _opcionesLectura);
                return preferencias ?? new PreferenciasUsuario();
            }
            catch
            {
                // Si el JSON está corrupto, ignorar y usar defaults
                return new PreferenciasUsuario();
            }
        }

        // ?? Guardado ?????????????????????????????????????????????????????????

        /// <summary>
        /// Guarda las preferencias en disco de forma asíncrona.
        /// Nunca lanza excepción.
        /// </summary>
        public async Task GuardarAsync(PreferenciasUsuario preferencias)
        {
            try
            {
                var dir = Path.GetDirectoryName(_rutaArchivo)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(preferencias, _opcionesEscritura);
                await File.WriteAllTextAsync(_rutaArchivo, json);
            }
            catch { /* Fallo silencioso — no es crítico */ }
        }

        // ?? Aplicar al estado estático ????????????????????????????????????????

        /// <summary>
        /// Aplica las preferencias cargadas a <see cref="ConfiguracionSonidos"/>.
        /// Llamar tras cada carga o guardado.
        /// </summary>
        public static void AplicarSonido(SeccionSonido s)
        {
            ConfiguracionSonidos.SonidosActivos = s.Activo;
            ConfiguracionSonidos.Intro          = s.Intro;
            ConfiguracionSonidos.Cerrar         = s.Cerrar;
            ConfiguracionSonidos.Click          = s.Click;
            ConfiguracionSonidos.Hover          = s.Hover;
            ConfiguracionSonidos.Instalar       = s.Instalar;
            ConfiguracionSonidos.Exito          = s.Exito;
            ConfiguracionSonidos.Error          = s.Error;
            ConfiguracionSonidos.Navegacion     = s.Navegacion;
            ConfiguracionSonidos.Volumen        = s.Volumen;
        }
    }
}
