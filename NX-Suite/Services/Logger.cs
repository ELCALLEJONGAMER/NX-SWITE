using System;
using System.IO;
using NX_Suite.Core.Configuracion;

namespace NX_Suite.Services
{
    public static class Logger
    {
        private static readonly string _logFilePath = ConfiguracionLocal.RutaLog;

        static Logger()
        {
            try { Directory.CreateDirectory(ConfiguracionLocal.RutaAppData); } catch { }
        }

        public static void Info(string message) => WriteToFile("INFO", message);

        public static void Warning(string message) => WriteToFile("WARN", message);

        public static void Error(string message, Exception? ex = null)
        {
            string fullMessage = ex == null
                ? message
                : $"{message} | {ex.GetType().Name}: {ex.Message}";
            WriteToFile("ERROR", fullMessage);
        }

        private static void WriteToFile(string level, string message)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine(logLine);
            }
            catch { }
        }
    }
}