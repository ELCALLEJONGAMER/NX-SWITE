// ZipExtractor.cpp — implementacion de extraccion ZIP con minizip (unzip.h).
#include "ZipExtractor.hpp"

#include <minizip/unzip.h>
#include <sys/stat.h>
#include <dirent.h>
#include <cstdio>
#include <cstring>
#include <vector>
#include <sstream>
#include <switch.h>

namespace NxSwite
{
    namespace
    {
        // Bufer de extraccion reutilizable (256 KB) para reducir el numero
        // de llamadas a unzReadCurrentFile()/fwrite() por archivo.
        constexpr int TAMANO_BUFER = 256 * 1024;
        constexpr double TICKS_POR_SEGUNDO = 19200000.0;

        // Reemplaza '\' por '/' para normalizar separadores de ruta.
        std::string NormalizarSeparadores(const std::string& ruta)
        {
            std::string resultado = ruta;
            for (char& c : resultado)
            {
                if (c == '\\')
                    c = '/';
            }
            return resultado;
        }

        // Comprueba si alguno de los componentes de la ruta es '..' (path traversal).
        bool ContieneComponentePadre(const std::string& rutaNormalizada)
        {
            std::stringstream ss(rutaNormalizada);
            std::string componente;
            while (std::getline(ss, componente, '/'))
            {
                if (componente == "..")
                    return true;
            }
            return false;
        }

        // Crea recursivamente TODAS las carpetas de 'rutaDirectorio' (la ruta
        // completa se trata como directorio a crear, componente a componente).
        void CrearDirectorioRecursivo(const std::string& rutaDirectorio)
        {
            std::string acumulado;
            for (size_t i = 0; i < rutaDirectorio.size(); ++i)
            {
                if (rutaDirectorio[i] == '/')
                {
                    acumulado = rutaDirectorio.substr(0, i);
                    if (!acumulado.empty())
                    {
                        mkdir(acumulado.c_str(), 0777);
                    }
                }
            }
            if (!rutaDirectorio.empty())
            {
                mkdir(rutaDirectorio.c_str(), 0777);
            }
        }

        // Crea recursivamente solo las carpetas PADRE de 'rutaArchivo'
        // (todo lo que hay antes de la última '/'), sin crear el propio archivo.
        void CrearCarpetasPadre(const std::string& rutaArchivo)
        {
            size_t ultimaBarra = rutaArchivo.find_last_of('/');
            if (ultimaBarra == std::string::npos)
                return;

            std::string directorioPadre = rutaArchivo.substr(0, ultimaBarra);
            if (!directorioPadre.empty())
            {
                CrearDirectorioRecursivo(directorioPadre);
            }
        }

        bool TerminaConBarra(const std::string& texto)
        {
            return !texto.empty() && texto.back() == '/';
        }

        // Elimina recursivamente el contenido de 'ruta' (archivos y subcarpetas),
        // y finalmente la propia carpeta 'ruta'.
        void EliminarRecursivo(const std::string& ruta)
        {
            DIR* dir = opendir(ruta.c_str());
            if (!dir)
                return;

            struct dirent* entrada;
            while ((entrada = readdir(dir)) != nullptr)
            {
                std::string nombre = entrada->d_name;
                if (nombre == "." || nombre == "..")
                    continue;

                std::string rutaHijo = ruta + "/" + nombre;

                struct stat info;
                if (stat(rutaHijo.c_str(), &info) == 0 && S_ISDIR(info.st_mode))
                {
                    EliminarRecursivo(rutaHijo);
                }
                else
                {
                    remove(rutaHijo.c_str());
                }
            }

            closedir(dir);
            rmdir(ruta.c_str());
        }
    }

    bool ZipExtractor::RecrearCarpetaVacia(const std::string& carpetaDestino)
    {
        EliminarRecursivo(carpetaDestino);
        CrearDirectorioRecursivo(carpetaDestino);

        struct stat info;
        return stat(carpetaDestino.c_str(), &info) == 0 && S_ISDIR(info.st_mode);
    }

