using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Pregunta común a todos los procesos (instalar/actualizar en
    /// Micro SD o PC) sobre si descargar el firmware y qué versión. El firmware
    /// pesa mucho, así que nunca se incluye sin que el usuario lo elija.
    /// </summary>
    public partial class MainWindow
    {
        private static bool EsFirmware(ModuloConfig m) =>
            string.Equals(m.Id, "firmware", StringComparison.OrdinalIgnoreCase) ||
            m.Etiquetas?.Exists(e => string.Equals(e, "firmware", StringComparison.OrdinalIgnoreCase)) == true;

        /// <summary>
        /// Si la lista contiene firmware, pregunta al usuario si lo quiere y qué
        /// versión. Devuelve la lista filtrada (sin firmware si lo omite) o
        /// <c>null</c> si cancela el proceso.
        /// </summary>
        private List<ModuloConfig>? ConfirmarFirmwareEnLista(List<ModuloConfig> modulos)
        {
            var firmware = modulos.FirstOrDefault(EsFirmware);
            if (firmware == null) return modulos;

            var versiones = firmware.Versiones.Where(v => !v.SoloDeteccion).ToList();
            if (versiones.Count == 0) return modulos.Where(m => !EsFirmware(m)).ToList();

            const string sinFirmware = "No descargar firmware";
            var opciones = new List<object> { sinFirmware };
            opciones.AddRange(versiones);

            var lista = new ListBox
            {
                ItemsSource = opciones.Select(o => o is ModuloVersion v ? $"Firmware {v.Version}" : (string)o).ToList(),
                Background  = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1C)),
                Foreground  = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x35)),
                MaxHeight   = 220,
                Margin      = new Thickness(0, 10, 0, 10),
                SelectedIndex = 0,
            };

            var btnOk     = new Button { Content = "Continuar", Width = 110, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var btnCancel = new Button { Content = "Cancelar",  Width = 110, Height = 30, IsCancel = true };
            var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            botones.Children.Add(btnOk);
            botones.Children.Add(btnCancel);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "El firmware ocupa varios cientos de MB. ¿Quieres descargarlo?\nElige la versión o continúa sin firmware.",
                Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(lista);
            panel.Children.Add(botones);

            var ventana = new Window
            {
                Title = "Firmware",
                Owner = this,
                Content = panel,
                Width = 440,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x12)),
            };
            btnOk.Click += (_, _) => ventana.DialogResult = true;
            lista.MouseDoubleClick += (_, _) => ventana.DialogResult = true;

            if (ventana.ShowDialog() != true) return null;

            int idx = lista.SelectedIndex;
            if (idx <= 0)
            {
                firmware.VersionElegidaUsuario = null;
                return modulos.Where(m => !EsFirmware(m)).ToList();
            }

            firmware.VersionElegidaUsuario = versiones[idx - 1];
            return modulos;
        }
    }
}
