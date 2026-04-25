using NX_Suite.Core.Configuracion;
using System;
using System.IO;
using System.Management;
using System.Collections.Generic;
using System.Text.Json;

namespace NX_Suite.Core
{
   

    public class ManifiestoLocal
    {
        public string Version { get; set; }
    }

    public class SDMonitorLogic
    {
       

        /// <summary>
        /// Verifica la existencia y versión de un módulo instalado en la SD mediante su manifiesto local.
        /// </summary>
        public string DetectarModulo(string rutaRaiz, string nombreCarpetaModulo)
        {
            try
            {
                string pathModulo = Path.Combine(rutaRaiz, nombreCarpetaModulo);

                if (Directory.Exists(pathModulo))
                {
                    string pathManifiesto = Path.Combine(pathModulo, ConfiguracionLocal.NombreManifiesto);

                    if (File.Exists(pathManifiesto))
                    {
                        string json = File.ReadAllText(pathManifiesto);
                        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var manifiesto = JsonSerializer.Deserialize<ManifiestoLocal>(json, opciones);

                        return manifiesto?.Version ?? "Versión Desconocida";
                    }

                    return "Instalación No Oficial";
                }
                return "No instalado";
            }
            catch { return "Error lectura"; }
        }

       
    }
}