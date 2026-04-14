using System;
using System.IO;

namespace NX_Suite.Services
{
    public static class Logger
    {
        private static readonly string logFilePath = "NX_Suite_Log.txt";

        public static void Info(string message)
        {
            WriteToFile("INFO", message);
        }

        public static void Error(string message, Exception? ex = default)
        {
            string fullMessage = ex == null ? message : $"{message} | Detalle: {ex.Message}";
            WriteToFile("ERROR", fullMessage);
        }

        private static void WriteToFile(string level, string message)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(logFilePath, logLine + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine(logLine);
            }
            catch { }
        }
    }
}