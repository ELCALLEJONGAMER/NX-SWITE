using DiscUtils.Fat;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.Save;
using NX_Swite.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Path       = System.IO.Path;
using FileStream = System.IO.FileStream;
using FileMode   = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;

namespace NX_Swite.Core
{
    public sealed class SaveEmuMmc
    {
        public string   ArchivoFat  { get; init; } = string.Empty;
        public string   TitleId     { get; init; } = string.Empty;
        public Guid     UsuarioId   { get; init; }
        public long     Tamano      { get; init; }
        public DateTime? Fecha      { get; init; }
        public string   Nombre      { get; set; } = string.Empty;
        public string   UsuarioCorto => UsuarioId.ToString("N")[..8].ToUpperInvariant();
        public string   TamanoTexto  => Tamano >= 1024 * 1024 ? $"{Tamano / (1024 * 1024.0):0.#} MB" : $"{Tamano / 1024} KB";
    }

    /// <summary>
    /// Lectura (solo lectura) de los saves de juego de una emuMMC.
    /// Descifra la partición USER (BIS key 2, AES-XTS) con prod.keys,
    /// recorre su FAT con DiscUtils y abre cada save con LibHac.
    /// Nunca escribe en la emuMMC.
    /// </summary>
    public sealed class EmuMmcSavesLogic : IDisposable
    {
        private const int  BytesSector    = 512;
        private const long SectoresBoot   = 0x4000;
        private const int  SectorXts      = 0x4000;

        private readonly List<IDisposable> _recursos = new();
        private readonly KeySet   _keys;
        private readonly FatFileSystem _fat;

        private EmuMmcSavesLogic(KeySet keys, FatFileSystem fat)
        {
            _keys = keys;
            _fat  = fat;
        }

        /// <summary>
        /// Abre la partición USER de la emuMMC de la SD indicada.
        /// <paramref name="discoFisico"/> solo se usa para emuMMC en partición.
        /// </summary>
        public static EmuMmcSavesLogic Abrir(string raizSd, int discoFisico)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string prodKeys = Path.Combine(raizSd, "switch", "prod.keys");
            if (!File.Exists(prodKeys))
                throw new InvalidOperationException("No se encontró switch/prod.keys en la microSD.");

            var info = EmuMmcInfoLogic.Leer(raizSd);
            if (!info.ExisteIni || info.Tipo == TipoEmuMmc.NoConfigurada)
                throw new InvalidOperationException("La microSD no tiene emuMMC configurada.");

            var keys = ExternalKeyReader.ReadKeyFile(prodKeys, null, null, null, KeySet.Mode.Prod);
            var bisUser = keys.BisKeys[2];
            if (bisUser.DataRo.ToArray().All(b => b == 0))
                throw new InvalidOperationException("prod.keys no contiene bis_key_02.");

            var recursos = new List<IDisposable>();
            IStorage rawnand;
            if (info.Tipo == TipoEmuMmc.Particion)
            {
                var fs = new FileStream($@"\\.\PhysicalDrive{discoFisico}", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                recursos.Add(fs);
                var disco = new StreamStorage(fs, false);
                rawnand = disco.Slice((info.Sector!.Value + SectoresBoot) * BytesSector);
            }
            else
            {
                string carpeta = Path.Combine(raizSd, info.RutaArchivos!.Replace('/', '\\').TrimStart('\\'), "eMMC");
                var partes = Directory.EnumerateFiles(carpeta)
                    .Where(f => Path.GetFileName(f).Length == 2 && Path.GetFileName(f).All(char.IsDigit))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f =>
                    {
                        var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        recursos.Add(s);
                        return (IStorage)new StreamStorage(s, false);
                    })
                    .ToList();
                if (partes.Count == 0)
                    throw new InvalidOperationException($"No se encontraron los archivos de la emuMMC en {carpeta}.");
                rawnand = new ConcatenationStorage(partes, false);
            }

