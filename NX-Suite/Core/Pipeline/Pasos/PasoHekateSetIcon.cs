using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Aplica un icono a las secciones de un .ini de Hekate filtrando por
    /// tipo lógico (emummc / stock / sysnand). No modifica el .ini si no
    /// encuentra ninguna sección que coincida.
    ///
    /// Parámetros JSON:
    ///   ArchivoIni : ruta del .ini en la SD (ej. "/bootloader/hekate_ipl.ini")
    ///   TipoIcono  : "emummc" | "stock" | "sysnand"
    ///   RutaIcono  : ruta del BMP a asignar
    /// </summary>
    public class PasoHekateSetIcon : IPasoPipeline
    {
        public string TipoAccion => "HEKATE_SET_ICON";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string archivoRel = parametros.GetProperty("ArchivoIni").GetString()!;
            string tipoIcono  = parametros.GetProperty("TipoIcono").GetString()!;
            string rutaIcono  = parametros.GetProperty("RutaIcono").GetString()!;
            string fullPath   = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, archivoRel);

            if (!File.Exists(fullPath)) return;

            var iniMgr = new HekateIniManager(fullPath);
            await iniMgr.LoadAsync();

            List<string> seccionesObjetivo = tipoIcono.ToLower() switch
            {
                "emummc"  => iniMgr.ObtenerSeccionesConClave("emummcforce", "1"),
                "stock"   => iniMgr.ObtenerSeccionesConClave("stock", "1"),
                "sysnand" => iniMgr.ObtenerSeccionesConClave("emummc_force_disable", "1")
                                   .Intersect(iniMgr.ObtenerSeccionesConClave("atmosphere", "1"))
                                   .ToList(),
                _         => new List<string>()
            };

            foreach (var sec in seccionesObjetivo)
                iniMgr.SetValue(sec, "icon", rutaIcono);

            if (seccionesObjetivo.Count > 0)
                await iniMgr.SaveAsync();
        }
    }
}
