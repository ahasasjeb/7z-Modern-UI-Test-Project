using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                Log("LaunchActivated Args: " + (args.Arguments ?? string.Empty));
                Log("CommandLine Args: " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));

                var sfxArchivePath = ResolveSfxArchivePath(args.Arguments);
                if (!string.IsNullOrWhiteSpace(sfxArchivePath))
                {
                    Log("SFX mode launch: " + sfxArchivePath);
                    MainWindow = new SfxWindow(sfxArchivePath);
                }
                else
                {
                    MainWindow = new MainWindow();
                }

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

        private static string? ResolveSfxArchivePath(string? launchArguments)
        {
            var explicitPath = GetExplicitSfxArchivePath(launchArguments);
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            explicitPath = GetExplicitSfxArchivePath(Environment.GetCommandLineArgs());
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return null;
            }

            var sidecarPath = Path.ChangeExtension(processPath, ".sfxpath");
            if (File.Exists(sidecarPath))
            {
                var relativeArchive = File.ReadAllText(sidecarPath).Trim();
                if (!string.IsNullOrWhiteSpace(relativeArchive))
                {
                    var baseDir = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
                    var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativeArchive));
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            var siblingArchivePath = Path.ChangeExtension(processPath, ".7z");
            if (File.Exists(siblingArchivePath))
            {
                return siblingArchivePath;
            }

            return null;
        }

        private static string? GetExplicitSfxArchivePath(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return null;
            }

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i]?.Trim();
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (arg.StartsWith("--sfx-archive=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg["--sfx-archive=".Length..].Trim('"');
                }

                if (arg.Equals("--sfx-archive", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1].Trim().Trim('"');
                }
            }

            return null;
        }

        private static string? GetExplicitSfxArchivePath(string? launchArguments)
        {
            if (string.IsNullOrWhiteSpace(launchArguments))
            {
                return null;
            }

            var tokens = Regex.Matches(launchArguments, "\"[^\"]+\"|\\S+")
                .Select(m => m.Value.Trim())
                .Select(v => v.StartsWith("\"") && v.EndsWith("\"") && v.Length >= 2 ? v[1..^1] : v)
                .ToList();

            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Equals("--sfx-archive", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Count)
                {
                    return tokens[i + 1];
                }
            }

            return null;
        }
    }
}
