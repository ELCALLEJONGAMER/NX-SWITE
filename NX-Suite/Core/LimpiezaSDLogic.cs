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
    /// L�gica de limpieza de la Micro SD.
    /// Borra todos los elementos de primer nivel (carpetas y archivos sueltos)
    /// de la ra�z de la SD, excepto los que est�n en la lista de protegidos.
    /// La comparaci�n de nombres es case-insensitive (comportamiento de Windows FAT32/exFAT).
    /// </summary>
    public class LimpiezaSDLogic
    {
        /// <summary>
        /// Analiza la ra�z de la SD y devuelve qu� se borrar� y qu� se proteger�,
        /// sin ejecutar ninguna operaci�n de escritura.
        /// </summary>
        /// <param name="letraSD">Letra de unidad (ej. "E:\").</param>
        /// <param name="protegidos">Nombres de entradas protegidas (case-insensitive).</param>
        /// <returns>Listas separadas de entradas a borrar y a conservar.</returns>
        public AnalisisLimpiezaSD Analizar(string letraSD, IEnumerable<string> protegidos)
        {
            var setProtegidos = new HashSet<string>(
                protegidos, StringComparer.OrdinalIgnoreCase);

            var aBorrar   = new List<EntradaSD>();
            var aConservar = new List<EntradaSD>();

            if (!Directory.Exists(letraSD))
                return new AnalisisLimpiezaSD(aBorrar, aConservar);

            // Carpetas de primer nivel
            foreach (var dir in Directory.EnumerateDirectories(letraSD))
            {
                string nombre = Path.GetFileName(dir);
                var entrada = new EntradaSD(nombre, EsTipoEntrada.Carpeta);

                if (setProtegidos.Contains(nombre))
                    aConservar.Add(entrada);
                else
                    aBorrar.Add(entrada);
            }

            // Archivos sueltos de primer nivel
            foreach (var file in Directory.EnumerateFiles(letraSD))
            {
                string nombre = Path.GetFileName(file);
                string ext    = Path.GetExtension(file);
                var tipo      = ZipLogic.ExtensionesComprimidas.Contains(ext)
                                ? EsTipoEntrada.Comprimido
                                : EsTipoEntrada.Archivo;
                var entrada = new EntradaSD(nombre, tipo);

                if (setProtegidos.Contains(nombre))
                    aConservar.Add(entrada);
                else
                    aBorrar.Add(entrada);
            }

            return new AnalisisLimpiezaSD(
                aBorrar.OrderBy(e => e.Tipo != EsTipoEntrada.Carpeta).ThenBy(e => e.Nombre).ToList(),
                aConservar.OrderBy(e => e.Tipo != EsTipoEntrada.Carpeta).ThenBy(e => e.Nombre).ToList());
        }

        /// <summary>
        /// Ejecuta la limpieza: borra todo lo que no est� protegido.
        /// Reporta progreso e informa de cada error sin abortar el proceso completo.
        /// </summary>
        public async Task<Resultado> EjecutarAsync(
            string letraSD,
            IEnumerable<string> protegidos,
            IProgress<EstadoProgreso>? progreso = null,
            CancellationToken ct = default)
        {
            var analisis = Analizar(letraSD, protegidos);
            var aBorrar  = analisis.ABorrar;

            if (aBorrar.Count == 0)
                return Resultado.Ok();

            Logger.LimpiezaSDIniciada(letraSD, aBorrar.Count);
            var errores = new List<string>();
            int total   = aBorrar.Count;

            return await Task.Run(() =>
            {
                for (int i = 0; i < total; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var entrada  = aBorrar[i];
                    double pct   = (double)i / total * 100.0;

                    progreso?.Report(new EstadoProgreso
                    {
                        Porcentaje  = pct,
                        TareaActual = $"Eliminando {entrada.Nombre}...",
                        PasoActual  = 1,
                    });

                    string rutaCompleta = Path.Combine(letraSD, entrada.Nombre);
                    try
                    {
                        if (entrada.Tipo == EsTipoEntrada.Carpeta)
                        {
                            if (Directory.Exists(rutaCompleta))
                                Directory.Delete(rutaCompleta, recursive: true);
                        }
                        else
                        {
                            if (File.Exists(rutaCompleta))
                                File.Delete(rutaCompleta);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LimpiezaSDElementoFallido(entrada.Nombre, ex.Message);
                        errores.Add($"{entrada.Nombre}: {ex.Message}");
                    }
                }

                progreso?.Report(new EstadoProgreso
                {
                    Porcentaje  = 100,
                    TareaActual = "Limpieza completada",
                    PasoActual  = 1,
                });

                if (errores.Count > 0)
                {
                    Logger.LimpiezaSDCompletadaConErrores(letraSD, errores.Count);
                    return Resultado.Error(
                        $"Limpieza completada con {errores.Count} error(es):\n" +
                        string.Join("\n", errores));
                }

                Logger.LimpiezaSDCompletada(letraSD);
                return Resultado.Ok();
            }, ct);
        }
    }

    /// <summary>Resultado del an�lisis previo a la limpieza.</summary>
    public sealed class AnalisisLimpiezaSD
    {
        public List<EntradaSD> ABorrar    { get; }
        public List<EntradaSD> AConservar { get; }

        public AnalisisLimpiezaSD(List<EntradaSD> aBorrar, List<EntradaSD> aConservar)
        {
            ABorrar    = aBorrar;
            AConservar = aConservar;
        }
    }

    /// <summary>Entrada de primer nivel de la SD (carpeta, archivo o comprimido).</summary>
    public sealed class EntradaSD
    {
        /// <summary>Carpetas cr�ticas del sistema Nintendo Switch.</summary>
        public static readonly HashSet<string> NombresCriticos =
            new(StringComparer.OrdinalIgnoreCase) { "emuMMC", "Nintendo" };

        public string        Nombre     { get; }
        public EsTipoEntrada Tipo       { get; }
        /// <summary>True si la entrada es una carpeta cr�tica del sistema (emuMMC, Nintendo).</summary>
        public bool          EsCritico  { get; }

        public EntradaSD(string nombre, EsTipoEntrada tipo)
        {
            Nombre    = nombre;
            Tipo      = tipo;
            EsCritico = tipo == EsTipoEntrada.Carpeta &&
                        NombresCriticos.Contains(nombre);
        }

        /// <summary>Icono de texto para mostrar en la UI.</summary>
        public string Icono => Tipo switch
        {
            EsTipoEntrada.Carpeta    => "??",
            EsTipoEntrada.Comprimido => "??",
            _                        => "??",
        };
    }

    public enum EsTipoEntrada { Carpeta, Archivo, Comprimido }
}
