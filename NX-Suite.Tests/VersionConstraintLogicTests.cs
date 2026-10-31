using NX_Swite.Core;

namespace NX_Swite.Tests;

public class VersionConstraintLogicTests
{
    [Theory]
    [InlineData("1.11.1", "1.11.1")]
    [InlineData("v1.11.1", "1.11.1")]
    [InlineData("V2.0", "2.0")]
    [InlineData("1.11.1+", "1.11.1")]
    [InlineData("1.11.1-dev", "1.11.1")]
    [InlineData("1.3.7hotfix1", "1.3.7.1")]
    [InlineData("1.3.7hotfix2", "1.3.7.2")]
    [InlineData("19", "19.0")]
    public void NormalizarVersion_ToleraSufijosComunes(string entrada, string esperado)
    {
        Assert.Equal(esperado, VersionConstraintLogic.NormalizarVersion(entrada));
    }

    [Fact]
    public void NormalizarVersion_HotfixOrdenaDespuesDeBase()
    {
        var baseVer = Version.Parse(VersionConstraintLogic.NormalizarVersion("1.3.7"));
        var hf1 = Version.Parse(VersionConstraintLogic.NormalizarVersion("1.3.7hotfix1"));
        var hf2 = Version.Parse(VersionConstraintLogic.NormalizarVersion("1.3.7hotfix2"));

        Assert.True(baseVer < hf1);
        Assert.True(hf1 < hf2);
    }

    [Theory]
    [InlineData("<=1.11.1", "<=", "1.11.1")]
    [InlineData(">=19.0.0", ">=", "19.0.0")]
    [InlineData("<2.0", "<", "2.0")]
    [InlineData(">1.0", ">", "1.0")]
    [InlineData("  1.5.0 ", ">=", "1.5.0")]
    public void ParseConstraintVersion_ReconoceOperadores(string expr, string op, string version)
    {
        var resultado = VersionConstraintLogic.ParseConstraintVersion(expr);

        Assert.NotNull(resultado);
        Assert.Equal(op, resultado.Value.Operador);
        Assert.Equal(Version.Parse(version), resultado.Value.Version);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(">=")]
    public void ParseConstraintVersion_InvalidaDevuelveNull(string expr)
    {
        Assert.Null(VersionConstraintLogic.ParseConstraintVersion(expr));
    }

    [Theory]
    [InlineData("1.0", ">=", "1.0", false)]
    [InlineData("0.9", ">=", "1.0", true)]
    [InlineData("1.1", "<=", "1.0", true)]
    [InlineData("1.0", "<=", "1.0", false)]
    [InlineData("1.0", "<", "1.0", true)]
    [InlineData("1.0", ">", "1.0", true)]
    [InlineData("1.1", ">", "1.0", false)]
    public void ViolaConstraint_EvaluaCorrectamente(string instalada, string op, string requerida, bool viola)
    {
        Assert.Equal(viola, VersionConstraintLogic.ViolaConstraint(
            Version.Parse(instalada), op, Version.Parse(requerida)));
    }

    [Theory]
    [InlineData("<=1.11.1", "1.11.1")]
    [InlineData(">= 19.0.0", "19.0.0")]
    [InlineData("<2", "2")]
    [InlineData("1.0", "1.0")]
    public void LimpiarVersionMostrar_QuitaOperador(string expr, string esperado)
    {
        Assert.Equal(esperado, VersionConstraintLogic.LimpiarVersionMostrar(expr));
    }

    [Theory]
    [InlineData("<=", "Firmware", "Firmware máximo compatible")]
    [InlineData(">", "Atmosphere", "Atmosphere mínimo requerido")]
    [InlineData("", "Hekate", "Hekate requerido")]
    public void EtiquetaLimitePorOperador_GeneraTexto(string op, string nombre, string esperado)
    {
        Assert.Equal(esperado, VersionConstraintLogic.EtiquetaLimitePorOperador(op, nombre));
    }
}
