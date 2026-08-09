// NX-SWITE-Switch — main.cpp
// Aplicación de prueba funcional #2 para Nintendo Switch.
// Descarga un ZIP fijo por HTTP/HTTPS y lo extrae a una carpeta staging
// en la microSD, mostrando progreso simple por consola.
// No copia/mueve nada fuera de staging ni borra el ZIP descargado.

#include <switch.h>
#include <cstdio>
#include <string>

#include "Downloader.hpp"
#include "ZipExtractor.hpp"

namespace
{
    // URL fija de prueba (ZIP pequeño y públicamente accesible).
    constexpr const char* URL_PAQUETE_PRUEBA = "https://github.com/ELCALLEJONGAMER/NX-SWITE.ASSETS/raw/refs/heads/main/nx-swite-switch/atmos-22.5.0/22.5.0.zip";

    constexpr const char* RUTA_ZIP_TEMPORAL   = "sdmc:/NX-SWITE/temp/package.zip";
    constexpr const char* RUTA_CARPETA_STAGING = "sdmc:/NX-SWITE/temp/staging";

    // Mantiene la aplicacion viva refrescando la consola. Ya NO permite
    // salir manualmente mediante '+'; el usuario debe regresar al menu
    // HOME. appletMainLoop() se encarga de terminar el bucle si el sistema
    // solicita el cierre (p. ej. al reabrir el HBMenu).
    void EsperarSalida(PadState& pad)
    {
        while (appletMainLoop())
        {
            padUpdate(&pad);
            consoleUpdate(NULL);
        }
    }

    // RAII: garantiza socketExit() exactamente una vez, sin importar por
    // que camino se salga de main() (exito o cualquier error temprano).
    struct GuardaSockets
    {
        bool activo = false;

        Result Inicializar()
        {
            Result rc = socketInitializeDefault();
            activo = R_SUCCEEDED(rc);
            return rc;
        }

        void Cerrar()
        {
            if (activo)
            {
                printf("Cerrando sockets...\n");
                consoleUpdate(NULL);
                socketExit();
                activo = false;
            }
        }

        ~GuardaSockets() { Cerrar(); }
    };

    // RAII: garantiza Downloader::Finalizar() (curl_global_cleanup) exactamente
    // una vez, sin importar el camino de salida.
    struct GuardaCurl
    {
        bool activo = false;

        bool Inicializar()
        {
            activo = NxSwite::Downloader::Inicializar();
            return activo;
        }

        void Cerrar()
        {
            if (activo)
            {
                printf("Cerrando CURL...\n");
                consoleUpdate(NULL);
                NxSwite::Downloader::Finalizar();
                activo = false;
            }
        }

        ~GuardaCurl() { Cerrar(); }
    };

    // RAII: garantiza consoleExit(NULL) exactamente una vez, al final de main().
    struct GuardaConsola
    {
        GuardaConsola() { consoleInit(NULL); }

        ~GuardaConsola()
        {
            printf("Cerrando consola...\n");
            consoleUpdate(NULL);
            consoleExit(NULL);
        }
    };
}

int main(int argc, char* argv[])
{
    // El orden de destruccion es el inverso al de declaracion: al salir de
    // main() (por return o no), primero se cierra curl, luego los sockets
    // y finalmente la consola, sin importar por que camino se llegue al final.
    GuardaConsola guardaConsola;
    GuardaSockets guardaSockets;
    GuardaCurl guardaCurl;

    PadState pad;
    padConfigureInput(1, HidNpadStyleSet_NpadStandard);
    padInitializeDefault(&pad);

    printf("NX-SWITE\n");
    printf("Switch Updater\n");
    printf("v0.0.1\n\n");

    // 1) Inicializa la conexión de red (sockets de libnx + libcurl).
    printf("Conectando...\n");
    consoleUpdate(NULL);

    Result rc = guardaSockets.Inicializar();
    if (R_FAILED(rc))
    {
        printf("ERROR: no se pudo inicializar los sockets (0x%x).\n", rc);
        printf("\nPulsa HOME para regresar al menu.\n");
        EsperarSalida(pad);
        return 1;
    }

    if (!guardaCurl.Inicializar())
    {
        printf("ERROR: no se pudo inicializar la red.\n");
        printf("\nPulsa HOME para regresar al menu.\n");
        EsperarSalida(pad);
        return 1;
    }

    // 2) Descarga el ZIP a la ruta temporal fija. Salida simplificada
    //    temporalmente para el benchmark: solo titulo mientras descarga,
    //    y metricas finales al terminar.
    printf("Descargando paquete...\n");
    consoleUpdate(NULL);

    NxSwite::ResultadoDescarga resultadoDescarga =
        NxSwite::Downloader::DescargarArchivo(URL_PAQUETE_PRUEBA, RUTA_ZIP_TEMPORAL, nullptr);

    if (!resultadoDescarga.exito)
    {
        printf("\nERROR al descargar: %s\n", resultadoDescarga.mensajeError.c_str());
        printf("\nPulsa HOME para regresar al menu.\n");
        EsperarSalida(pad);
        return 1;
    }

    {
        double tamanoMB = resultadoDescarga.tamanoBytes / (1024.0 * 1024.0);
        printf("\nDescarga completada\n");
        printf("Tamano: %.1f MB\n", tamanoMB);
        printf("Tiempo: %.1f s\n", resultadoDescarga.tiempoSegundos);
        printf("Velocidad media: %.2f MB/s\n", resultadoDescarga.velocidadPromedioMBs);
        consoleUpdate(NULL);
    }

    // La red ya no se necesita para la extraccion: se cierra explicitamente
    // aqui (con mensaje de diagnostico) en vez de esperar al final de main().
    guardaCurl.Cerrar();
    guardaSockets.Cerrar();

    // 3) Extrae TODO el contenido del ZIP a la carpeta staging.
    //    Salida simplificada TEMPORALMENTE: sin barra ni progreso por
    //    archivo (se implementara una interfaz visual real mas adelante).
    printf("\nExtrayendo paquete...\n");
    consoleUpdate(NULL);

    NxSwite::ResultadoExtraccion resultadoExtraccion =
        NxSwite::ZipExtractor::ExtraerTodo(RUTA_ZIP_TEMPORAL, RUTA_CARPETA_STAGING, nullptr);

    if (!resultadoExtraccion.exito)
    {
        printf("\nERROR al extraer: %s\n", resultadoExtraccion.mensajeError.c_str());
        printf("\nPulsa HOME para regresar al menu.\n");
        EsperarSalida(pad);
        return 1;
    }

    printf("\nPaquete preparado correctamente.\n\n");
    printf("Archivos extraidos: %d\n", resultadoExtraccion.archivosExtraidos);
    printf("Tiempo de extraccion: %.1f s\n", resultadoExtraccion.tiempoSegundos);

    printf("\nPulsa HOME para regresar al menu.\n");
    EsperarSalida(pad);

    return 0;
}


