using NX_Swite.Core.Pipeline.Pasos;
using System;
using System.Collections.Generic;

namespace NX_Swite.Core.Pipeline
{
    /// <summary>
    /// Registro centralizado de todos los <see cref="IPasoPipeline"/> disponibles.
    /// El orquestador (<c>ReglasLogic</c>) busca aqu� el handler de cada
    /// <c>TipoAccion</c> del JSON.
    ///
    /// Para a�adir un paso nuevo:
    ///   1. Crear la clase en <c>Core/Pipeline/Pasos/</c> implementando <see cref="IPasoPipeline"/>.
    ///   2. A�adir <c>Registrar(new PasoXxx())</c> en el constructor de abajo.
    /// </summary>
    public class RegistroPasos
    {
        private readonly Dictionary<string, IPasoPipeline> _pasos =
            new(StringComparer.OrdinalIgnoreCase);

        public RegistroPasos()
        {
            // ?? Descargas y extracci�n ???????????????????????????????????
            Registrar(new PasoDescargar());
            Registrar(new PasoExtraer());
            Registrar(new PasoCopiarSD());

            // ?? INI / Hekate ?????????????????????????????????????????????
            Registrar(new PasoHekateSetIcon());
            Registrar(new PasoHekateSetValue());
            Registrar(new PasoEditarIni());
            Registrar(new PasoCrearTxt());
            Registrar(new PasoCrearIni());

            // ?? Filesystem en SD ?????????????????????????????????????????
            Registrar(new PasoBorrarArchivos());
            Registrar(new PasoBorrarCarpetas());
            Registrar(new PasoBorrarCarpetasVacias());
            Registrar(new PasoCrearCarpeta());
            Registrar(new PasoMoverArchivo());

            // ?? Sistema y backups ????????????????????????????????????????
            Registrar(new PasoEjecutarCmd());
            Registrar(new PasoRespaldarAPc());
            Registrar(new PasoRestaurarDePc());
            Registrar(new PasoLimpiarCache());

            // ?? Hardware ?????????????????????????????????????????????????
            Registrar(new PasoFormatearSd());
        }

        private void Registrar(IPasoPipeline paso) => _pasos[paso.TipoAccion] = paso;

        /// <summary>
        /// Devuelve el paso correspondiente al tipo indicado o null si no existe.
        /// La b�squeda es case-insensitive.
        /// </summary>
        public IPasoPipeline? Obtener(string tipoAccion)
            => _pasos.TryGetValue(tipoAccion, out var p) ? p : null;
    }
}
