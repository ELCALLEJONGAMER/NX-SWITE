namespace NX_Suite.Models
{
    /// <summary>
    /// URLs de los archivos .wav descargables desde el Gist.
    /// Cada campo puede estar vacío (sin sonido para ese evento).
    /// </summary>
    public class SonidosConfig
    {
        public string Intro      { get; set; } = string.Empty;
        public string Cerrar     { get; set; } = string.Empty;
        public string Click      { get; set; } = string.Empty;
        public string Hover      { get; set; } = string.Empty;
        public string Instalar   { get; set; } = string.Empty;
        public string Exito      { get; set; } = string.Empty;
        public string Error      { get; set; } = string.Empty;
        public string Navegacion { get; set; } = string.Empty;
    }
}
