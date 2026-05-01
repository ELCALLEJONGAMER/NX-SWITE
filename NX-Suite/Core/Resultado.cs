namespace NX_Suite.Core
{
    /// <summary>
    /// Resultado de una operación de lógica de negocio. Sustituye a las
    /// tuplas <c>(bool Exito, string MensajeError)</c> que estaban dispersas
    /// por la app. Convención del proyecto:
    /// <list type="bullet">
    ///   <item>Las clases de Core/Network/Hardware devuelven <see cref="Resultado"/>.</item>
    ///   <item>La UI hace <c>if (r.Exito) … else Dialogos.Error(r.MensajeError)</c>.</item>
    ///   <item>Las excepciones quedan reservadas para errores inesperados (bugs, I/O fatal).</item>
    /// </list>
    /// </summary>
    public readonly record struct Resultado(bool Exito, string MensajeError)
    {
        public static Resultado Ok()                  => new(true,  string.Empty);
        public static Resultado Error(string mensaje) => new(false, mensaje ?? string.Empty);

        public static implicit operator bool(Resultado r) => r.Exito;
    }

    /// <summary>
    /// Variante con valor de retorno. Cuando <see cref="Resultado{T}.Exito"/>
    /// es <c>true</c>, <see cref="Resultado{T}.Valor"/> está garantizado.
    /// </summary>
    public readonly record struct Resultado<T>(bool Exito, T? Valor, string MensajeError)
    {
        public static Resultado<T> Ok(T valor)              => new(true,  valor,   string.Empty);
        public static Resultado<T> Error(string mensaje)    => new(false, default, mensaje ?? string.Empty);

        public static implicit operator bool(Resultado<T> r) => r.Exito;
    }
}