            var (inicio, fin) = BuscarParticionUser(rawnand);
            IStorage user = rawnand.Slice(inicio * BytesSector, (fin - inicio + 1) * BytesSector);
            user = new CachedStorage(user, SectorXts, 4, false);

            var clave = bisUser.DataRo.ToArray();
            IStorage descifrada = new Aes128XtsStorage(user, clave, SectorXts, false, true);
            descifrada = new CachedStorage(descifrada, SectorXts, 16, false);

            var stream = descifrada.AsStream(FileAccess.Read);
            recursos.Add(stream);
            var fat = new FatFileSystem(stream);
            recursos.Add(fat);

            var r = new EmuMmcSavesLogic(keys, fat);
            r._recursos.AddRange(recursos);
            return r;
        }

        private static (long Inicio, long Fin) BuscarParticionUser(IStorage rawnand)
        {
            var cabecera = new byte[BytesSector];
            rawnand.Read(BytesSector, cabecera).ThrowIfFailure();
            if (System.Text.Encoding.ASCII.GetString(cabecera, 0, 8) != "EFI PART")
                throw new InvalidOperationException("No se encontró la tabla GPT de la emuMMC.");

            var entradas = new byte[128 * 128];
            rawnand.Read(2 * BytesSector, entradas).ThrowIfFailure();
            for (int i = 0; i < 128; i++)
            {
                int o = i * 128;
                string nombre = System.Text.Encoding.Unicode.GetString(entradas, o + 56, 72).TrimEnd('\0');
                if (nombre == "USER")
                    return (BitConverter.ToInt64(entradas, o + 32), BitConverter.ToInt64(entradas, o + 40));
            }
            throw new InvalidOperationException("No se encontró la partición USER en la emuMMC.");
        }

        /// <summary>Lista los saves de juegos (tipo Account) de la emuMMC.</summary>
        public List<SaveEmuMmc> ListarSavesDeJuegos(CancellationToken ct = default)
        {
            var lista = new List<SaveEmuMmc>();
            if (!_fat.DirectoryExists(@"\save")) return lista;

            foreach (string archivo in _fat.GetFiles(@"\save"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var save = AbrirSave(archivo);
                    var extra = save.Header.ExtraData;
                    if (extra.Type != SaveDataType.Account) continue;
                    lista.Add(new SaveEmuMmc
                    {
                        ArchivoFat = archivo,
                        TitleId    = extra.TitleId.ToString("X16"),
                        UsuarioId  = extra.UserId,
                        Tamano     = _fat.GetFileLength(archivo),
                        Fecha      = extra.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(extra.Timestamp).LocalDateTime : null,
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[emuMMC] Save ilegible {archivo}: {ex.Message}");
                }
            }
            return lista;
        }

        /// <summary>
        /// Extrae el contenido del save (estructura JKSV/Checkpoint) en <paramref name="carpetaDestino"/>.
        /// </summary>
        public void ExportarSave(SaveEmuMmc save, string carpetaDestino)
        {
            Directory.CreateDirectory(carpetaDestino);
            using var fs = AbrirSave(save.ArchivoFat);
            using var destino = new LocalFileSystem(carpetaDestino);
            fs.CopyDirectory(destino, "/", "/", null, CreateFileOptions.None).ThrowIfFailure();
        }

        private SaveDataFileSystem AbrirSave(string archivoFat)
        {
            var ms = new MemoryStream();
            using (var origen = _fat.OpenFile(archivoFat, FileMode.Open, FileAccess.Read))
                origen.CopyTo(ms);
            ms.Position = 0;
            return new SaveDataFileSystem(_keys, new StreamStorage(ms, false), IntegrityCheckLevel.None, false);
        }

        public void Dispose()
        {
            for (int i = _recursos.Count - 1; i >= 0; i--)
                try { _recursos[i].Dispose(); } catch { }
            _recursos.Clear();
        }
    }
}
