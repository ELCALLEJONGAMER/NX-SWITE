using NX_Suite.Models;
using System;
using System.Collections.Generic;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Argumentos del evento ProcesarCompletoSolicitado de VistaAsistida.
    /// Transporta TODO lo necesario para ejecutar el flujo Asistido Completo
    /// (particionado + instalación) sin que MainWindow tenga que mantener estado.
    /// </summary>
    public class ProcesarCompletoArgs : EventArgs
    {
        public int                  GbEmuMMC        { get; init; }
        public string?              LetraSD         { get; init; }
        public string               Etiqueta        { get; init; } = "SWITCH SD";
        public int                  NumeroDisco     { get; init; } = -1;
        public List<ModuloConfig>   Modulos         { get; init; } = new();
        public HashSet<string>      IdsDependencias { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Action<string>?      Logger          { get; init; }
    }
}
