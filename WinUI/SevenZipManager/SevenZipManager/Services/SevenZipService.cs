using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SevenZipManager
{
    public class SevenZipService
    {
        private const string SevenZipDll = "7z.dll";

        [DllImport(SevenZipDll, CallingConvention = CallingConvention.StdCall)]
        private static extern int CreateObject(ref Guid clsid, ref Guid iid, out IntPtr outObject);

        [DllImport(SevenZipDll, CallingConvention = CallingConvention.StdCall)]
        private static extern int SetLargePageMode();

        [DllImport(SevenZipDll, CallingConvention = CallingConvention.StdCall)]
        private static extern int SetCaseSensitive(int caseSensitive);

        public SevenZipService()
        {
            try
            {
                SetLargePageMode();
            }
            catch
            {
            }
        }

        public void Compress(string outputPath, List<string> inputFiles, string format, string password)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var args = BuildCompressArgs(outputPath, inputFiles, format, password);
            Run7zCommand(args);
        }

        public void Extract(string archivePath, string outputPath, string password)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var args = BuildExtractArgs(archivePath, outputPath, password);
            Run7zCommand(args);
        }

        private static string BuildCompressArgs(string outputPath, List<string> inputFiles, string format, string password)
        {
            var args = $"a -t{format}";
            if (!string.IsNullOrEmpty(password))
            {
                args += $" -p{password}";
            }
            args += $" \"{outputPath}\"";
            foreach (var file in inputFiles)
            {
                args += $" \"{file}\"";
            }
            return args;
        }

        private static string BuildExtractArgs(string archivePath, string outputPath, string password)
        {
            var args = $"x \"{archivePath}\" -o\"{outputPath}\" -y";
            if (!string.IsNullOrEmpty(password))
            {
                args += $" -p{password}";
            }
            return args;
        }

        private static void Run7zCommand(string args)
        {
            var sevenZipExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "CPP", "7zip", "Bundles", "Console", "7z.exe");
            if (!File.Exists(sevenZipExe))
            {
                sevenZipExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
            }
            if (!File.Exists(sevenZipExe))
            {
                sevenZipExe = "7z.exe";
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sevenZipExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new Exception($"7z 命令执行失败: {error}");
            }
        }

        public List<string> ListArchiveContents(string archivePath)
        {
            var result = new List<string>();
            var args = $"l \"{archivePath}\"";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "7z.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        result.Add(line);
                    }
                }
                process.WaitForExit();
            }

            return result;
        }
    }
}
