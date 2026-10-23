using System;
using System.IO;
using System.Windows;
using Kerf.UI;

namespace Kerf
{
    public static class Program
    {
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KerfStudio", "app.log");

        public static void Log(string msg)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogFile, string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}\r\n", DateTime.Now, msg));
            }
            catch { }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Log("================ App Started ================");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log("AppDomain UnhandledException: " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
            };

            try
            {
                var app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Log("DispatcherUnhandledException: " + e.Exception.ToString());
                    e.Handled = false;
                };

                string initialFile = null;

                if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    if (File.Exists(args[0]))
                    {
                        initialFile = args[0];
                    }
                }

                Log("Displaying After Effects-style Splash & Loading Screen...");
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var splash = new SplashScreenWindow(initialFile);
                splash.Show();
                int exitCode = app.Run();
                Log("Application exited with code: " + exitCode);
                return exitCode;
            }
            catch (Exception ex)
            {
                Log("Fatal exception in Main: " + ex.ToString());
                MessageBox.Show(
                    "A critical error occurred in Kerf Studio:\n\n" + ex.ToString(),
                    "Kerf Studio Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
