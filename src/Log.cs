using System;
using System.IO;
using System.Text;

namespace ToDoist
{
    /// <summary>
    /// Простой лог в %APPDATA%\ToDoist\error.log — без внешних зависимостей.
    /// </summary>
    public static class Log
    {
        private static readonly object Sync = new object();

        public static string LogDir { get; private set; }
        public static string LogFile { get; private set; }

        /// <summary>Выключает запись (используется в тестах).</summary>
        public static bool Silent { get; set; }

        public static void Init()
        {
            LogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToDoist");
            LogFile = Path.Combine(LogDir, "error.log");
        }

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception exception)
        {
            string text = message;
            if (exception != null)
            {
                text = message + Environment.NewLine + exception;
            }
            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            if (Silent)
            {
                return;
            }

            try
            {
                if (LogFile == null)
                {
                    Init();
                }

                System.IO.Directory.CreateDirectory(LogDir);
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}",
                    DateTime.Now, level, message, Environment.NewLine);

                lock (Sync)
                {
                    File.AppendAllText(LogFile, line, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Логировать ошибку логирования некуда — молча игнорируем.
            }
        }
    }
}
