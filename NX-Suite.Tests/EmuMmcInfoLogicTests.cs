using NX_Swite.Core;

namespace NX_Swite.Tests;

public class EmuMmcInfoLogicTests
{
    [Fact]
    public void Particion_SeDetectaPorSector()
    {
        var v = EmuMmcInfoLogic.ParsearIni(new[]
        {
            "[emummc]", "enabled=1", "sector=0x2", "nintendo_path=emuMMC/RAW1/Nintendo"
        });
        var info = EmuMmcInfoLogic.Interpretar(v);

        Assert.True(info.Habilitada);
        Assert.Equal(TipoEmuMmc.Particion, info.Tipo);
        Assert.Equal(2, info.Sector);
        Assert.Equal("emuMMC/RAW1/Nintendo", info.RutaNintendo);
    }

    [Fact]
    public void Archivos_SeDetectaPorPath()
    {
        var info = EmuMmcInfoLogic.Interpretar(EmuMmcInfoLogic.ParsearIni(new[]
        {
            "enabled=0", "sector=0x0", "path=emuMMC/SD00"
        }));

        Assert.False(info.Habilitada);
        Assert.Equal(TipoEmuMmc.Archivos, info.Tipo);
        Assert.Null(info.Sector);
        Assert.Equal("emuMMC/SD00", info.RutaArchivos);
    }
}
