using System.Text.Json.Nodes;
using NX_Suite.Catalogo;
using Xunit;

namespace NX_Suite.Tests
{
    public class MapaPaqueteTests
    {
        private static Dictionary<string, IReadOnlyList<string>> Contenido(string asset, params string[] rutas) => new() { [asset] = rutas };

        private static SortedSet<string> Simular(JsonArray inst, Dictionary<string, IReadOnlyList<string>> contenido) =>
            SimuladorSD.Simular(inst.OfType<JsonObject>(), contenido, new List<string>());

        [Fact]
        public void ZipConCarpetaPadre_CopiaEnteroYSimulaIgual()
        {
            var archivos = new List<ArchivoMapa>
            {
                new() { Origen = "a/b.nca", Destino = "/firmware/a/b.nca" },
                new() { Origen = "c.nca", Destino = "/firmware/c.nca" },
            };
            var (inst, _) = MapaPaquete.GenerarPipelines("https://x/fw.zip", "fw.zip", "fw-1", archivos, true);
            var sd = Simular(inst, Contenido("fw.zip", "a/b.nca", "c.nca"));
            Assert.Equal(new[] { "/firmware/a/b.nca", "/firmware/c.nca" }, sd);
        }

        [Fact]
        public void ZipConRenombradoYExcluido()
        {
            var archivos = new List<ArchivoMapa>
            {
                new() { Origen = "switch/DBI/DBI.nro", Destino = "/switch/DBI/DBI.nro" },
                new() { Origen = "switch/DBI/dbi.config", Destino = "/switch/DBI/dbi.config" },
                new() { Origen = "README.md", Destino = "/README.md", Incluir = false },
                new() { Origen = "lang/es.bin", Destino = "/switch/DBI/translation.bin" },
            };
            var (inst, desins) = MapaPaquete.GenerarPipelines("https://x/dbi.zip", "dbi.zip", "dbi-1", archivos, false);
            var sd = Simular(inst, Contenido("dbi.zip", "switch/DBI/DBI.nro", "switch/DBI/dbi.config", "README.md", "lang/es.bin"));
            Assert.Equal(new[] { "/switch/DBI/dbi.config", "/switch/DBI/DBI.nro", "/switch/DBI/translation.bin" }, sd);
            Assert.DoesNotContain(inst.OfType<JsonObject>(), p => p["TipoAccion"]!.GetValue<string>() == "LIMPIAR_CACHE");
            Assert.Equal("BORRARARCHIVOS", desins[0]!["TipoAccion"]!.GetValue<string>());
        }

        [Fact]
        public void ArchivoSuelto_VaASuCarpetaPorExtension()
        {
            string destino = MapaPaquete.DestinoPorDefecto("tool.nro", "tool.nro", "tool");
            Assert.Equal("/switch/tool/tool.nro", destino);
            var archivos = new List<ArchivoMapa> { new() { Origen = "tool.nro", Destino = destino, Sha = "ab", UsarSha = true } };
            var (inst, _) = MapaPaquete.GenerarPipelines("https://x/tool.nro", "tool.nro", "t", archivos, true);
            Assert.Equal(new[] { destino }, Simular(inst, Contenido("tool.nro", "tool.nro")));
            var firma = MapaPaquete.GenerarFirma("1.0", archivos);
            Assert.Equal("ab", firma["Archivos"]![0]!["SHA256"]!.GetValue<string>());
        }
    }
}
