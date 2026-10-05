namespace NX_Swite.Models
{
    /// <summary>
    /// Sección independiente del Gist JSON, mapeada a la raíz <c>"titleDB"</c>.
    /// Contiene únicamente la URL del TitleDB remoto usado para resolver
    /// nombres de juegos a partir de un TitleId. La app NUNCA debe hardcodear
    /// un proveedor concreto (ej. blawar): solo depende de esta URL.
    /// </summary>
    public class TitleDbConfig
    {
        /// <summary>URL del JSON remoto con el TitleDB (TitleId ? metadata con "name").</summary>
        public string UrlTitleDB { get; set; } = string.Empty;
    }
}
