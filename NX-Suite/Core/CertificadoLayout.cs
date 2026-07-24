namespace NX_Swite.Core
{
    /// <summary>
    /// Coordenadas de texto sobre la plantilla certificado_plantilla.png (1491×1055).
    /// Edita este archivo para ajustar la posición de cada campo sin tocar la lógica.
    ///
    /// Cómo funciona:
    ///   - X = distancia en píxeles desde el borde izquierdo de la imagen.
    ///   - Y = distancia en píxeles desde el borde superior de la imagen.
    ///   - El texto se ancla por su esquina superior-izquierda.
    ///   - Aumentar X  → mueve el texto hacia la derecha.
    ///   - Aumentar Y  → mueve el texto hacia abajo.
    /// </summary>
    internal static class CertificadoLayout
    {
        // ── Tamaño de la plantilla ──────────────────────────────────────────
        public const int ImagenAncho = 1491;
        public const int ImagenAlto  = 1055;

        // ── Bloque "Generado por / Fecha" (cuadro superior) ─────────────────
        // "Generado por :" termina en x≈310  →  valor comienza en XGeneradoPorValor
        // "Fecha :"        termina en x≈327  →  valor comienza en XFechaValor
        public const int YGeneradoPor      = 310;
        public const int XGeneradoPorValor = 370;

        public const int YFecha            = 345;
        public const int XFechaValor       = 370;

        // ── Número de serie ─────────────────────────────────────────────────
        public const int YSerial        = 450;
        public const int XSerialValor   = 455;

        // ── BISKEYS (filas hex debajo de la etiqueta de la plantilla) ──────
        // La plantilla ya muestra la etiqueta "BISKEYS :"; aquí solo van los valores hex.
        public const int YBiskey0 = 535;
        public const int YBiskey1 = 562;
        public const int YBiskey2 = 588;
        public const int YBiskey3 = 615;
        public const int XBiskeyHexValor = 210; // x donde empieza "bis_key_0X = <hex>"

        // ── prod.keys ───────────────────────────────────────────────────────
        public const int YProdkeys      = 655;
        public const int XProdkeysValor = 455;

        // ── Master key máxima ───────────────────────────────────────────────
        public const int YMasterKey     = 695;
        public const int XMasterKeyValor = 455;

        // ── Compatibilidad confirmada (rango HOS) ───────────────────────────
        public const int YCompatibilidad     = 736;
        public const int XCompatibilidadValor = 455;

        // ── Integridad de generaciones ──────────────────────────────────────
        public const int YIntegridad     = 775;
        public const int XIntegridadValor = 455;

        // ── Verificación criptográfica ──────────────────────────────────────
        public const int YVerificacion      = 818;
        public const int XVerificacionValor = 455;

        // ── NOTAS (cuadro inferior beige) ───────────────────────────────────
        public const int XNotasValor  = 455;   // x donde empieza el texto de nota
        public const int XNotasMax    = 1330;  // límite derecho del cuadro (para word-wrap)
        public const int YNotasLinea1 = 890;   // primera línea de nota
        public const int YNotasLineaH = 25;    // interlineado entre líneas
    }
}
