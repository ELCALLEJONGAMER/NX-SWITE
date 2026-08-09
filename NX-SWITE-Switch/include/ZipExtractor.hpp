// ZipExtractor.hpp — extraccion de archivos ZIP usando minizip.
// Alcance minimo: extraer TODO el contenido de un .zip a una carpeta destino.
#pragma once

#include <string>
#include <functional>

namespace NxSwite
{
    // Resultado de una operacion de extraccion.
    struct ResultadoExtraccion
    {
        bool exito = false;
        std::string mensajeError;
        int archivosExtraidos = 0;
        double tiempoSegundos = 0.0;
    };

    // Callback de progreso de extraccion: se invoca al completar cada entrada
    // del ZIP con (archivosProcesados, totalEntradas, nombreEntradaActual).
    using CallbackProgresoExtraccion =
        std::function<void(int archivosProcesados, int totalEntradas, const std::string& nombreEntrada)>;

    class ZipExtractor
    {
    public:
        // Elimina recursivamente 'carpetaDestino' (si existe) y la vuelve a
        // crear vacia, para evitar que restos de directorios incorrectos de
        // extracciones previas interfieran con la nueva extraccion.
        static bool RecrearCarpetaVacia(const std::string& carpetaDestino);

        // Extrae todo el contenido de 'rutaZip' dentro de 'carpetaDestino'.
        // Crea 'carpetaDestino' si no existe. No elimina el ZIP de origen.
        // 'onProgreso' es opcional; si se proporciona, se invoca al completar
        // cada entrada del ZIP (no por cada bloque, para no afectar el rendimiento).
        static ResultadoExtraccion ExtraerTodo(const std::string& rutaZip, const std::string& carpetaDestino,
                                                const CallbackProgresoExtraccion& onProgreso = nullptr);
    };
}
