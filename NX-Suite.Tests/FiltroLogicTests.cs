using NX_Swite.Core;
using NX_Swite.Models;

namespace NX_Swite.Tests;

public class FiltroLogicTests
{
    private static ModuloConfig Modulo(string id, params string[] etiquetas) => new()
    {
        Id = id,
        Nombre = id,
        Descripcion = $"Descripción de {id}",
        Etiquetas = etiquetas.ToList()
    };

    private static readonly List<ModuloConfig> Catalogo = new()
    {
        Modulo("hekate", "bootloader"),
        Modulo("atmosphere", "cfw"),
        Modulo("sigpatches", "cfw", "parches"),
        Modulo("tema", "personalizacion")
    };

    [Fact]
    public void FiltrarPorEtiquetas_IgnoraMayusculas()
    {
        var r = FiltroLogic.FiltrarPorEtiquetas(Catalogo, new[] { "CFW" }).Select(m => m.Id);
        Assert.Equal(new[] { "atmosphere", "sigpatches" }, r);
    }

    [Fact]
    public void FiltrarPorEtiquetas_SinEtiquetasDevuelveTodo()
    {
        Assert.Equal(4, FiltroLogic.FiltrarPorEtiquetas(Catalogo, Array.Empty<string>()).Count());
    }

    [Theory]
    [InlineData("Todos", 4)]
    [InlineData("", 4)]
    [InlineData("parches", 1)]
    [InlineData("inexistente", 0)]
    public void FiltrarPorEtiqueta_RespetaTodos(string etiqueta, int esperados)
    {
        Assert.Equal(esperados, FiltroLogic.FiltrarPorEtiqueta(Catalogo, etiqueta).Count());
    }

    [Theory]
    [InlineData("HEKA", "hekate")]
    [InlineData("descripción de tema", "tema")]
    public void FiltrarPorTexto_BuscaEnNombreYDescripcion(string busqueda, string id)
    {
        Assert.Equal(id, Assert.Single(FiltroLogic.FiltrarPorTexto(Catalogo, busqueda)).Id);
    }

    [Fact]
    public void FiltrarPorEtiquetasMundo_NullDevuelveTodo()
    {
        Assert.Equal(4, FiltroLogic.FiltrarPorEtiquetasMundo(Catalogo, null).Count());
    }

    [Fact]
    public void OrdenarPorPrioridadAccion_ActualizarPrimero()
    {
        var a = Modulo("a"); a.AccionRapida = AccionRapidaModulo.Instalar;
        var b = Modulo("b"); b.AccionRapida = AccionRapidaModulo.Actualizar;
        var c = Modulo("c"); c.AccionRapida = AccionRapidaModulo.Eliminar;

        var r = FiltroLogic.OrdenarPorPrioridadAccion(new[] { a, b, c }).Select(m => m.Id);

        Assert.Equal(new[] { "b", "c", "a" }, r);
    }
}
