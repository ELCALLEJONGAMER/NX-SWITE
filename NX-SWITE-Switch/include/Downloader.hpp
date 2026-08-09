// Downloader.hpp — descarga simple de archivos por HTTP/HTTPS usando libcurl.
// Alcance minimo: descargar una URL fija a un archivo local en la SD.
#pragma once

#include <string>
#include <functional>
#include <cstdint>

namespace NxSwite
{
    // Resultado de una operacion de descarga.
    struct ResultadoDescarga
    {
        bool exito = false;
        std::string mensajeError;

        // Metricas de rendimiento (validas solo si exito == true).
        int64_t tamanoBytes = 0;
        double tiempoSegundos = 0.0;
        double velocidadPromedioMBs = 0.0;

        // Diagnostico: cuantas veces se llamo a fwrite() durante la descarga
        // (con el buffer acumulador de 1 MiB, deberia ser aprox. tamano/1MiB).
        int64_t numeroEscrituras = 0;
    };

    // Callback de progreso: bytesDescargados, bytesTotales (0 si aun no se
    // conoce el total) y velocidadInstantaneaMBs (velocidad actual en MB/s,
    // segun la informacion de libcurl). Se invoca solo cuando cambia el
    // porcentaje entero, o cada ~200ms si el total todavia no se conoce.
    using CallbackProgresoDescarga =
        std::function<void(int64_t bytesDescargados, int64_t bytesTotales, double velocidadInstantaneaMBs)>;

    class Downloader
    {
    public:
        // Inicializa el subsistema de red (curl global). Debe llamarse una
        // sola vez antes de cualquier descarga.
        static bool Inicializar();

        // Libera el subsistema de red (curl global). Llamar al salir de la app.
        static void Finalizar();

        // Descarga el contenido de 'url' y lo guarda en 'rutaDestino'.
        // Crea las carpetas intermedias de 'rutaDestino' si no existen.
        // 'onProgreso' es opcional; si se proporciona, se invoca periodicamente
        // durante la descarga con el progreso actual.
        static ResultadoDescarga DescargarArchivo(const std::string& url, const std::string& rutaDestino,
                                                   const CallbackProgresoDescarga& onProgreso = nullptr);
    };
}

