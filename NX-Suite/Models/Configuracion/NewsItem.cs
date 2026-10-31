namespace NX_Swite.Models
{
    /// <summary>
    /// Elemento de noticia mostrado en la pantalla inicial de la aplicación.
    /// </summary>
    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = "#0F0F15";
    }
}
