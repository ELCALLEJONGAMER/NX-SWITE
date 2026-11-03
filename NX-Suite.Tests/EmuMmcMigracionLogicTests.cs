using NX_Swite.Core;

namespace NX_Swite.Tests;

public class EmuMmcMigracionLogicTests
{
    [Fact]
    public void ParsearMbr_DevuelveParticiones()
    {
        var s = new byte[512];
        s[510] = 0x55; s[511] = 0xAA;
        Escribir(s, 0, 0x0C, 2048, 1000);
        Escribir(s, 1, 0xE0, 3048, 500);

        var p = EmuMmcMigracionLogic.ParsearMbr(s);

        Assert.Equal(2, p.Count);
        Assert.Equal(0xE0, p[1].Tipo);
        Assert.True(p[1].Contiene(3048));
        Assert.False(p[1].Contiene(3548));
    }

    [Fact]
    public void ParsearMbr_SinFirma_Vacio()
        => Assert.Empty(EmuMmcMigracionLogic.ParsearMbr(new byte[512]));

    [Fact]
    public void ReemplazarSectorIni_ConservaResto()
    {
        var r = EmuMmcMigracionLogic.ReemplazarSectorIni(
            new[] { "[emummc]", "enabled=1", "sector=0x2", "nintendo_path=emuMMC/RAW1/Nintendo" }, 0x1000);

        Assert.Equal(new[] { "[emummc]", "enabled=1", "sector=0x1000", "nintendo_path=emuMMC/RAW1/Nintendo" }, r);
    }

    [Theory]
    [InlineData("System Volume Information\\x", true)]
    [InlineData("emuMMC\\SD00\\eMMC\\00", false)]
    public void EsRutaExcluida(string ruta, bool esperado)
        => Assert.Equal(esperado, EmuMmcMigracionLogic.EsRutaExcluida(ruta));

    [Fact]
    public void GbNecesarios_RedondeaHaciaArriba()
        => Assert.Equal(30, new ManifiestoMigracion { BytesRaw = 29L * 1024 * 1024 * 1024 + 1 }.GbEmuMmcNecesarios);

    private static void Escribir(byte[] s, int i, byte tipo, uint inicio, uint sectores)
    {
        int o = 446 + i * 16;
        s[o + 4] = tipo;
        BitConverter.GetBytes(inicio).CopyTo(s, o + 8);
        BitConverter.GetBytes(sectores).CopyTo(s, o + 12);
    }
}
