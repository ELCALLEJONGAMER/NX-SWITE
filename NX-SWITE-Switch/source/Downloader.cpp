// Downloader.cpp — implementacion de descarga por HTTP/HTTPS con libcurl.
#include "Downloader.hpp"

#include <curl/curl.h>
#include <cstdio>
#include <cstring>
#include <sys/stat.h>
#include <cerrno>
#include <switch.h>
#include <vector>

namespace NxSwite
{
    namespace
    {
        // Crea recursivamente las carpetas necesarias para que 'rutaArchivo'
        // sea escribible (equivalente a 'mkdir -p' sobre el directorio padre).
        void CrearCarpetasIntermedias(const std::string& rutaArchivo)
        {
            std::string acumulado;

            // Busca la ultima barra para quedarnos solo con el directorio.
            size_t ultimaBarra = rutaArchivo.find_last_of('/');
            if (ultimaBarra == std::string::npos)
                return;

            std::string directorio = rutaArchivo.substr(0, ultimaBarra);

            for (size_t i = 0; i <= directorio.size(); ++i)
            {
                if (i == directorio.size() || directorio[i] == '/')
                {
                    acumulado = directorio.substr(0, i);
                    if (!acumulado.empty())
                    {
                        mkdir(acumulado.c_str(), 0777);
                    }
                }
            }
        }

        // Tamano del buffer acumulador de escritura: los bloques pequeños que
        // entrega libcurl se copian aqui, y solo se llama a fwrite() cuando el
        // buffer se llena (o al finalizar la descarga con lo que quede).
        constexpr size_t TAMANO_BUFER_ESCRITURA = 1024 * 1024; // 1 MiB

        // Estado del buffer acumulador de escritura, pasado como CURLOPT_WRITEDATA.
        struct BuferEscritura
        {
            FILE* archivo = nullptr;
            std::vector<char> buffer;
            size_t usados = 0;
            bool errorEscritura = false;
            int64_t numeroEscrituras = 0; // diagnostico: cuantas veces se llamo a fwrite()

            BuferEscritura() { buffer.resize(TAMANO_BUFER_ESCRITURA); }
        };

        // Callback de escritura de libcurl: copia los bytes recibidos al
        // buffer acumulador de 1 MiB; solo cuando el buffer se llena se
        // ejecuta una unica llamada a fwrite() con el bloque completo. El
        // buffer se reserva una sola vez (en el constructor de BuferEscritura)
        // y se reutiliza durante toda la descarga.
        size_t EscribirEnArchivo(void* datos, size_t tamano, size_t nmemb, void* stream)
        {
            BuferEscritura* estado = static_cast<BuferEscritura*>(stream);
            if (!estado || estado->errorEscritura)
                return 0; // aborta la transferencia: libcurl detecta el mismatch.

            size_t bytesRecibidos = tamano * nmemb;
            const char* origen = static_cast<const char*>(datos);
            size_t procesados = 0;

            while (procesados < bytesRecibidos)
            {
                size_t espacioLibre = estado->buffer.size() - estado->usados;
                size_t porCopiar = bytesRecibidos - procesados;
                if (porCopiar > espacioLibre)
                    porCopiar = espacioLibre;

                memcpy(estado->buffer.data() + estado->usados, origen + procesados, porCopiar);
                estado->usados += porCopiar;
                procesados += porCopiar;

                // Buffer lleno: se vuelca a disco en una unica escritura grande.
                if (estado->usados == estado->buffer.size())
                {
                    size_t aEscribir = estado->usados;
                    size_t escritos = fwrite(estado->buffer.data(), 1, aEscribir, estado->archivo);
                    estado->numeroEscrituras++;
                    estado->usados = 0;

                    if (escritos != aEscribir)
                    {
                        estado->errorEscritura = true;
                        return 0; // aborta la transferencia de forma controlada.
                    }
                }
            }

            return bytesRecibidos;
        }

