using System;
using System.Windows;

namespace NX_Suite_Updater
{
    public class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new App();
            app.Run(new VentanaActualizacion(args));
        }
    }
}
