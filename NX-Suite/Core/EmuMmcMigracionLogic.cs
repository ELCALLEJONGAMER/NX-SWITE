using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    public sealed class ParticionMbr
    {
        public int  Indice   { get; init; }
        public byte Tipo     { get; init; }
        public long Inicio   { get; init; }
        public long Sectores { get; init; }
        public bool Contiene(long sector) => sector >= Inicio && sector < Inicio + Sectores;
    }

    /// <summary>Qué se copia de la microSD al PC.</summary>
    public enum AlcanceCopia
    {
        /// <summary>Toda la SD (migración).</summary>
        SdCompleta,
        /// <summary>Solo la emuMMC (sistema, saves) sin juegos instalados en la SD.</summary>
        EmuMmc,
        /// <summary>emuMMC + juegos instalados en la SD (Nintendo/Contents).</summary>
        EmuMmcConJuegos,
    }

    /// <summary>Encaje de la partición emuMMC del respaldo en la SD destino.</summary>
    public enum EncajeParticion
    {
        /// <summary>emuMMC por archivos: no depende de particiones.</summary>
        NoAplica,
        /// <summary>Partición del mismo tamaño.</summary>
        Exacta,
        /// <summary>Partición mayor: se restaura y queda espacio sin usar al final.</summary>
        Mayor,
        /// <summary>Partición más pequeña: hay que reparticionar (borra la SD).</summary>
        Insuficiente,
        /// <summary>No hay partición emuMMC: hay que reparticionar (borra la SD).</summary>
        Ausente,
    }

    public sealed class ManifiestoMigracion
    {
        public int        Version        { get; set; } = 2;
        public DateTime   Fecha          { get; set; }
        public string     SerialOrigen   { get; set; } = string.Empty;
        public TipoEmuMmc Tipo           { get; set; }
        public AlcanceCopia Alcance      { get; set; }
        public string?    Firmware       { get; set; }
        public string?    RutaNintendo   { get; set; }
        public long       BytesArchivos  { get; set; }
        public long       BytesJuegos    { get; set; }
        public int        NumArchivos    { get; set; }
        public long?      SectorOrigen   { get; set; }
        public long?      InicioParticionOrigen { get; set; }
        public long       BytesRaw       { get; set; }
        public string?    HashRaw        { get; set; }

        public bool IncluyeJuegos => Alcance != AlcanceCopia.EmuMmc && BytesJuegos > 0;
        public long BytesTotales  => BytesArchivos + BytesRaw;

        /// <summary>GB (GiB enteros) que debe tener la partición emuMMC del destino.</summary>
        public int GbEmuMmcNecesarios => BytesRaw <= 0 ? 0 : (int)Math.Ceiling(BytesRaw / (1024d * 1024 * 1024));
    }

    public sealed class EstimacionCopia
    {
        public TipoEmuMmc Tipo        { get; init; }
        public long       BytesEmuMmc { get; init; }
        public long       BytesJuegos { get; init; }
        public long       BytesRaw    { get; init; }
    }

    /// <summary>
    /// Migración de una microSD con emuMMC a otra usando un único lector:
    /// la SD origen se copia (solo lectura) a una carpeta del PC y después se
    /// restaura en la SD destino conservando el tipo de emuMMC.
    /// </summary>
    public static class EmuMmcMigracionLogic
    {
        public const string NombreManifiesto = "manifiesto.json";
        public const string CarpetaArchivos  = "sd";
        public const string NombreRaw        = "emummc.raw";
        private const int   BytesSector      = 512;
        private const int   Bloque           = 4 * 1024 * 1024;
        private const byte  TipoEmuMmcMbr    = 0xE0;

        private static readonly string[] Excluidas = { "System Volume Information", "$RECYCLE.BIN" };
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        // ── Lógica pura (testeable) ───────────────────────────────────────────

        public static List<ParticionMbr> ParsearMbr(byte[] sector0)
        {
            var r = new List<ParticionMbr>();
            if (sector0.Length < 512 || sector0[510] != 0x55 || sector0[511] != 0xAA) return r;
            for (int i = 0; i < 4; i++)
            {
                int o = 446 + i * 16;
                byte tipo = sector0[o + 4];
                long inicio = BitConverter.ToUInt32(sector0, o + 8);
                long sectores = BitConverter.ToUInt32(sector0, o + 12);
                if (tipo != 0 && sectores > 0)
                    r.Add(new ParticionMbr { Indice = i, Tipo = tipo, Inicio = inicio, Sectores = sectores });
            }
            return r;
        }

        /// <summary>Sustituye (o añade) la clave <c>sector</c> de emummc.ini conservando el resto.</summary>
        public static List<string> ReemplazarSectorIni(IEnumerable<string> lineas, long sector)
        {
            var r = new List<string>();
            bool hecho = false;
            foreach (var l in lineas)
            {
                string t = l.TrimStart();
                int i = t.IndexOf('=');
                if (!hecho && i > 0 && t[..i].Trim().Equals("sector", StringComparison.OrdinalIgnoreCase))
                {
                    r.Add($"sector=0x{sector:x}");
                    hecho = true;
                }
                else r.Add(l);
            }
            if (!hecho) r.Add($"sector=0x{sector:x}");
            return r;
        }

        public static bool EsRutaExcluida(string relativa)
        {
            string primera = relativa.Split('\\', '/')[0];
            return Excluidas.Any(e => e.Equals(primera, StringComparison.OrdinalIgnoreCase));
        }

        public static ManifiestoMigracion LeerManifiesto(string carpeta)
        {
            string ruta = Path.Combine(carpeta, NombreManifiesto);
            if (!File.Exists(ruta))
                throw new InvalidOperationException("La carpeta no contiene una copia de migración (falta manifiesto.json).");
            return JsonSerializer.Deserialize<ManifiestoMigracion>(File.ReadAllText(ruta))
                ?? throw new InvalidOperationException("manifiesto.json no es válido.");
        }

        /// <summary>
        /// Decide si un archivo (ruta relativa a la raíz de la SD) entra en la copia.
        /// Los juegos son <c>{nintendo_path}/Contents</c>; el resto de la carpeta
        /// Nintendo (saves en SD, álbum) siempre acompaña a la emuMMC.
        /// </summary>
        public static bool IncluirEnCopia(string relativa, AlcanceCopia alcance, string? rutaNintendo)
        {
            string r = Normalizar(relativa);
            if (EsRutaExcluida(r)) return false;
            if (alcance == AlcanceCopia.SdCompleta) return true;

            string? nin = string.IsNullOrWhiteSpace(rutaNintendo) ? null : Normalizar(rutaNintendo);
            bool enEmu = EmpiezaPor(r, "emuMMC");
            bool enNin = nin != null && EmpiezaPor(r, nin);
            if (!enEmu && !enNin) return false;
            return alcance == AlcanceCopia.EmuMmcConJuegos || !EsJuego(r, nin);
        }

        public static bool EsJuego(string relativa, string? rutaNintendo)
        {
            if (string.IsNullOrWhiteSpace(rutaNintendo)) return false;
            string r = Normalizar(relativa);
            string contents = Normalizar(rutaNintendo) + "\\Contents";
            // Los archivos sueltos de Contents (private, private1) no son juegos:
            // vinculan la carpeta Nintendo con la emuMMC y sin ellos la Switch
            // la considera dañada.
            return EmpiezaPor(r, contents) && r.Length > contents.Length
                   && r[(contents.Length + 1)..].Contains('\\');
        }

        public static EncajeParticion EvaluarEncaje(ManifiestoMigracion m, IReadOnlyList<ParticionMbr> particiones)
        {
            if (m.Tipo != TipoEmuMmc.Particion) return EncajeParticion.NoAplica;
            var p = particiones.FirstOrDefault(x => x.Tipo == TipoEmuMmcMbr);
            if (p == null) return EncajeParticion.Ausente;
            long bytes = p.Sectores * BytesSector;
            return bytes == m.BytesRaw ? EncajeParticion.Exacta
                 : bytes > m.BytesRaw  ? EncajeParticion.Mayor
                 : EncajeParticion.Insuficiente;
        }

        /// <summary>Compara versiones de firmware ("18.1.0"). Null si alguna es desconocida.</summary>
        public static int? CompararFirmware(string? a, string? b)
            => Version.TryParse(a, out var va) && Version.TryParse(b, out var vb) ? va.CompareTo(vb) : null;

        /// <summary>Respaldos válidos (con manifiesto) dentro de <paramref name="raiz"/>, más recientes primero.</summary>
        public static List<(string Carpeta, ManifiestoMigracion Manifiesto)> ListarRespaldos(string raiz)
        {
            var r = new List<(string, ManifiestoMigracion)>();
            if (!Directory.Exists(raiz)) return r;
            foreach (var d in Directory.EnumerateDirectories(raiz))
            {
                try { r.Add((d, LeerManifiesto(d))); }
                catch { /* carpeta ajena o incompleta */ }
            }
            return r.OrderByDescending(x => x.Item2.Fecha).ToList();
        }

        public static List<ParticionMbr> LeerParticiones(int disco) => LeerMbr(disco);

        public static string FormatearTamano(long bytes)
            => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.0} GB" : $"{bytes / (1024d * 1024):0} MB";

        /// <summary>Tamaños de lo que se copiaría (sin copiar nada).</summary>
        public static EstimacionCopia Estimar(string raizSd, int discoFisico)
        {
            var info = EmuMmcInfoLogic.Leer(raizSd);
            if (!info.ExisteIni || info.Tipo == TipoEmuMmc.NoConfigurada)
                throw new InvalidOperationException("La microSD no tiene emuMMC configurada.");
            long emu = 0, juegos = 0;
            foreach (var f in ListarArchivos(raizSd))
            {
                string rel = Path.GetRelativePath(raizSd, f.FullName);
                if (!IncluirEnCopia(rel, AlcanceCopia.EmuMmcConJuegos, info.RutaNintendo)) continue;
                if (EsJuego(rel, info.RutaNintendo)) juegos += f.Length; else emu += f.Length;
            }
            long raw = 0;
            if (info.Tipo == TipoEmuMmc.Particion)
                raw = (LeerMbr(discoFisico).FirstOrDefault(p => p.Contiene(info.Sector!.Value))?.Sectores ?? 0) * BytesSector;
            return new EstimacionCopia { Tipo = info.Tipo, BytesEmuMmc = emu, BytesJuegos = juegos, BytesRaw = raw };
        }

        private static string Normalizar(string ruta)
            => ruta.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);

        private static bool EmpiezaPor(string ruta, string prefijo)
            => ruta.Equals(prefijo, StringComparison.OrdinalIgnoreCase)
               || ruta.StartsWith(prefijo + "\\", StringComparison.OrdinalIgnoreCase);

        // ── Fase 1: SD origen → PC ────────────────────────────────────────────

        public static async Task<ManifiestoMigracion> CopiarOrigenAPcAsync(
            string raizSd, int discoFisico, string serial, string carpetaDestino,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
            => await CopiarAPcAsync(raizSd, discoFisico, serial, carpetaDestino, AlcanceCopia.SdCompleta, null, progreso, ct);

        public static async Task<ManifiestoMigracion> CopiarAPcAsync(
            string raizSd, int discoFisico, string serial, string carpetaDestino,
            AlcanceCopia alcance, string? firmware,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
        {
            var info = EmuMmcInfoLogic.Leer(raizSd);
            if (!info.ExisteIni || info.Tipo == TipoEmuMmc.NoConfigurada)
                throw new InvalidOperationException("La microSD no tiene emuMMC configurada.");

            var m = new ManifiestoMigracion
            {
                Fecha = DateTime.Now, SerialOrigen = serial, Tipo = info.Tipo,
                Alcance = alcance, Firmware = firmware, RutaNintendo = info.RutaNintendo,
            };

            ParticionMbr? part = null;
            if (info.Tipo == TipoEmuMmc.Particion)
            {
                part = LeerMbr(discoFisico).FirstOrDefault(p => p.Contiene(info.Sector!.Value))
                    ?? throw new InvalidOperationException("No se encontró la partición que contiene la emuMMC.");
                m.SectorOrigen = info.Sector;
                m.InicioParticionOrigen = part.Inicio;
                m.BytesRaw = part.Sectores * BytesSector;
            }

            var archivos = ListarArchivos(raizSd)
                .Where(f => IncluirEnCopia(Path.GetRelativePath(raizSd, f.FullName), alcance, info.RutaNintendo))
                .ToList();
            m.NumArchivos = archivos.Count;
            m.BytesArchivos = archivos.Sum(a => a.Length);
            m.BytesJuegos = archivos.Where(f => EsJuego(Path.GetRelativePath(raizSd, f.FullName), info.RutaNintendo)).Sum(a => a.Length);

            Directory.CreateDirectory(carpetaDestino);
            long libres = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(carpetaDestino))!).AvailableFreeSpace;
            long necesarios = m.BytesArchivos + m.BytesRaw;
            if (libres < necesarios)
                throw new InvalidOperationException(
                    $"Espacio insuficiente en el PC: se necesitan {Gb(necesarios)} y hay {Gb(libres)} libres.");

            long total = Math.Max(1, necesarios), hecho = 0;
            void Avance(long n, string msg) { hecho += n; progreso.Report(((int)(hecho * 100 / total), msg)); }

            await CopiarArchivosAsync(raizSd, archivos, Path.Combine(carpetaDestino, CarpetaArchivos), Avance, ct);
            CopiarCarpetas(raizSd, Path.Combine(carpetaDestino, CarpetaArchivos),
                rel => IncluirEnCopia(rel, alcance, info.RutaNintendo));

            if (part != null)
            {
                using var origen = AbrirDisco(discoFisico, FileAccess.Read);
                using var salida = new FileStream(Path.Combine(carpetaDestino, NombreRaw), FileMode.Create, FileAccess.Write, FileShare.None, Bloque);
                m.HashRaw = await CopiarRawAsync(origen, part.Inicio * BytesSector, salida, 0, m.BytesRaw,
                    n => Avance(n, "Copiando partición emuMMC al PC…"), ct);
            }

            await File.WriteAllTextAsync(Path.Combine(carpetaDestino, NombreManifiesto), JsonSerializer.Serialize(m, Json), ct);
            progreso.Report((100, "Copia en el PC completada."));
            return m;
        }

        // ── Fase 2: PC → SD destino (ya particionada/formateada) ─────────────

        public static Task RestaurarEnDestinoAsync(
            string carpeta, string raizDestino, int discoDestino,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
            => RestaurarAsync(carpeta, raizDestino, discoDestino, true, progreso, ct);

        /// <summary>
        /// Restaura un respaldo sobre una SD ya preparada. No formatea: los archivos
        /// se sobrescriben y la emuMMC RAW se escribe al inicio de la partición E0
        /// existente (debe ser igual o mayor), verificándola después por SHA-256.
        /// </summary>
        public static async Task RestaurarAsync(
            string carpeta, string raizDestino, int discoDestino, bool incluirJuegos,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
        {
            var m = LeerManifiesto(carpeta);
            string origenArchivos = Path.Combine(carpeta, CarpetaArchivos);
            var archivos = Directory.Exists(origenArchivos)
                ? ListarArchivos(origenArchivos)
                    .Where(f => incluirJuegos || !EsJuego(Path.GetRelativePath(origenArchivos, f.FullName), m.RutaNintendo))
                    .ToList()
                : new List<FileInfo>();
            long bytesArchivos = archivos.Sum(a => a.Length);

            // Espacio neto: lo que ya existe en el destino se sobrescribe.
            long necesarios = 0;
            foreach (var f in archivos)
            {
                var dest = new FileInfo(Path.Combine(raizDestino, Path.GetRelativePath(origenArchivos, f.FullName)));
                necesarios += Math.Max(0, f.Length - (dest.Exists ? dest.Length : 0));
            }
            long libres = new DriveInfo(raizDestino).AvailableFreeSpace;
            if (libres < necesarios)
                throw new InvalidOperationException(
                    $"La SD destino no tiene espacio: se necesitan {Gb(necesarios)} y hay {Gb(libres)} libres.");

            ParticionMbr? part = null;
            if (m.Tipo == TipoEmuMmc.Particion)
            {
                var parts = LeerMbr(discoDestino);
                var encaje = EvaluarEncaje(m, parts);
                if (encaje == EncajeParticion.Ausente)
                    throw new InvalidOperationException("La SD destino no tiene partición emuMMC (tipo E0).");
                if (encaje == EncajeParticion.Insuficiente)
                    throw new InvalidOperationException("La partición emuMMC del destino es más pequeña que la del respaldo.");
                part = parts.First(p => p.Tipo == TipoEmuMmcMbr);
            }

            long total = Math.Max(1, bytesArchivos + m.BytesRaw * 2), hecho = 0;
            void Avance(long n, string msg) { hecho += n; progreso.Report(((int)(hecho * 100 / total), msg)); }

            await CopiarArchivosAsync(origenArchivos, archivos, raizDestino, Avance, ct);
            if (Directory.Exists(origenArchivos))
                CopiarCarpetas(origenArchivos, raizDestino, rel => incluirJuegos || !EsJuego(rel, m.RutaNintendo));

            if (part != null)
            {
                string raw = Path.Combine(carpeta, NombreRaw);
                using (var entrada = new FileStream(raw, FileMode.Open, FileAccess.Read, FileShare.Read, Bloque))
                using (var disco = AbrirDisco(discoDestino, FileAccess.ReadWrite))
                {
                    await CopiarRawAsync(entrada, 0, disco, part.Inicio * BytesSector, m.BytesRaw,
                        n => Avance(n, "Escribiendo partición emuMMC…"), ct);
                }

                using (var disco = AbrirDisco(discoDestino, FileAccess.Read))
                {
                    string hash = await CopiarRawAsync(disco, part.Inicio * BytesSector, Stream.Null, 0, m.BytesRaw,
                        n => Avance(n, "Verificando partición emuMMC…"), ct);
                    if (!string.Equals(hash, m.HashRaw, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("La verificación de la partición emuMMC ha fallado (hash distinto).");
                }

                long nuevoSector = part.Inicio + (m.SectorOrigen!.Value - m.InicioParticionOrigen!.Value);
                ActualizarSector(raizDestino, m.SectorOrigen.Value, nuevoSector);
            }

            var info = EmuMmcInfoLogic.Leer(raizDestino);
            if (!info.ExisteIni || info.Tipo != m.Tipo)
                throw new InvalidOperationException("La emuMMC no se detecta correctamente en la SD destino.");
            progreso.Report((100, "Restauración completada y verificada."));
        }

        // ── Auxiliares ────────────────────────────────────────────────────────

        private static void ActualizarSector(string raiz, long anterior, long nuevo)
        {
            string ini = Path.Combine(raiz, "emuMMC", "emummc.ini");
            File.WriteAllLines(ini, ReemplazarSectorIni(File.ReadAllLines(ini), nuevo));

            // Hekate guarda también el sector (uint32 LE) en emuMMC/RAWx/raw_based.
            foreach (var f in Directory.EnumerateFiles(Path.Combine(raiz, "emuMMC"), "raw_based", SearchOption.AllDirectories))
            {
                var b = File.ReadAllBytes(f);
                if (b.Length >= 4 && BitConverter.ToUInt32(b, 0) == (uint)anterior)
                {
                    BitConverter.GetBytes((uint)nuevo).CopyTo(b, 0);
                    File.WriteAllBytes(f, b);
                }
            }
        }

        private static List<FileInfo> ListarArchivos(string raiz)
        {
            var dir = new DirectoryInfo(raiz);
            var r = new List<FileInfo>();
            foreach (var sub in dir.EnumerateDirectories())
                if (!EsRutaExcluida(sub.Name))
                    r.AddRange(sub.EnumerateFiles("*", SearchOption.AllDirectories));
            r.AddRange(dir.EnumerateFiles());
            return r;
        }

        private static async Task CopiarArchivosAsync(string raizOrigen, List<FileInfo> archivos, string raizDestino,
            Action<long, string> avance, CancellationToken ct)
        {
            var buffer = new byte[Bloque];
            foreach (var f in archivos)
            {
                ct.ThrowIfCancellationRequested();
                string rel = Path.GetRelativePath(raizOrigen, f.FullName);
                string dest = Path.Combine(raizDestino, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                using (var e = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1))
                using (var s = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1))
                {
                    int n;
                    while ((n = await e.ReadAsync(buffer, ct)) > 0)
                    {
                        await s.WriteAsync(buffer.AsMemory(0, n), ct);
                        avance(n, $"Copiando {rel}");
                    }
                }
                File.SetLastWriteTime(dest, f.LastWriteTime);
                File.SetAttributes(dest, f.Attributes & ~FileAttributes.ReadOnly);
            }
        }

        /// <summary>
        /// Recrea todas las carpetas (también las vacías) con sus atributos (bit
        /// Archivo, oculto, sistema). La Switch necesita Contents/placehld, save…
        /// y el bit Archivo en las carpetas de los NCA divididos.
        /// </summary>
        private static void CopiarCarpetas(string raizOrigen, string raizDestino, Func<string, bool> incluir)
        {
            foreach (var sub in new DirectoryInfo(raizOrigen).EnumerateDirectories())
            {
                if (EsRutaExcluida(sub.Name)) continue;
                foreach (var d in sub.EnumerateDirectories("*", SearchOption.AllDirectories).Prepend(sub))
                {
                    string rel = Path.GetRelativePath(raizOrigen, d.FullName);
                    if (!incluir(rel)) continue;
                    var dest = Directory.CreateDirectory(Path.Combine(raizDestino, rel));
                    var attrs = d.Attributes & (FileAttributes.Archive | FileAttributes.Hidden | FileAttributes.System);
                    if ((dest.Attributes & attrs) != attrs) dest.Attributes |= attrs;
                }
            }
        }

        private static async Task<string> CopiarRawAsync(Stream entrada, long offsetEntrada, Stream salida, long offsetSalida,
            long longitud, Action<long> avance, CancellationToken ct)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[Bloque];
            entrada.Position = offsetEntrada;
            if (salida.CanSeek) salida.Position = offsetSalida;
            long restante = longitud;
            while (restante > 0)
            {
                ct.ThrowIfCancellationRequested();
                int pedir = (int)Math.Min(buffer.Length, restante);
                int leidos = 0;
                while (leidos < pedir)
                {
                    int n = await entrada.ReadAsync(buffer.AsMemory(leidos, pedir - leidos), ct);
                    if (n == 0) throw new EndOfStreamException("Lectura incompleta de la emuMMC.");
                    leidos += n;
                }
                hash.AppendData(buffer, 0, leidos);
                await salida.WriteAsync(buffer.AsMemory(0, leidos), ct);
                restante -= leidos;
                avance(leidos);
            }
            await salida.FlushAsync(ct);
            return Convert.ToHexString(hash.GetHashAndReset());
        }

        private static FileStream AbrirDisco(int disco, FileAccess acceso)
            => new($@"\\.\PhysicalDrive{disco}", FileMode.Open, acceso, FileShare.ReadWrite, 0);

        private static List<ParticionMbr> LeerMbr(int disco)
        {
            using var fs = AbrirDisco(disco, FileAccess.Read);
            var s = new byte[BytesSector];
            fs.ReadExactly(s);
            return ParsearMbr(s);
        }

        private static string Gb(long bytes) => $"{bytes / (1024d * 1024 * 1024):0.0} GB";
    }
}
