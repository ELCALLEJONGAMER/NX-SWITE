using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public class UninstallLogic
    {
        /// <summary>
        /// Elimina archivos o carpetas específicos de la unidad de destino basándose en una lista de rutas relativas.
        /// </summary>
        public async Task<bool> DesinstalarAsync(List<string> rutasABorrar, string letraSD)
        {
            if (rutasABorrar == null || rutasABorrar.Count == 0) return false;

            return await Task.Run(() =>
            {
                try
                {
                    foreach (var ruta in rutasABorrar)
                    {
                        string rutaReal = Path.Combine(letraSD, ruta.TrimStart('/'));

                        if (File.Exists(rutaReal))
                        {
                            File.Delete(rutaReal);
                        }
                        else if (Directory.Exists(rutaReal))
                        {
                            Directory.Delete(rutaReal, true);
                        }
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }
}