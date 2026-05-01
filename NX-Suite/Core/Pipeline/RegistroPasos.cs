using NX_Suite.Core.Pipeline.Pasos;
using System;
using System.Collections.Generic;

namespace NX_Suite.Core.Pipeline
{
    /// <summary>
    /// Registro centralizado de todos los <see cref="IPasoPipeline"/> disponibles.
    /// El orquestador (<c>ReglasLogic</c>) busca aquí el handler de cada
    /// <c>TipoAccion</c> del JSON.
    ///
    /// Para añadir un paso nuevo:
    ///   1. Crear la clase en <c>Core/Pipeline/Pasos/</c> implementando <see cref="IPasoPipeline"/>.
    ///   2. Añadir <c>Registrar(new PasoXxx())</c> en el constructor de abajo.
    /// </summary>
    public class RegistroPasos
    {
        private readonly Dictionary<string, IPasoPipeline> _pasos =
            new(StringComparer.OrdinalIgnoreCase);

        public RegistroPasos()
        {
            // ?? Descargas y extracción ???????????????????????????????????
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
        /// La búsqueda es case-insensitive.
        /// </summary>
        public IPasoPipeline? Obtener(string tipoAccion)
            => _pasos.TryGetValue(tipoAccion, out var p) ? p : null;
    }
}