        // Escribe el resto de bytes que quedan en el buffer acumulador (menos
        // de 1 MiB) al finalizar la descarga. Devuelve false si falla.
        bool VolcarRestoBuferEscritura(BuferEscritura& estado)
        {
            if (estado.usados == 0)
                return true;

            size_t aEscribir = estado.usados;
            size_t escritos = fwrite(estado.buffer.data(), 1, aEscribir, estado.archivo);
            estado.numeroEscrituras++;
            estado.usados = 0;

            return escritos == aEscribir;
        }

        // Estado que se pasa al callback de progreso de curl para poder
        // filtrar cuantas veces se invoca realmente al callback del usuario.
        struct EstadoProgreso
        {
            const CallbackProgresoDescarga* callback = nullptr;
            CURL* curl = nullptr;
            int ultimoPorcentajeReportado = -1;
            int64_t ultimosBytesReportados = -1;
            u64 ultimoTickReportado = 0;
        };

        // Obtiene la velocidad de descarga instantanea (MB/s) reportada por
        // libcurl para el 'curl' dado; 0.0 si no esta disponible.
        double VelocidadInstantaneaMBs(CURL* curl)
        {
            if (!curl)
                return 0.0;

            curl_off_t bytesPorSegundo = 0;
            if (curl_easy_getinfo(curl, CURLINFO_SPEED_DOWNLOAD_T, &bytesPorSegundo) != CURLE_OK)
                return 0.0;

            return static_cast<double>(bytesPorSegundo) / (1024.0 * 1024.0);
        }

        // ~200ms expresados en ticks del reloj del sistema (armGetSystemTick).
        constexpr double TICKS_POR_SEGUNDO = 19200000.0;
        constexpr double INTERVALO_MIN_SEGUNDOS = 0.2;

        // Callback de progreso de libcurl (CURLOPT_XFERINFOFUNCTION).
        // Solo reenvia al callback del usuario cuando cambia el porcentaje
        // entero Y ademas ha pasado al menos ~200ms desde el ultimo reporte,
        // para evitar refrescos excesivos de consola que ralentizan la descarga.
        int ProgresoCurl(void* clientp, curl_off_t dltotal, curl_off_t dlnow,
                          curl_off_t /*ultotal*/, curl_off_t /*ulnow*/)
        {
            EstadoProgreso* estado = static_cast<EstadoProgreso*>(clientp);
            if (!estado || !estado->callback || !*estado->callback)
                return 0;

            int64_t bytesDescargados = static_cast<int64_t>(dlnow);
            int64_t bytesTotales = static_cast<int64_t>(dltotal);

            u64 tickActual = armGetSystemTick();
            double segundosDesdeUltimoReporte =
                (tickActual - estado->ultimoTickReportado) / TICKS_POR_SEGUNDO;

            if (bytesTotales > 0)
            {
                int porcentaje = static_cast<int>((bytesDescargados * 100) / bytesTotales);
                bool cambioPorcentaje = porcentaje != estado->ultimoPorcentajeReportado;
                bool esFinal = bytesDescargados >= bytesTotales;

                if (!esFinal && (!cambioPorcentaje || segundosDesdeUltimoReporte < INTERVALO_MIN_SEGUNDOS))
                    return 0;

                estado->ultimoPorcentajeReportado = porcentaje;
                estado->ultimoTickReportado = tickActual;
                (*estado->callback)(bytesDescargados, bytesTotales, VelocidadInstantaneaMBs(estado->curl));
            }
            else
            {
                // Total desconocido todavia: reporta solo cada ~256KB o ~200ms.
                constexpr int64_t PASO_BYTES_SIN_TOTAL = 256 * 1024;
                bool avanzoBastante = estado->ultimosBytesReportados < 0 ||
                    (bytesDescargados - estado->ultimosBytesReportados) >= PASO_BYTES_SIN_TOTAL;

                if (!avanzoBastante && segundosDesdeUltimoReporte < INTERVALO_MIN_SEGUNDOS)
                    return 0;

                estado->ultimosBytesReportados = bytesDescargados;
                estado->ultimoTickReportado = tickActual;
                (*estado->callback)(bytesDescargados, 0, VelocidadInstantaneaMBs(estado->curl));
            }

            return 0;
        }

