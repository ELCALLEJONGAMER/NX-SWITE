using BenchmarkDotNet.Attributes;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

[MemoryDiagnoser]
public class CatalogoTemplateBenchmarks
{
    [Benchmark]
    public int CrearContenidoPlantillaModuloGamer18Veces()
    {
        int creados = 0;
        Exception? error = null;

        var hilo = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                }

                var recursos = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/NX-Suite;component/UI/Estilos/EstilosTarjetas.xaml", UriKind.Absolute)
                };

                if (recursos["PlantillaModuloGamer"] is not DataTemplate plantilla)
                    return;

                for (int i = 0; i < 18; i++)
                {
                    if (plantilla.LoadContent() is FrameworkElement)
                        creados++;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();

        if (error != null)
            throw new InvalidOperationException("No se pudo crear la plantilla WPF del catálogo.", error);

        return creados;
    }
}
