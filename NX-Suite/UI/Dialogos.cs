using System.Windows;

namespace NX_Swite.UI
{
    /// <summary>
    /// Helpers centralizados para di�logos modales del usuario. Toda la UI
    /// debe usar estos m�todos en lugar de invocar <see cref="MessageBox.Show(string)"/>
    /// directamente. Garantiza t�tulos e iconos consistentes y permite cambiar
    /// el look (toast, snackbar, ventana custom�) en un �nico punto el d�a que
    /// queramos sustituir el cl�sico <see cref="MessageBox"/>.
    /// </summary>
    public static class Dialogos
    {
        /// <summary>Muestra un di�logo de error con el icono rojo est�ndar.</summary>
        public static void Error(string mensaje, string titulo = "Error")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);

        /// <summary>Muestra un aviso no fatal (icono amarillo).</summary>
        public static void Advertencia(string mensaje, string titulo = "Advertencia")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);

        /// <summary>Muestra un mensaje informativo (icono azul).</summary>
        public static void Info(string mensaje, string titulo = "Informaci�n")
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);

        /// <summary>
        /// Pregunta S�/No al usuario y devuelve <c>true</c> si responde S�.
        /// Por defecto usa el icono de pregunta amarillo.
        /// </summary>
        public static bool Confirmar(string mensaje, string titulo = "Confirmar",
            MessageBoxImage icono = MessageBoxImage.Question)
            => MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, icono) == MessageBoxResult.Yes;
    }
}
