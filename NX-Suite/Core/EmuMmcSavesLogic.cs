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
        public string   Perfil      { get; set; } = string.Empty;
        public string?  IconoUrl    { get; set; }
        public string   UsuarioCorto => UsuarioId.ToString("N")[..8].ToUpperInvariant();
        public string   TamanoTexto  => Tamano >= 1024 * 1024 ? $"{Tamano / (1024 * 1024.0):0.#} MB" : $"{Tamano / 1024} KB";
    }

    public sealed class PerfilEmuMmc
    {
        public string  Uid    { get; init; } = string.Empty;
        public string  Apodo  { get; init; } = string.Empty;
        public byte[]? Avatar { get; init; }
        public IReadOnlyList<Guid> Variantes { get; init; } = Array.Empty<Guid>();
    }

    public sealed class EspacioParticion
    {
        public long Total { get; init; }
        public long Libre { get; init; }
        public long Usado => Math.Max(0, Total - Libre);
        public double Porcentaje => Total <= 0 ? 0 : Usado * 100d / Total;
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
        private FatFileSystem? _fatSystem;

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

            var fat = MontarParticion(rawnand, "USER", bisUser.DataRo.ToArray(), recursos);
            var r = new EmuMmcSavesLogic(keys, fat);

            // SYSTEM (bis_key_03) contiene el save 8000000000000010 con los perfiles.
            try
            {
                var bisSystem = keys.BisKeys[3].DataRo.ToArray();
                if (bisSystem.Any(b => b != 0))
                    r._fatSystem = MontarParticion(rawnand, "SYSTEM", bisSystem, recursos);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] No se pudo montar SYSTEM: {ex.Message}");
            }

            r._recursos.AddRange(recursos);
            return r;
        }

        private static FatFileSystem MontarParticion(IStorage rawnand, string nombre, byte[] clave, List<IDisposable> recursos)
        {
            var (inicio, fin) = BuscarParticion(rawnand, nombre);
            IStorage part = rawnand.Slice(inicio * BytesSector, (fin - inicio + 1) * BytesSector);
            part = new CachedStorage(part, SectorXts, 4, false);
            IStorage descifrada = new Aes128XtsStorage(part, clave, SectorXts, false, true);
            descifrada = new CachedStorage(descifrada, SectorXts, 16, false);

            var stream = descifrada.AsStream(FileAccess.Read);
            recursos.Add(stream);
            var fat = new FatFileSystem(stream);
            recursos.Add(fat);
            return fat;
        }

        private static (long Inicio, long Fin) BuscarParticion(IStorage rawnand, string particion)
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
                if (nombre == particion)
                    return (BitConverter.ToInt64(entradas, o + 32), BitConverter.ToInt64(entradas, o + 40));
            }
            throw new InvalidOperationException($"No se encontró la partición {particion} en la emuMMC.");
        }

        /// <summary>
        /// Lee los perfiles de la consola (save del sistema 8000000000000010,
        /// <c>/su/avators/profiles.dat</c>). Devuelve UserId → apodo.
        /// </summary>
        public Dictionary<Guid, string> LeerPerfiles()
        {
            var r = new Dictionary<Guid, string>();
            foreach (var p in LeerPerfilesCompletos())
                foreach (var g in p.Variantes) r.TryAdd(g, p.Apodo);
            return r;
        }

        public EspacioParticion EspacioUser()   => Espacio(_fat);

        /// <summary>
        /// Copia (solo lectura) el álbum de la memoria interna (<c>USER:\Album</c>) a
        /// <paramref name="carpetaCache"/>; omite los archivos ya copiados con el mismo tamaño.
        /// </summary>
        public List<ArchivoAlbum> ExtraerAlbumConsola(string carpetaCache, CancellationToken ct = default)
        {
            var r = new List<ArchivoAlbum>();
            if (!_fat.DirectoryExists(@"\Album")) return r;
            foreach (string archivo in _fat.GetFiles(@"\Album", "*.*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (!EmuMmcAlbumLogic.EsArchivoAlbum(archivo)) continue;
                try
                {
                    string destino = Path.Combine(carpetaCache, archivo.TrimStart('\\'));
                    long tam = _fat.GetFileLength(archivo);
                    if (!File.Exists(destino) || new FileInfo(destino).Length != tam)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                        using var origen = _fat.OpenFile(archivo, FileMode.Open, FileAccess.Read);
                        using var salida = File.Create(destino);
                        origen.CopyTo(salida);
                    }
                    var fecha = EmuMmcAlbumLogic.FechaDesdeNombre(archivo) ?? _fat.GetLastWriteTime(archivo);
                    r.Add(new ArchivoAlbum
                    {
                        Ruta = destino, Fecha = fecha, Tamano = tam,
                        EsVideo = EmuMmcAlbumLogic.EsVideo(archivo), Origen = OrigenAlbum.Consola,
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[emuMMC] Álbum: no se pudo leer {archivo}: {ex.Message}");
                }
            }
            return r;
        }
        public EspacioParticion? EspacioSystem() => _fatSystem == null ? null : Espacio(_fatSystem);

        private static EspacioParticion Espacio(FatFileSystem fat)
        {
            try { return new EspacioParticion { Total = fat.Size, Libre = fat.AvailableSpace }; }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] No se pudo calcular el espacio: {ex.Message}");
                return new EspacioParticion();
            }
        }

        /// <summary>
        /// Perfiles de la consola con su apodo y avatar (JPG en <c>/su/avators/&lt;uid&gt;.jpg</c>).
        /// </summary>
        public List<PerfilEmuMmc> LeerPerfilesCompletos()
        {
            var perfiles = new List<PerfilEmuMmc>();
            const string nombreSave = "8000000000000010";
            string temp = Path.Combine(Path.GetTempPath(), $"nxs_perfiles_{Guid.NewGuid():N}");
            try
            {
                // DiscUtils devuelve nombres cortos 8.3 (800000~1), se identifica por SaveDataId.
                if (_fatSystem == null) { Logger.Warning("[emuMMC] Perfiles: partición SYSTEM no disponible"); return perfiles; }
                ulong idBuscado = Convert.ToUInt64(nombreSave, 16);
                string? archivo = _fatSystem.GetFiles(@"\save")
                    .Where(f => Path.GetFileName(f).StartsWith("8", StringComparison.Ordinal))
                    .FirstOrDefault(f =>
                    {
                        try { using var s = AbrirSave(_fatSystem, f); return s.Header.ExtraData.SaveId == idBuscado; }
                        catch { return false; }
                    });
                if (archivo == null) { Logger.Warning("[emuMMC] Perfiles: no existe el save 8000000000000010"); return perfiles; }
                Directory.CreateDirectory(temp);
                using (var fs = AbrirSave(_fatSystem, archivo))
                using (var destino = new LocalFileSystem(temp))
                    fs.CopyDirectory(destino, "/", "/", null, CreateFileOptions.None).ThrowIfFailure();

                string dat = Path.Combine(temp, "su", "avators", "profiles.dat");
                if (!File.Exists(dat))
                {
                    var hay = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(temp, f)).Take(20);
                    Logger.Warning($"[emuMMC] Perfiles: falta profiles.dat. Contenido: {string.Join(", ", hay)}");
                    return perfiles;
                }
                byte[] b = File.ReadAllBytes(dat);
                Logger.Info($"[emuMMC] Perfiles: profiles.dat {b.Length} bytes");
                string carpetaAv = Path.Combine(temp, "su", "avators");
                var avatares = Directory.EnumerateFiles(carpetaAv, "*.jpg")
                    .GroupBy(a => Path.GetFileNameWithoutExtension(a).Replace("-", "").ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                // Cabecera 0x10 + 8 entradas de 0xC8: Uid(0x10) + ProfileBase(0x38) + UserData(0x80).
                for (int i = 0; i < 8; i++)
                {
                    int o = 0x10 + i * 0xC8;
                    if (o + 0xC8 > b.Length) break;
                    var uid = b.AsSpan(o, 0x10);
                    if (uid.IndexOfAnyExcept((byte)0) < 0) continue;
                    var nick = b.AsSpan(o + 0x28, 0x20);
                    int fin = nick.IndexOf((byte)0);
                    string apodo = System.Text.Encoding.UTF8.GetString(fin < 0 ? nick : nick[..fin]);
                    Logger.Info($"[emuMMC] Perfil {Convert.ToHexString(uid)} = {apodo}");
                    var variantes = VariantesUid(uid).ToList();
                    string? jpg = variantes.Select(g => g.ToString("N").ToUpperInvariant())
                        .Append(Convert.ToHexString(uid))
                        .Select(k => avatares.GetValueOrDefault(k))
                        .FirstOrDefault(f => f != null);
                    perfiles.Add(new PerfilEmuMmc
                    {
                        Uid = Convert.ToHexString(uid), Apodo = apodo, Variantes = variantes,
                        Avatar = jpg != null ? File.ReadAllBytes(jpg) : null,
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] No se pudieron leer los perfiles: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
            return perfiles;
        }

        // El Uid son 2 u64; según la librería puede mapearse al Guid en orden directo o invertido.
        private static IEnumerable<Guid> VariantesUid(ReadOnlySpan<byte> uid)
        {
            var a = uid.ToArray();
            var b = a[8..].Concat(a[..8]).ToArray();
            var c = a.Reverse().ToArray();
            return new[] { Guid.ParseExact(Convert.ToHexString(a), "N"), new Guid(a), new Guid(b), new Guid(c), new Guid(b.Reverse().ToArray()) };
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
                    Logger.Info($"[emuMMC] Save {archivo}: tipo {extra.Type}, title {extra.TitleId:X16}, user {extra.UserId}");
                    if (extra.Type != SaveDataType.Account && extra.Type != SaveDataType.Device) continue;
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

        private SaveDataFileSystem AbrirSave(string archivoFat) => AbrirSave(_fat, archivoFat);

        private SaveDataFileSystem AbrirSave(FatFileSystem fat, string archivoFat)
        {
            var ms = new MemoryStream();
            using (var origen = fat.OpenFile(archivoFat, FileMode.Open, FileAccess.Read))
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
