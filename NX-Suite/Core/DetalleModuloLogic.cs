using NX_Swite.Models;
using System;

namespace NX_Swite.Core
{
    /// <summary>
    /// Tipo de acci&#243;n sem&#225;ntica a representar en el bot&#243;n "Instalar" de la
    /// vista de Detalle. No contiene texto: la UI decide c&#243;mo traducir cada
    /// valor al texto final del bot&#243;n.
    /// </summary>
    public enum AccionInstalarDetalle
    {
        /// <summary>No se debe modificar/mostrar el texto del bot&#243;n Instalar.</summary>
        Ninguna,

        /// <summary>Sin versi&#243;n de chip seleccionada, m&#243;dulo no instalado (instalar la m&#225;s reciente).</summary>
        InstalarNormal,

        /// <summary>Versi&#243;n de chip seleccionada, m&#243;dulo no instalado (instalar esa versi&#243;n concreta).</summary>
        InstalarVersionSeleccionada,

        /// <summary>Downgrade: instalar la versi&#243;n seleccionada SIN eliminar la actual.</summary>
        InstalarVersionSinEliminar
    }

    /// <summary>
    /// Tipo de acci&#243;n sem&#225;ntica a representar en el bot&#243;n "Actualizar" de la
    /// vista de Detalle. No contiene texto: la UI decide c&#243;mo traducir cada
    /// valor al texto final del bot&#243;n.
    /// </summary>
    public enum AccionActualizarDetalle
    {
        /// <summary>No se debe modificar/mostrar el texto del bot&#243;n Actualizar.</summary>
        Ninguna,

        /// <summary>Sin versi&#243;n de chip seleccionada, hay actualizaci&#243;n disponible del m&#243;dulo instalado.</summary>
        ActualizarNormal,

        /// <summary>Upgrade: la versi&#243;n seleccionada es m&#225;s reciente que la instalada.</summary>
        ActualizarAVersion,

        /// <summary>Downgrade: la versi&#243;n seleccionada es m&#225;s antigua que la instalada.</summary>
        DegradarAVersion
    }

    /// <summary>
    /// Resultado de <see cref="DetalleModuloLogic.DeterminarAcciones"/>: decisiones
    /// semanticas necesarias para que la vista de Detalle pinte exactamente los
    /// botones correctos, sin que Core conozca WPF ni genere texto localizado.
    /// </summary>
    public sealed record AccionesDetalleModulo(
        bool MostrarSitioWeb,
        bool MostrarInstalar,
        bool MostrarActualizar,
        bool MostrarBorrar,
        bool MostrarAbrirUbicacion,
        bool EsDegradacion,
        AccionInstalarDetalle TipoAccionInstalar,
        AccionActualizarDetalle TipoAccionActualizar,
        string? VersionObjetivo);

    /// <summary>
    /// L&#243;gica pura (sin WPF) que decide qu&#233; botones/acciones son v&#225;lidos en la
    /// vista de Detalle de un m&#243;dulo, seg&#250;n si hay una versi&#243;n de chip
    /// seleccionada y c&#243;mo se relaciona con la versi&#243;n instalada.
    ///
    /// Extra&#237;do de MainWindow.Detalle.cs (FASE 2 del plan de migraci&#243;n).
    /// No genera texto de bot&#243;n: devuelve estado sem&#225;ntico (<see cref="AccionInstalarDetalle"/>/
    /// <see cref="AccionActualizarDetalle"/>) que la UI traduce a texto.
    /// </summary>
    public static class DetalleModuloLogic
    {
        /// <summary>
        /// Determina las acciones v&#225;lidas para la vista de Detalle de <paramref name="modulo"/>.
        /// </summary>
        /// <param name="modulo">M&#243;dulo mostrado en el detalle.</param>
        /// <param name="versionSeleccionada">
        /// Versi&#243;n de chip seleccionada por el usuario, o <c>null</c> si no hay
        /// selecci&#243;n (comportamiento por defecto sobre la versi&#243;n instalada/m&#225;s reciente).
        /// </param>
        /// <param name="haySd">True si hay una microSD seleccionada actualmente.</param>
        public static AccionesDetalleModulo DeterminarAcciones(
            ModuloConfig modulo,
            ModuloVersion? versionSeleccionada,
            bool haySd)
        {
            bool tieneSitioWeb = !string.IsNullOrWhiteSpace(modulo.UrlOficial);
            bool instalado     = modulo.EstaInstaladoEnSd;

            if (versionSeleccionada == null)
            {
                // Sin selecci?n de chip: comportamiento original
                bool tieneUpdate = modulo.TieneActualizacion;

                return new AccionesDetalleModulo(
                    MostrarSitioWeb: tieneSitioWeb,
                    MostrarInstalar: !instalado,
                    MostrarActualizar: instalado && tieneUpdate,
                    MostrarBorrar: instalado,
                    MostrarAbrirUbicacion: instalado,
                    EsDegradacion: false,
                    TipoAccionInstalar: instalado ? AccionInstalarDetalle.Ninguna : AccionInstalarDetalle.InstalarNormal,
                    TipoAccionActualizar: AccionActualizarDetalle.ActualizarNormal,
                    VersionObjetivo: null);
            }

            var verSel = versionSeleccionada;

            if (verSel.SoloDeteccion)
            {
                // Versi?n bloqueada: solo se puede eliminar si es la instalada
                bool esLaInstalada = instalado &&
                    string.Equals(verSel.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase);

                return new AccionesDetalleModulo(
                    MostrarSitioWeb: tieneSitioWeb,
                    MostrarInstalar: false,
                    MostrarActualizar: false,
                    MostrarBorrar: esLaInstalada,
                    MostrarAbrirUbicacion: esLaInstalada,
                    EsDegradacion: false,
                    TipoAccionInstalar: AccionInstalarDetalle.Ninguna,
                    TipoAccionActualizar: AccionActualizarDetalle.Ninguna,
                    VersionObjetivo: verSel.Version);
            }

            bool esVersionInstalada = instalado &&
                string.Equals(verSel.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase);

            // ?ndice en la lista: posici?n 0 = m&#225;s reciente
            int idxSel = modulo.Versiones.IndexOf(verSel);
            int idxIns = instalado
                ? modulo.Versiones.FindIndex(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase))
                : -1;

