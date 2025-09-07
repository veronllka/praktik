using System;
using System.IO;

namespace praktik.Models
{
    public static class Logger
    {
        private static readonly object Sync = new object();
        private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "praktik", "app.log");

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                 
            }
        }
    }
}




