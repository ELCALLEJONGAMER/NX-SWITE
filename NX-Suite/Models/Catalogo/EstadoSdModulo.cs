﻿﻿namespace NX_Swite.Models
{
    /// <summary>Estado de instalación del módulo en la microSD seleccionada.</summary>
    public enum EstadoSdModulo
    {
        NoInstalado,
        ParcialmenteInstalado,
        Instalado,

        /// <summary>
        /// Existe evidencia física suficientemente fuerte de que el módulo está presente
        /// en la SD (ruta exclusiva de este módulo en el catálogo actual, con SHA256
        /// declarado), pero el SHA256 real del archivo no coincide con ninguna versión
        /// conocida del módulo. NO implica que el contenido sea válido o compatible,
        /// solo que su versión/contenido no pudo ser identificado.
        /// </summary>
        InstaladoVersionDesconocida
    }
}
