using System.Windows;

namespace NX_Suite.UI
{
    /// <summary>
    /// Helpers centralizados para diálogos modales del usuario. Toda la UI
    /// debe usar estos métodos en lugar de invocar <see cref="MessageBox.Show(string)"/>
    /// directamente. Garantiza títulos e iconos consistentes y permite cambiar
    /// el look (toast, snackbar, ventana custom…) en un único punto el día que
    /// queramos sustituir el clásico <see cref="MessageBox"/>.
    /// </summary>
    public static class Dialogos
    {
        /// <summary>Muestra un diálogo de error con el icono rojo estándar.</summary>
        public static void Error(string mensaje, string titulo = "Error")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);

        /// <summary>Muestra un aviso no fatal (icono amarillo).</summary>
        public static void Advertencia(string mensaje, string titulo = "Advertencia")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);

        /// <summary>Muestra un mensaje informativo (icono azul).</summary>
        public static void Info(string mensaje, string titulo = "Información")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);

        /// <summary>
        /// Pregunta Sí/No al usuario y devuelve <c>true</c> si responde Sí.
        /// Por defecto usa el icono de pregunta amarillo.
        /// </summary>
        public static bool Confirmar(string mensaje, string titulo = "Confirmar",
            MessageBoxImage icono = MessageBoxImage.Question)
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, icono) == MessageBoxResult.Yes;
    }
}
