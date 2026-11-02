using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Mundo emuMMC (fase 1): muestra la configuración emuMMC de la SD
    /// seleccionada en modo solo lectura.
    /// </summary>
    public partial class VistaEmuMMC : UserControl
    {
        /// <summary>Devuelve la letra de la SD seleccionada (p.ej. "E:\") o null.</summary>
        public Func<string?>? ObtenerLetraSD { get; set; }

        public VistaEmuMMC()
        {
            InitializeComponent();
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = RefrescarAsync();

        public async Task RefrescarAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            if (string.IsNullOrWhiteSpace(letra))
            {
                TxtEstado.Text = "Selecciona una microSD para ver su emuMMC.";
                ListaDatos.ItemsSource = null;
                return;
            }

            TxtEstado.Text = "Leyendo…";
            InfoEmuMmc info;
            try
            {
                info = await Task.Run(() => EmuMmcInfoLogic.Leer(letra));
            }
            catch (Exception ex)
            {
                TxtEstado.Text = $"No se pudo leer la emuMMC: {ex.Message}";
                ListaDatos.ItemsSource = null;
                return;
            }

            if (!info.ExisteIni)
            {
                TxtEstado.Text = "Esta microSD no tiene emuMMC configurada (falta emuMMC/emummc.ini).";
                ListaDatos.ItemsSource = null;
                return;
            }

            TxtEstado.Text = info.Habilitada ? "emuMMC habilitada" : "emuMMC configurada pero deshabilitada";

            var datos = new List<KeyValuePair<string, string>>
            {
                new("Tipo", info.Tipo switch
                {
                    TipoEmuMmc.Particion => "Partición (RAW)",
                    TipoEmuMmc.Archivos  => "Archivos en la SD",
                    _                    => "Desconocido",
                }),
            };
            if (info.Sector != null)       datos.Add(new("Sector de inicio", $"0x{info.Sector:X}"));
            if (info.RutaArchivos != null) datos.Add(new("Ruta de archivos", info.RutaArchivos));
            datos.Add(new("Carpeta Nintendo", info.RutaNintendo == null
                ? "No indicada"
                : $"{info.RutaNintendo} ({(info.ExisteNintendo ? "existe" : "no encontrada")})"));
            if (info.ExisteNintendo)
                datos.Add(new("Contenidos instalados en SD", info.ContenidosNintendo.ToString()));

            ListaDatos.ItemsSource = datos;
        }
    }
}
