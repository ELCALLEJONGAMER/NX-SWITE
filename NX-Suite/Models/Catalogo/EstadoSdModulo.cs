namespace NX_Swite.Models
{
    /// <summary>Estado de instalaci�n del m�dulo en la microSD seleccionada.</summary>
    public enum EstadoSdModulo
    {
        NoInstalado,
        ParcialmenteInstalado,
        Instalado,

        /// <summary>
        /// Existe evidencia f�sica suficientemente fuerte de que el m�dulo est� presente
        /// en la SD (ruta exclusiva de este m�dulo en el cat�logo actual, con SHA256
        /// declarado), pero el SHA256 real del archivo no coincide con ninguna versi�n
        /// conocida del m�dulo. NO implica que el contenido sea v�lido o compatible,
        /// solo que su versi�n/contenido no pudo ser identificado.
        /// </summary>
        InstaladoVersionDesconocida
    }
}
