using System.Collections.Generic;
using System.IO;
using System.Management;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// Escaneo de unidades extraíbles del sistema. Usa <see cref="DriveInfo"/>
    /// y WMI para obtener etiqueta, capacidad, formato y serial; combina con
    /// el P/Invoke nativo (<see cref="DiskMaster.Native.cs"/>) para resolver
    /// el índice del disco físico.
    /// </summary>
    public partial class DiskMaster
    {
        public List<SDInfo> ObtenerUnidadesRemovibles()
        {
            List<SDInfo> lista = new List<SDInfo>();
            DriveInfo[] unidades = DriveInfo.GetDrives();

            foreach (DriveInfo d in unidades)
            {
                if (d.DriveType != DriveType.Removable) continue;

                SDInfo info = new SDInfo { Letra = d.Name };
                try
                {
                    if (d.IsReady)
                    {
                        info.Etiqueta       = string.IsNullOrEmpty(d.VolumeLabel) ? "Disco Extraíble" : d.VolumeLabel;
                        info.CapacidadTotal = (d.TotalSize / 1024 / 1024 / 1024).ToString();
                        info.Formato        = d.DriveFormat;
                    }
                    else // Memorias RAW
                    {
                        info.Etiqueta       = "Sin Formato (RAW)";
                        info.CapacidadTotal = "0";
                        info.Formato        = "RAW";
                    }
                    info.Serial      = ObtenerSerialWMI(d.Name.Substring(0, 2));
                    info.DiscoFisico = GetPhysicalDiskNumber(d.Name);
                    lista.Add(info);
                }
                catch
                {
                    info.Etiqueta       = "Inaccesible";
                    info.CapacidadTotal = "0";
                    info.Formato        = "RAW";
                    lista.Add(info);
                }
            }
            return lista;
        }

        private string ObtenerSerialWMI(string letra)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = '{letra}'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                        return obj["VolumeSerialNumber"]?.ToString() ?? "N/A";
                }
            }
            catch { return "N/A"; }
            return "N/A";
        }
    }
}
