using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public void Compress(string outputPath, List<string> inputFiles, CompressionOptions options)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var normalizedFormat = string.IsNullOrWhiteSpace(options.Format) ? "7z" : options.Format.Trim().ToLowerInvariant();
            if (options.CreateSfx && normalizedFormat == "7z")
            {
                BuildWinUiSfxPackage(outputPath, inputFiles, options);
                return;
            }

            var args = BuildCompressArgs(outputPath, inputFiles, options);
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

        private static string BuildCompressArgs(string outputPath, List<string> inputFiles, CompressionOptions options)
        {
            var format = string.IsNullOrWhiteSpace(options.Format) ? "7z" : options.Format.Trim().ToLowerInvariant();
            var command = options.UpdateMode switch
            {
                "update" => "u",
                _ => "a"
            };

            var args = $"{command} -t{format} -mx={Math.Clamp(options.CompressionLevel, 0, 9)}";

            if (options.UpdateMode == "sync")
            {
                args += " -u- -up0q3r2x2y2z0w2";
            }

            if (options.PathMode == "full")
            {
                args += " -spf";
            }

            if (options.CompressSharedFiles)
            {
                args += " -ssw";
            }

            if (options.ThreadCount > 0)
            {
                args += $" -mmt={options.ThreadCount}";
            }

            if (!string.IsNullOrWhiteSpace(options.Method))
            {
                var method = options.Method!.Trim().ToLowerInvariant();
                if (format == "7z" || format == "zip")
                {
                    args += $" -m0={method}";
                }
            }

            if (format == "7z")
            {
                if (!string.IsNullOrWhiteSpace(options.DictionarySize))
                {
                    args += $" -md={options.DictionarySize}";
                }

                if (options.WordSize is > 0)
                {
                    args += $" -mfb={options.WordSize.Value}";
                }
            }

            if (!string.IsNullOrEmpty(options.Password))
            {
                var escapedPassword = options.Password.Replace("\"", string.Empty);
                args += $" -p\"{escapedPassword}\"";
                if (format == "7z" || format == "zip")
                {
                    args += $" -mem={options.EncryptionMethod}";
                }

                if (format == "7z" && options.EncryptHeaders)
                {
                    args += " -mhe=on";
                }
            }

            if (format == "7z")
            {
                if (options.SolidBlockSize == "off")
                {
                    args += " -ms=off";
                }
                else if (!string.IsNullOrWhiteSpace(options.SolidBlockSize))
                {
                    args += $" -ms={options.SolidBlockSize}";
                }
                else
                {
                    args += options.SolidArchive ? " -ms=on" : " -ms=off";
                }
            }

            if (!string.IsNullOrWhiteSpace(options.VolumeSize))
            {
                args += $" -v{options.VolumeSize}";
            }

            if (options.DeleteSourceFiles)
            {
                args += " -sdel";
            }

            if (!string.IsNullOrWhiteSpace(options.AdditionalParameters))
            {
                args += " " + options.AdditionalParameters;
            }

            args += $" \"{outputPath}\"";
            foreach (var file in inputFiles)
            {
                args += $" \"{file}\"";
            }
            return args;
        }

        private void BuildWinUiSfxPackage(string outputExePath, List<string> inputFiles, CompressionOptions options)
        {
            var archivePath = Path.ChangeExtension(outputExePath, ".7z");
            var archiveOptions = new CompressionOptions
            {
                UpdateMode = options.UpdateMode,
                PathMode = options.PathMode,
                Format = "7z",
                CompressionLevel = options.CompressionLevel,
                Method = options.Method,
                DictionarySize = options.DictionarySize,
                WordSize = options.WordSize,
                ThreadCount = options.ThreadCount,
                SolidArchive = options.SolidArchive,
                SolidBlockSize = options.SolidBlockSize,
                EncryptionMethod = options.EncryptionMethod,
                EncryptHeaders = options.EncryptHeaders,
                Password = options.Password,
                VolumeSize = options.VolumeSize,
                DeleteSourceFiles = options.DeleteSourceFiles,
                CreateSfx = false,
                CompressSharedFiles = options.CompressSharedFiles,
                AdditionalParameters = options.AdditionalParameters
            };

            var compressArgs = BuildCompressArgs(archivePath, inputFiles, archiveOptions);
            Run7zCommand(compressArgs);

            var appDir = AppContext.BaseDirectory;
            var appExePath = ResolveAppExecutablePath(appDir);
            if (string.IsNullOrWhiteSpace(appExePath) || !File.Exists(appExePath))
            {
                throw new Exception("未找到 WinUI3 启动程序，无法创建自解压包。");
            }

            var outputDir = Path.GetDirectoryName(outputExePath);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                throw new Exception("输出路径无效。");
            }

            Directory.CreateDirectory(outputDir);

            CopyDirectoryRecursive(appDir, outputDir);

            File.Copy(appExePath, outputExePath, true);

            EnsureRenamedHostSupportFiles(appDir, appExePath, outputExePath);

            var localArchiveName = Path.GetFileName(archivePath);
            var localArchivePath = Path.Combine(outputDir, localArchiveName);
            if (!archivePath.Equals(localArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(archivePath, localArchivePath, true);
            }

            var sfxSidecarPath = Path.ChangeExtension(outputExePath, ".sfxpath");
            File.WriteAllText(sfxSidecarPath, localArchiveName);
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, directory);
                var destinationSubdir = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(destinationSubdir);
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var destinationPath = Path.Combine(targetDir, relative);
                var destinationFolder = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                File.Copy(file, destinationPath, true);
            }
        }

        private static void EnsureRenamedHostSupportFiles(string appDir, string appExePath, string outputExePath)
        {
            var sourceBase = Path.GetFileNameWithoutExtension(appExePath);
            var targetBase = Path.GetFileNameWithoutExtension(outputExePath);
            if (string.Equals(sourceBase, targetBase, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var outputDir = Path.GetDirectoryName(outputExePath) ?? appDir;

            var sourceRuntimeConfig = Path.Combine(appDir, sourceBase + ".runtimeconfig.json");
            var sourceDeps = Path.Combine(appDir, sourceBase + ".deps.json");

            if (File.Exists(sourceRuntimeConfig))
            {
                var targetRuntimeConfig = Path.Combine(outputDir, targetBase + ".runtimeconfig.json");
                File.Copy(sourceRuntimeConfig, targetRuntimeConfig, true);
            }

            if (File.Exists(sourceDeps))
            {
                var targetDeps = Path.Combine(outputDir, targetBase + ".deps.json");
                File.Copy(sourceDeps, targetDeps, true);
            }
        }

        private static string? ResolveAppExecutablePath(string appDir)
        {
            var exeCandidates = Directory
                .GetFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preferred = exeCandidates.FirstOrDefault(path =>
                Path.GetFileName(path).Equals("SevenZipManager.exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                return processPath;
            }

            return exeCandidates.FirstOrDefault();
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

            using var process = Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode != 0)
            {
                var error = process!.StandardError.ReadToEnd();
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

            using var process = Process.Start(psi);
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