            bool esUpgrade   = instalado && !esVersionInstalada && idxSel >= 0 && idxIns >= 0 && idxSel < idxIns;
            bool esDowngrade = instalado && !esVersionInstalada && idxSel >= 0 && idxIns >= 0 && idxSel > idxIns;

            if (esVersionInstalada)
            {
                // La versi?n seleccionada ES la instalada en la SD
                return new AccionesDetalleModulo(
                    MostrarSitioWeb: tieneSitioWeb,
                    MostrarInstalar: false,
                    MostrarActualizar: false,
                    MostrarBorrar: true,
                    MostrarAbrirUbicacion: true,
                    EsDegradacion: false,
                    TipoAccionInstalar: AccionInstalarDetalle.Ninguna,
                    TipoAccionActualizar: AccionActualizarDetalle.Ninguna,
                    VersionObjetivo: verSel.Version);
            }

            if (esUpgrade)
            {
                // Versi?n seleccionada es m&#225;s nueva que la instalada ? actualizar
                return new AccionesDetalleModulo(
                    MostrarSitioWeb: tieneSitioWeb,
                    MostrarInstalar: false,
                    MostrarActualizar: true,
                    MostrarBorrar: false,
                    MostrarAbrirUbicacion: false,
                    EsDegradacion: false,
                    TipoAccionInstalar: AccionInstalarDetalle.Ninguna,
                    TipoAccionActualizar: AccionActualizarDetalle.ActualizarAVersion,
                    VersionObjetivo: verSel.Version);
            }

            if (esDowngrade)
            {
                // Versi?n seleccionada es m&#225;s antigua que la instalada:
                // Actualizar = DEGRADAR (desinstala la actual + instala la vieja)
                // Instalar   = INSTALAR (solo instala, sin eliminar la actual)
                return new AccionesDetalleModulo(
                    MostrarSitioWeb: tieneSitioWeb,
                    MostrarInstalar: true,
                    MostrarActualizar: true,
                    MostrarBorrar: false,
                    MostrarAbrirUbicacion: false,
                    EsDegradacion: true,
                    TipoAccionInstalar: AccionInstalarDetalle.InstalarVersionSinEliminar,
                    TipoAccionActualizar: AccionActualizarDetalle.DegradarAVersion,
                    VersionObjetivo: verSel.Version);
            }

            // M&#243;dulo no instalado ? instalar versi&#243;n seleccionada
            return new AccionesDetalleModulo(
                MostrarSitioWeb: tieneSitioWeb,
                MostrarInstalar: true,
                MostrarActualizar: false,
                MostrarBorrar: false,
                MostrarAbrirUbicacion: false,
                EsDegradacion: false,
                TipoAccionInstalar: AccionInstalarDetalle.InstalarVersionSeleccionada,
                TipoAccionActualizar: AccionActualizarDetalle.Ninguna,
                VersionObjetivo: verSel.Version);
        }
    }
}
