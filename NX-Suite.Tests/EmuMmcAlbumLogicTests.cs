using NX_Swite.Core;
using System;
using Xunit;

namespace NX_Swite.Tests
{
    public class EmuMmcAlbumLogicTests
    {
        [Theory]
        [InlineData("2024053118420700-0123456789ABCDEF.jpg", true)]
        [InlineData("clip.MP4", true)]
        [InlineData("notas.txt", false)]
        public void EsArchivoAlbum_FiltraExtensiones(string nombre, bool esperado)
            => Assert.Equal(esperado, EmuMmcAlbumLogic.EsArchivoAlbum(nombre));

        [Fact]
        public void FechaDesdeNombre_FormatoConsola()
            => Assert.Equal(new DateTime(2024, 5, 31, 18, 42, 7),
                            EmuMmcAlbumLogic.FechaDesdeNombre("2024053118420700-0123456789ABCDEF.jpg"));

        [Fact]
        public void FechaDesdeNombre_NombreInvalido_DevuelveNull()
            => Assert.Null(EmuMmcAlbumLogic.FechaDesdeNombre("captura.jpg"));

        [Fact]
        public void NombreExportacion_AnadeIndiceSiHayColision()
        {
            var a = new ArchivoAlbum { Ruta = @"C:\x\a.MP4", Fecha = new DateTime(2024, 1, 2, 3, 4, 5) };
            Assert.Equal("2024-01-02 03.04.05.mp4", EmuMmcAlbumLogic.NombreExportacion(a));
            Assert.Equal("2024-01-02 03.04.05 (2).mp4", EmuMmcAlbumLogic.NombreExportacion(a, 2));
        }
    }
}
