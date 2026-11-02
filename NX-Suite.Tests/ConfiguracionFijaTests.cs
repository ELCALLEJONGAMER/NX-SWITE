using System.Text.Json;
using NX_Swite.Models;

namespace NX_Swite.Tests;

public class ConfiguracionFijaTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void SinSeccionesEnCatalogo_SeCarganDesdeLaApp()
    {
        var datos = JsonSerializer.Deserialize<GistData>("{\"Modulos\":[]}", Opciones)!;

        Assert.NotEmpty(datos.FiltrosCentroMando);
        Assert.NotEmpty(datos.DiagramaNodos);
    }

    [Fact]
    public void CatalogoAntiguo_NoSobrescribeLaConfiguracionFija()
    {
        var datos = JsonSerializer.Deserialize<GistData>(
            "{\"FiltrosCentroMando\":[{\"Titulo\":\"roto\"}]}", Opciones)!;

        Assert.DoesNotContain(datos.FiltrosCentroMando, f => f.Titulo == "roto");
    }
}
