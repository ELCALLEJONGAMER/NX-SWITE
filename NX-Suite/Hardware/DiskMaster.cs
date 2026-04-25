namespace NX_Suite.Hardware
{
    /// <summary>
    /// Punto de entrada único para todas las operaciones de hardware sobre la
    /// microSD: detección plug &amp; play, escaneo de unidades, particionado
    /// FAT32 silencioso y "sniper" de ventanas modales de Windows.
    ///
    /// La implementación está distribuida en archivos parciales por
    /// responsabilidad, todos en esta misma carpeta:
    /// <list type="bullet">
    ///   <item><c>DiskMaster.Notificaciones.cs</c> – evento <c>UnidadConectada</c> y hook WM_DEVICECHANGE.</item>
    ///   <item><c>DiskMaster.Escaneo.cs</c>        – <c>ObtenerUnidadesRemovibles()</c> + WMI.</item>
    ///   <item><c>DiskMaster.Particionado.cs</c>   – <c>ParticionarYFormatearAsync()</c> y helpers (diskpart + fat32format).</item>
    ///   <item><c>DiskMaster.Sniper.cs</c>         – <c>EjecutarSniper()</c> + P/Invoke user32.</item>
    ///   <item><c>DiskMaster.Native.cs</c>         – P/Invoke kernel32 disco compartidos (CreateFile / DeviceIoControl) + <c>GetPhysicalDiskNumber</c>.</item>
    /// </list>
    /// El modelo <see cref="SDInfo"/> vive en <c>SDInfo.cs</c> dentro de esta misma carpeta.
    /// </summary>
    public partial class DiskMaster
    {
    }
}
