using NX_Suite.Core.Configuracion;

namespace NX_Suite.Core
{
    /// <summary>
    /// Contenedor centralizado de servicios infraestructurales de la app
    /// (sonidos, cola visual y caché de iconos). Sustituye al patrón
    /// <c>GestorXxx.Instancia</c> antiguo.
    ///
    /// Convenciones del proyecto:
    /// <list type="bullet">
    ///   <item>El código debe acceder a estos servicios por <c>Servicios.Sonidos</c>,
    ///         <c>Servicios.Cola</c> y <c>Servicios.Iconos</c>, NO instanciando
    ///         los gestores directamente.</item>
    ///   <item>Las instancias se crean perezosamente la primera vez que se
    ///         acceden, leyendo las rutas de caché desde <see cref="ConfiguracionLocal"/>.</item>
    ///   <item>Para tests/mocks, usar <see cref="Reemplazar"/> antes del primer
    ///         acceso para inyectar implementaciones alternativas.</item>
    /// </list>
    ///
    /// Nota: <c>Servicios</c> es el único punto del proyecto donde se mantiene
    /// estado estático asociado a estos componentes. Si en el futuro se quiere
    /// migrar a un contenedor DI completo (Microsoft.Extensions.DI), basta con
    /// reescribir esta clase para que delegue en un <c>IServiceProvider</c>.
    /// </summary>
    public static class Servicios
    {
        private static GestorSonidos? _sonidos;
        private static GestorIconos?          _iconos;
        private static GestorQueue?           _cola;
        private static ServicioActualizacion? _actualizacion;

        /// <summary>Servicio de sonidos (efectos UI, hover, click, navegación, etc.).</summary>
        public static GestorSonidos Sonidos => _sonidos ??= CrearSonidos();

        /// <summary>Servicio de caché y descarga de iconos remotos.</summary>
        public static GestorIconos  Iconos  => _iconos  ??= new GestorIconos(ConfiguracionLocal.RutaCacheIconos);

        /// <summary>Cola visual de operaciones en curso (descargas, instalaciones, etc.).</summary>
        public static GestorQueue   Cola    => _cola    ??= new GestorQueue();

        /// <summary>Servicio de auto-actualización de la app.</summary>
        public static ServicioActualizacion Actualizacion => _actualizacion ??= new ServicioActualizacion();

        private static GestorSonidos CrearSonidos()
        {
            var g = new GestorSonidos();
            g.Configurar(ConfiguracionLocal.RutaCacheSonidos);
            return g;
        }

        /// <summary>
        /// Reemplaza una o más instancias por mocks/dobles de prueba. Llamar
        /// ANTES del primer acceso a la propiedad correspondiente.
        /// </summary>
        public static void Reemplazar(
            GestorSonidos? sonidos = null,
            GestorIconos?  iconos  = null,
            GestorQueue?   cola    = null)
        {
            if (sonidos != null) _sonidos = sonidos;
            if (iconos  != null) _iconos  = iconos;
            if (cola    != null) _cola    = cola;
        }
    }
}
