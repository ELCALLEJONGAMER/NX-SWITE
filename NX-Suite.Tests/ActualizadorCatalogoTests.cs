using System.Net;
using System.Net.Http;
using NX_Suite.Catalogo;
using Xunit;

namespace NX_Suite.Tests
{
    public class ActualizadorCatalogoTests
    {
        [Theory]
        [InlineData(HttpStatusCode.MovedPermanently, "https://api.github.com/repositories/123/releases?per_page=50")]
        [InlineData(HttpStatusCode.Found, "/repositories/123/releases?per_page=50")]
        [InlineData(HttpStatusCode.SeeOther, "/repositories/123/releases?per_page=50")]
        [InlineData(HttpStatusCode.TemporaryRedirect, "/repositories/123/releases?per_page=50")]
        [InlineData(HttpStatusCode.PermanentRedirect, "/repositories/123/releases?per_page=50")]
        public async Task GitHubReleases_ConservaTokenAlRedirigirYCachea(HttpStatusCode estado, string destino)
        {
            int llamadas = 0;
            using var github = new GitHubReleases("token-de-prueba", new RespuestaGitHub(req =>
            {
                Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                Assert.Equal("token-de-prueba", req.Headers.Authorization?.Parameter);
                Assert.Contains("NX-Suite.Catalogo", req.Headers.UserAgent.ToString());
                if (++llamadas == 1)
                {
                    Assert.Equal("/repos/antiguo/repo/releases", req.RequestUri!.AbsolutePath);
                    var redireccion = new HttpResponseMessage(estado);
                    redireccion.Headers.Location = new Uri(destino, UriKind.RelativeOrAbsolute);
                    return redireccion;
                }
                Assert.Equal("https://api.github.com/repositories/123/releases?per_page=50", req.RequestUri!.AbsoluteUri);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            }));

            var releases = await github.ObtenerAsync("antiguo/repo");

            Assert.NotNull(releases);
            Assert.Empty(releases);
            Assert.Same(releases, await github.ObtenerAsync("ANTIGUO/REPO"));
            Assert.Equal(2, llamadas);
            Assert.Null(github.Http.DefaultRequestHeaders.Authorization);
        }

        [Theory]
        [InlineData("https://otro.example/releases")]
        [InlineData("https://api.github.com.otro.example/releases")]
        [InlineData("http://api.github.com/releases")]
        [InlineData("https://api.github.com:444/releases")]
        [InlineData("https://usuario@api.github.com/releases")]
        public async Task GitHubReleases_RechazaRedireccionFueraDeLaApi(string destino)
        {
            int llamadas = 0;
            using var github = new GitHubReleases("token-de-prueba", new RespuestaGitHub(req =>
            {
                llamadas++;
                var resp = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                resp.Headers.Location = new Uri(destino);
                return resp;
            }));

            await Assert.ThrowsAsync<HttpRequestException>(() => github.ObtenerAsync("antiguo/repo"));
            Assert.Equal(1, llamadas);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GitHubReleases_RechazaRedireccionSinDestinoOBucle(bool sinDestino)
        {
            int llamadas = 0;
            using var github = new GitHubReleases("", new RespuestaGitHub(req =>
            {
                llamadas++;
                Assert.Null(req.Headers.Authorization);
                var resp = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                if (!sinDestino) resp.Headers.Location = req.RequestUri;
                return resp;
            }));

            await Assert.ThrowsAsync<HttpRequestException>(() => github.ObtenerAsync("antiguo/repo"));
            Assert.Equal(sinDestino ? 1 : 6, llamadas);
        }

        private sealed class RespuestaGitHub(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(responder(request));
        }

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
