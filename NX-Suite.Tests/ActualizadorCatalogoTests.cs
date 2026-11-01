using NX_Suite.Catalogo;
using Xunit;

namespace NX_Suite.Tests
{
    public class ActualizadorCatalogoTests
    {
        [Theory]
        [InlineData("Checkpoint.nro", "5.2.0", "v5.2.0", "Checkpoint.nro", true)]
        [InlineData("tico-0.7.9.zip", "0.7.9", "0.7.9", "tico-0.8.0.zip", true)]
        [InlineData("tico-0.7.9.zip", "0.7.9", "0.7.9", "tico-0.8.0-debug.zip", false)]
        [InlineData("atmosphere-1.12.1-master-14e482b4c+hbl-2.4.5.zip", "1.12.1+", "1.12.1+",
                    "atmosphere-1.13.0-master-0a1b2c3d4+hbl-2.4.5.zip", true)]
        [InlineData("hekate_ctcaer_6.5.4_Nyx_1.8.4.zip", "6.5.4", "v6.5.4",
                    "hekate_ctcaer_6.5.5_Nyx_1.8.4.zip", true)]
        [InlineData("hekate_ctcaer_6.5.4_Nyx_1.9.4.zip", "6.5.4", "v6.5.4",
                    "hekate_ctcaer_6.5.5_Nyx_1.9.5.zip", true)]
        [InlineData("atmosphere-1.12.1-master-14e482b4c+hbl-2.4.5.zip", "1.12.1+", "1.12.1+",
                    "atmosphere-1.13.0-master-0a1b2c3d4+hbl-2.4.6.zip", true)]
        public void PatronAsset_EmparejaSoloElEquivalente(
            string viejo, string version, string tag, string nuevo, bool esperado)
        {
            var patron = ActualizadorCatalogo.PatronAsset(viejo, version, tag);
            Assert.Equal(esperado, patron.IsMatch(nuevo));
        }

        [Fact]
        public void PatronAsset_Hekate_EligeSoloElZipDeNyx()
        {
            var patron = ActualizadorCatalogo.PatronAsset("hekate_ctcaer_6.5.4_Nyx_1.9.4.zip", "6.5.4", "v6.5.4");
            var assets = new[]
            {
                "hekate_ctcaer_6.5.5.bin", "hekate_ctcaer_6.5.5_Nyx_1.9.5.zip", "hekate_ctcaer_6.5.5__ram8GB.bin",
                "joiner_scripts_for_windows_linux_macos.zip", "nyx_usb_max_rate__run_only_once_per_windows_pc.reg",
            };
            Assert.Equal(new[] { "hekate_ctcaer_6.5.5_Nyx_1.9.5.zip" }, assets.Where(a => patron.IsMatch(a)));
        }

        [Theory]
        [InlineData("5.2.0", "v5.2.0", "v5.3.0", "5.3.0")]
        [InlineData("905", "905", "921", "921")]
        [InlineData("nxt-2.9", "nxt-2.9", "nxt-3.0.2", "nxt-3.0.2")]
        public void DerivarVersion_RespetaElFormatoActual(string version, string tag, string tagNuevo, string esperado)
            => Assert.Equal(esperado, ActualizadorCatalogo.DerivarVersion(version, tag, tagNuevo));

        [Fact]
        public void CompararEstructura_DetectaCarpetasDesaparecidas()
        {
            var viejo = new Dictionary<string, string> { ["/atmosphere/package3"] = "a", ["/switch/app/app.nro"] = "b" };
            var igual = new Dictionary<string, string> { ["/atmosphere/package3"] = "c", ["/switch/app/app.nro"] = "d", ["/config/x.ini"] = "e" };
            var roto  = new Dictionary<string, string> { ["/atmosphere/package3"] = "c", ["/switch/app.nro"] = "d" };

            Assert.Null(ActualizadorCatalogo.CompararEstructura(viejo, igual, "1.0", "1.1"));
            Assert.NotNull(ActualizadorCatalogo.CompararEstructura(viejo, roto, "1.0", "1.1"));
        }

        [Fact]
        public void CompararEstructura_ToleraCarpetaEnvoltorioConVersion()
        {
            var viejo = new Dictionary<string, string> { ["/hekate-6.5.4/bootloader/x"] = "a" };
            var nuevo = new Dictionary<string, string> { ["/hekate-6.5.5/bootloader/x"] = "b" };
            Assert.Null(ActualizadorCatalogo.CompararEstructura(viejo, nuevo, "6.5.4", "6.5.5"));
        }
    }
}
