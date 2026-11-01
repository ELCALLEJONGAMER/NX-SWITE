using NX_Swite.Hardware.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace NX_Swite.Hardware
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

        /// <summary>
        /// Solicita la expulsión segura de la unidad extraíble indicada.
        /// Equivale a "Quitar hardware con seguridad" en la bandeja del sistema.
        /// Devuelve <c>true</c> si Windows aceptó la expulsión.
        /// </summary>
        public static bool ExpulsarUnidad(string letraRaiz)
            => Native.DiscoNativo.ExpulsarUnidad(letraRaiz);

        /// <summary>
        /// Lista discos físicos extraíbles (USB/SD/MMC) aunque no tengan letra,
        /// estén en RAW o con la tabla de particiones dañada. Excluye siempre los
        /// discos de sistema/arranque. Usado por «Recuperar Micro SD».
        /// </summary>
        public static List<DiscoRecuperable> ObtenerDiscosRecuperables()
        {
            var lista = new List<DiscoRecuperable>();
            var extraibles = ObtenerIndicesMedioExtraible();
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage",
                    "SELECT Number, FriendlyName, Size, BusType, IsSystem, IsBoot, PartitionStyle, NumberOfPartitions FROM MSFT_Disk");
                foreach (ManagementObject d in searcher.Get())
                {
                    int bus = Convert.ToInt32(d["BusType"] ?? 0);
                    // 7 = USB, 12 = SD, 13 = MMC
                    if (bus != 7 && bus != 12 && bus != 13) continue;
                    if (d["IsSystem"] is true || d["IsBoot"] is true) continue;
                    // Excluir HDD/SSD externos: solo lectores de tarjetas/pendrives
                    // que Windows reporta como «Removable Media».
                    if (!extraibles.Contains(Convert.ToInt32(d["Number"]))) continue;

                    ulong size = Convert.ToUInt64(d["Size"] ?? 0UL);
                    if (size == 0) continue; // lector sin tarjeta

                    lista.Add(new DiscoRecuperable
                    {
                        Numero      = Convert.ToInt32(d["Number"]),
                        Nombre      = d["FriendlyName"]?.ToString() ?? "Disco extraíble",
                        TamanoGb    = Math.Round(size / 1024d / 1024 / 1024, 1),
                        Particiones = Convert.ToInt32(d["NumberOfPartitions"] ?? 0),
                        Estilo      = Convert.ToInt32(d["PartitionStyle"] ?? 0) switch { 1 => "MBR", 2 => "GPT", _ => "RAW" },
                    });
                }
            }
            catch { }

            foreach (var disco in lista)
                disco.Letras = string.Join(" ", DriveInfo.GetDrives()
                    .Where(dr => { try { return Native.DiscoNativo.GetPhysicalDiskNumber(dr.Name) == disco.Numero; } catch { return false; } })
                    .Select(dr => dr.Name.TrimEnd('\\')));

            return lista.OrderBy(d => d.Numero).ToList();
        }

        /// <summary>
        /// Índices de discos físicos cuyo medio es extraíble (Win32_DiskDrive
        /// MediaType «Removable Media»). Los HDD/SSD externos se reportan como
        /// «External hard disk media» y quedan fuera.
        /// </summary>
        private static HashSet<int> ObtenerIndicesMedioExtraible()
        {
            var set = new HashSet<int>();
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Index, MediaType FROM Win32_DiskDrive");
                foreach (ManagementObject d in s.Get())
                {
                    string media = d["MediaType"]?.ToString() ?? string.Empty;
                    if (media.StartsWith("Removable", StringComparison.OrdinalIgnoreCase))
                        set.Add(Convert.ToInt32(d["Index"]));
                }
            }
            catch { }
            return set;
        }
    }

    public class DiscoRecuperable
    {
        public int    Numero      { get; set; }
        public string Nombre      { get; set; } = string.Empty;
        public double TamanoGb    { get; set; }
        public int    Particiones { get; set; }
        public string Estilo      { get; set; } = string.Empty;
        public string Letras      { get; set; } = string.Empty;

        public string Descripcion =>
            $"Disco {Numero} — {Nombre} — {TamanoGb} GB — {Estilo}, {Particiones} partición(es) — " +
            (string.IsNullOrEmpty(Letras) ? "SIN LETRA" : $"letra {Letras}");
    }
}
