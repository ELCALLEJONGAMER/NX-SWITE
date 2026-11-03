using NX_Swite.Core;

namespace NX_Swite.Tests;

public class EmuMmcRespaldoLogicTests
{
    [Theory]
    [InlineData(@"emuMMC\RAW1\raw_based", AlcanceCopia.EmuMmc, true)]
    [InlineData(@"emuMMC\RAW1\Nintendo\Contents\registered\a.nca", AlcanceCopia.EmuMmc, false)]
    [InlineData(@"emuMMC\RAW1\Nintendo\Contents\registered\a.nca", AlcanceCopia.EmuMmcConJuegos, true)]
    [InlineData(@"emuMMC\RAW1\Nintendo\save\x", AlcanceCopia.EmuMmc, true)]
    [InlineData(@"atmosphere\config.ini", AlcanceCopia.EmuMmc, false)]
    [InlineData(@"atmosphere\config.ini", AlcanceCopia.SdCompleta, true)]
    public void IncluirEnCopia_RespetaAlcance(string ruta, AlcanceCopia alcance, bool esperado)
        => Assert.Equal(esperado, EmuMmcMigracionLogic.IncluirEnCopia(ruta, alcance, "emuMMC/RAW1/Nintendo"));

    [Theory]
    [InlineData(1000, EncajeParticion.Exacta)]
    [InlineData(2000, EncajeParticion.Mayor)]
    [InlineData(500, EncajeParticion.Insuficiente)]
    public void EvaluarEncaje_ComparaTamano(long sectores, EncajeParticion esperado)
    {
        var m = new ManifiestoMigracion { Tipo = TipoEmuMmc.Particion, BytesRaw = 1000 * 512 };
        var p = new List<ParticionMbr> { new() { Tipo = 0xE0, Inicio = 10, Sectores = sectores } };
        Assert.Equal(esperado, EmuMmcMigracionLogic.EvaluarEncaje(m, p));
    }

    [Fact]
    public void EvaluarEncaje_SinParticion_Ausente()
        => Assert.Equal(EncajeParticion.Ausente, EmuMmcMigracionLogic.EvaluarEncaje(
            new ManifiestoMigracion { Tipo = TipoEmuMmc.Particion, BytesRaw = 512 }, new List<ParticionMbr>()));

    [Theory]
    [InlineData("17.0.1", "18.1.0", -1)]
    [InlineData("18.1.0", "18.1.0", 0)]
    [InlineData("19.0.0", "18.1.0", 1)]
    public void CompararFirmware_OrdenaVersiones(string a, string b, int esperado)
        => Assert.Equal(esperado, Math.Sign(EmuMmcMigracionLogic.CompararFirmware(a, b)!.Value));

    [Fact]
    public void CompararFirmware_Desconocido_Null()
        => Assert.Null(EmuMmcMigracionLogic.CompararFirmware(null, "18.1.0"));
}
