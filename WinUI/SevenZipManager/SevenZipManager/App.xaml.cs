using System;
using System.IO;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace SevenZipManager
{
    public partial class App : Application
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SevenZipManager",
            "startup.log");

        public static Window? MainWindow { get; private set; }
        public static IntPtr WindowHandle { get; private set; }

        public App()
        {
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                Log("OnLaunched begin");
                MainWindow = new MainWindow();
                MainWindow.Activate();
                WindowHandle = WindowNative.GetWindowHandle(MainWindow);
                Log("OnLaunched success");
            }
            catch (Exception ex)
            {
                Log($"OnLaunched exception: {ex}");
                throw;
            }
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log($"Xaml UnhandledException: {e.Exception}");
        }

        private void OnDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
        {
            Log($"Domain UnhandledException: {e.ExceptionObject}");
        }

        private static void Log(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
