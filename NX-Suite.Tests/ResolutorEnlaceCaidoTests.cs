using NX_Swite.Core;

namespace NX_Swite.Tests;

public class ResolutorEnlaceCaidoTests
{
    [Theory]
    [InlineData("hekate_ctcaer_6.5.4_Nyx_1.9.4.zip", "hekate_ctcaer_6.6.0_Nyx_1.9.5.zip")]
    [InlineData("atmosphere-1.12.1-master-14e482b4c%2Bhbl-2.4.5%2Bhbmenu-3.6.1.zip",
                "atmosphere-1.13.0-master-8795cc057+hbl-2.4.5+hbmenu-3.6.2.zip")]
    [InlineData("Firmware.22.5.0.zip", "Firmware.23.0.0.zip")]
    public void Esqueleto_MismoTipoDistintaVersion_Coincide(string a, string b)
        => Assert.Equal(ResolutorEnlaceCaido.Esqueleto(a), ResolutorEnlaceCaido.Esqueleto(b));

    [Fact]
    public void Esqueleto_AssetDistinto_NoCoincide()
        => Assert.NotEqual(
            ResolutorEnlaceCaido.Esqueleto("atmosphere-1.12.1-master-14e482b4c+hbl-2.4.5+hbmenu-3.6.1.zip"),
            ResolutorEnlaceCaido.Esqueleto("fusee.bin.zip"));

    [Theory]
    [InlineData("v6.5.4", "6.5.4")]
    [InlineData("1.12.1%2B", "1.12.1")]
    [InlineData("1.12.0+v2", "1.12.0")]
    [InlineData("23", "23.0")]
    public void ExtraerVersion(string texto, string esperado)
        => Assert.Equal(Version.Parse(esperado), ResolutorEnlaceCaido.ExtraerVersion(texto));

    [Theory]
    [InlineData("https://github.com/borntohonk/Atmosphere/releases/download/1.12.1%2B/a.zip", true)]
    [InlineData("https://github.com/CTCaer/hekate/releases/download/v6.5.4/h.zip", false)]
    public void AplicaA_SoloAtmosphere(string url, bool esperado)
        => Assert.Equal(esperado, ResolutorEnlaceCaido.AplicaA(url));

    [Fact]
    public void ParsearUrlRelease_Decodifica()
    {
        Assert.True(ResolutorEnlaceCaido.ParsearUrlRelease(
            "https://github.com/borntohonk/Atmosphere/releases/download/1.12.1%2B/atmosphere-1.12.1.zip",
            out var owner, out var repo, out var tag, out var archivo));
        Assert.Equal("borntohonk", owner);
        Assert.Equal("Atmosphere", repo);
        Assert.Equal("1.12.1+", tag);
        Assert.Equal("atmosphere-1.12.1.zip", archivo);
    }

    [Fact]
    public void EsEnlaceCaido_Detecta404Envuelto()
    {
        var inner = new global::System.Net.Http.HttpRequestException("x", null, System.Net.HttpStatusCode.NotFound);
        Assert.True(ResolutorEnlaceCaido.EsEnlaceCaido(new InvalidOperationException("w", inner)));
        Assert.False(ResolutorEnlaceCaido.EsEnlaceCaido(
            new global::System.Net.Http.HttpRequestException("x", null, System.Net.HttpStatusCode.ServiceUnavailable)));
    }
}