        // Aplica la configuracion base de CURL usada por DescargarArchivo().
        void ConfigurarCurlComun(CURL* curl, const std::string& url)
        {
            curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
            curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
            curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
            curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);
            curl_easy_setopt(curl, CURLOPT_USERAGENT, "NX-SWITE-Switch/0.0.1");
            curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT, 30L);
            curl_easy_setopt(curl, CURLOPT_TIMEOUT, 300L);
            // Bufer de recepcion de libcurl (256 KB): reduce el numero de
            // llamadas al callback de escritura.
            curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 256L * 1024L);
        }
    }

    bool Downloader::Inicializar()
    {
        CURLcode resultado = curl_global_init(CURL_GLOBAL_DEFAULT);
        return resultado == CURLE_OK;
    }

    void Downloader::Finalizar()
    {
        curl_global_cleanup();
    }

    ResultadoDescarga Downloader::DescargarArchivo(const std::string& url, const std::string& rutaDestino,
                                                    const CallbackProgresoDescarga& onProgreso)
    {
        ResultadoDescarga resultado;

        CrearCarpetasIntermedias(rutaDestino);

        FILE* archivo = fopen(rutaDestino.c_str(), "wb");
        if (!archivo)
        {
            resultado.exito = false;
            resultado.mensajeError = "No se pudo crear el archivo de destino: " + rutaDestino;
            return resultado;
        }

        CURL* curl = curl_easy_init();
        if (!curl)
        {
            fclose(archivo);
            resultado.exito = false;
            resultado.mensajeError = "No se pudo inicializar libcurl";
            return resultado;
        }

        EstadoProgreso estadoProgreso;
        estadoProgreso.callback = &onProgreso;
        estadoProgreso.curl = curl;

        // Buffer acumulador de escritura: se reserva UNA sola vez (1 MiB) y
        // se reutiliza durante toda la descarga; solo se llama a fwrite()
        // cuando se llena o al finalizar con el resto pendiente.
        BuferEscritura buferEscritura;
        buferEscritura.archivo = archivo;

        ConfigurarCurlComun(curl, url);
        curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, EscribirEnArchivo);
        curl_easy_setopt(curl, CURLOPT_WRITEDATA, &buferEscritura);

        if (onProgreso)
        {
            curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
            curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, ProgresoCurl);
            curl_easy_setopt(curl, CURLOPT_XFERINFODATA, &estadoProgreso);
        }

        CURLcode codigo = curl_easy_perform(curl);

        // Si la transferencia se completo sin error de CURL, aun asi hay que
        // volcar el resto del buffer acumulador (menos de 1 MiB) que pueda
        // quedar pendiente de escritura.
        bool escrituraFinalOk = true;
        if (codigo == CURLE_OK && !buferEscritura.errorEscritura)
            escrituraFinalOk = VolcarRestoBuferEscritura(buferEscritura);

        if (codigo == CURLE_OK && !buferEscritura.errorEscritura && escrituraFinalOk)
        {
            curl_off_t tamanoDescargado = 0;
            double tiempoTotal = 0.0;
            curl_easy_getinfo(curl, CURLINFO_SIZE_DOWNLOAD_T, &tamanoDescargado);
            curl_easy_getinfo(curl, CURLINFO_TOTAL_TIME, &tiempoTotal);

            resultado.tamanoBytes = static_cast<int64_t>(tamanoDescargado);
            resultado.tiempoSegundos = tiempoTotal;
            resultado.numeroEscrituras = buferEscritura.numeroEscrituras;
            if (tiempoTotal > 0.0)
            {
                double mb = resultado.tamanoBytes / (1024.0 * 1024.0);
                resultado.velocidadPromedioMBs = mb / tiempoTotal;
            }
        }

        curl_easy_cleanup(curl);
        fclose(archivo);

        if (buferEscritura.errorEscritura || !escrituraFinalOk)
        {
            resultado.exito = false;
            resultado.mensajeError = "Fallo al escribir el archivo en la SD: " + rutaDestino;
            return resultado;
        }

        if (codigo != CURLE_OK)
        {
            resultado.exito = false;
            resultado.mensajeError = curl_easy_strerror(codigo);
            return resultado;
        }

        resultado.exito = true;
        return resultado;
    }
}
