using NX_Suite.Services;
using NX_Suite.UI;
using System.Windows;

namespace NX_Suite
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            Logger.IniciarSesion();

            var splash = new VentanaSplash();
            MainWindow = splash;
            splash.Show();
        }
    }
}