    ResultadoExtraccion ZipExtractor::ExtraerTodo(const std::string& rutaZip, const std::string& carpetaDestino,
                                                   const CallbackProgresoExtraccion& onProgreso)
    {
        ResultadoExtraccion resultado;
        u64 tickInicio = armGetSystemTick();

        // Elimina cualquier resto de una extraccion anterior y recrea la
        // carpeta destino vacia, para evitar que directorios incorrectos de
        // pruebas previas interfieran con la nueva extraccion. Si no puede
        // dejarse completamente limpio, se cancela la extraccion.
        if (!RecrearCarpetaVacia(carpetaDestino))
        {
            resultado.exito = false;
            resultado.mensajeError = "No se pudo preparar la carpeta destino: " + carpetaDestino;
            return resultado;
        }

        unzFile zip = unzOpen(rutaZip.c_str());
        if (!zip)
        {
            resultado.exito = false;
            resultado.mensajeError = "No se pudo abrir el ZIP: " + rutaZip;
            return resultado;
        }

        unz_global_info infoGlobal;
        int totalEntradas = 0;
        if (unzGetGlobalInfo(zip, &infoGlobal) == UNZ_OK)
            totalEntradas = static_cast<int>(infoGlobal.number_entry);

        if (unzGoToFirstFile(zip) != UNZ_OK)
        {
            unzClose(zip);
            resultado.exito = false;
            resultado.mensajeError = "El ZIP esta vacio o esta dañado";
            return resultado;
        }

        // Bufer reutilizado en TODAS las entradas: se reserva una unica vez
        // y nunca se libera/realoca dentro del bucle de extraccion.
        std::vector<char> buffer(TAMANO_BUFER);
        int entradasProcesadas = 0;

        do
        {
            char nombreEntradaCrudo[512] = {0};
            unz_file_info infoEntrada;

            if (unzGetCurrentFileInfo(zip, &infoEntrada, nombreEntradaCrudo, sizeof(nombreEntradaCrudo),
                                       nullptr, 0, nullptr, 0) != UNZ_OK)
            {
                resultado.exito = false;
                resultado.mensajeError = "Error leyendo informacion de una entrada del ZIP";
                unzClose(zip);
                return resultado;
            }

            // Normaliza separadores ('\' -> '/') y bloquea entradas con '..'
            // que intenten escapar de la carpeta de staging.
            std::string nombreEntrada = NormalizarSeparadores(nombreEntradaCrudo);

            if (ContieneComponentePadre(nombreEntrada))
            {
                resultado.exito = false;
                resultado.mensajeError = "Entrada de ZIP no permitida (path traversal): " + nombreEntrada;
                unzClose(zip);
                return resultado;
            }

            std::string rutaSalida = carpetaDestino + "/" + nombreEntrada;

            if (TerminaConBarra(rutaSalida))
            {
                // Es una carpeta explícita dentro del ZIP: crearla y continuar.
                CrearDirectorioRecursivo(rutaSalida);
            }
            else
            {
                // Es un archivo: asegura solo sus carpetas PADRE (nunca crea
                // una carpeta con el propio nombre del archivo) y extrae su contenido.
                CrearCarpetasPadre(rutaSalida);

                if (unzOpenCurrentFile(zip) != UNZ_OK)
                {
                    resultado.exito = false;
                    resultado.mensajeError = "No se pudo abrir la entrada: " + nombreEntrada;
                    unzClose(zip);
                    return resultado;
                }

                FILE* archivoSalida = fopen(rutaSalida.c_str(), "wb");
                if (!archivoSalida)
                {
                    unzCloseCurrentFile(zip);
                    resultado.exito = false;
                    resultado.mensajeError = "No se pudo crear el archivo: " + rutaSalida;
                    unzClose(zip);
                    return resultado;
                }

                int bytesLeidos = 0;
                while ((bytesLeidos = unzReadCurrentFile(zip, buffer.data(), TAMANO_BUFER)) > 0)
                {
                    fwrite(buffer.data(), 1, bytesLeidos, archivoSalida);
                }

                fclose(archivoSalida);
                unzCloseCurrentFile(zip);

                if (bytesLeidos < 0)
                {
                    resultado.exito = false;
                    resultado.mensajeError = "Error leyendo datos de: " + nombreEntrada;
                    unzClose(zip);
                    return resultado;
                }

                resultado.archivosExtraidos++;
            }

            // Reporta progreso una vez por entrada completada (no por bloque).
            entradasProcesadas++;
            if (onProgreso)
                onProgreso(entradasProcesadas, totalEntradas, nombreEntrada);
        } while (unzGoToNextFile(zip) == UNZ_OK);

        unzClose(zip);

        resultado.exito = true;
        resultado.tiempoSegundos = (armGetSystemTick() - tickInicio) / TICKS_POR_SEGUNDO;
        return resultado;
    }
}
