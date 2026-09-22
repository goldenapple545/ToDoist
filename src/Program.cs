using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace ToDoist
{
    internal static class Program
    {
        /// <summary>Значение ключа --backdrop=… для проверки режимов стекла.</summary>
        internal static string BackdropArgument;

        [STAThread]
        private static void Main(string[] args)
        {
            BackdropArgument = BackdropSupport.ParseBackdropArgument(args);
            Log.Init();
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            application.DispatcherUnhandledException += OnDispatcherException;

            Window window;
            try
            {
                window = new MainWindowController().Window;
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось создать окно приложения", ex);
                MessageBox.Show(
                    "Не удалось запустить ToDoist:" + Environment.NewLine + ex.Message,
                    "ToDoist", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            application.MainWindow = window;
            application.Run(window);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Log.Info("Приложение закрыто.");
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("Необработанное исключение", e.ExceptionObject as Exception);
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Ошибка в потоке интерфейса", e.Exception);
            e.Handled = true;

            MessageBox.Show(
                "Что-то пошло не так:" + Environment.NewLine + e.Exception.Message +
                Environment.NewLine + Environment.NewLine +
                "Подробности — в " + Log.LogFile,
                "ToDoist", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
