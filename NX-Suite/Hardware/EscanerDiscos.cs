using NX_Suite.Hardware.Native;
using System.Collections.Generic;
using System.IO;
using System.Management;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// Escaneo de unidades extraíbles del sistema. Combina <see cref="DriveInfo"/>
    /// (etiqueta, capacidad, formato), WMI (serial de volumen) y
    /// <see cref="DiscoNativo"/> (índice del disco físico).
    /// </summary>
    public class EscanerDiscos
    {
        public List<SDInfo> ObtenerUnidadesRemovibles()
        {
            var lista = new List<SDInfo>();
            DriveInfo[] unidades = DriveInfo.GetDrives();

            foreach (DriveInfo d in unidades)
            {
                if (d.DriveType != DriveType.Removable || !d.IsReady) continue;

                var info = new SDInfo { Letra = d.Name };
                try
                {
                    info.Etiqueta       = string.IsNullOrEmpty(d.VolumeLabel) ? "Disco Extraíble" : d.VolumeLabel;
                    info.CapacidadTotal = (d.TotalSize / 1024 / 1024 / 1024).ToString();
                    info.Formato        = d.DriveFormat;
                    info.Serial         = ObtenerSerialWMI(d.Name.Substring(0, 2));
                    info.DiscoFisico    = DiscoNativo.GetPhysicalDiskNumber(d.Name);
                    lista.Add(info);
                }
                catch
                {
                    // Ignorar unidades que dan error al leer, probablemente no son válidas
                }
            }
            return lista;
        }

        private static string ObtenerSerialWMI(string letra)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = '{letra}'");
                foreach (ManagementObject obj in searcher.Get())
                    return obj["VolumeSerialNumber"]?.ToString() ?? "N/A";
            }
            catch { return "N/A"; }
            return "N/A";
        }
    }
}
