using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SevenZipManager.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly SevenZipService _zipService;
        private CancellationTokenSource? _loadCts;

        private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private string _statusText = "就绪";
        private string _itemCountText = "0 个项目";
        private bool _isBusy;
        private double _progressPercent;

        public MainViewModel(SevenZipService zipService)
        {
            _zipService = zipService;
        }

        public ObservableCollection<FileItem> Items { get; } = new();

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ItemCountText
        {
            get => _itemCountText;
            set => SetProperty(ref _itemCountText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public Task InitializeAsync()
        {
            return NavigateToPathAsync(CurrentPath);
        }

        public async Task NavigateToPathAsync(string path)
        {
            path = path.Trim();
            if (!Directory.Exists(path))
            {
                StatusText = $"路径不存在: {path}";
                return;
            }

            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsBusy = true;
            StatusText = "正在加载目录...";

            try
            {
                var data = await Task.Run(() => LoadItems(path, token), token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                CurrentPath = path;

                Items.Clear();
                foreach (var item in data)
                {
                    Items.Add(item);
                }

                ItemCountText = $"{Items.Count} 个项目";
                StatusText = $"就绪 - {path}";
            }
            catch (OperationCanceledException)
            {
                StatusText = "目录加载已取消";
            }
            catch (Exception ex)
            {
                StatusText = $"加载失败: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task RefreshAsync()
        {
            return NavigateToPathAsync(CurrentPath);
        }

        public async Task NavigateParentAsync()
        {
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                await NavigateToPathAsync(parent.FullName);
            }
        }

        public async Task OpenItemAsync(FileItem item)
        {
            if (item.IsDirectory)
            {
                var nextPath = item.FullPath ?? Path.Combine(CurrentPath, item.Name);
                await NavigateToPathAsync(nextPath);
                return;
            }

            var filePath = item.FullPath ?? Path.Combine(CurrentPath, item.Name);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"打开失败: {ex.Message}";
            }
        }

        public async Task CompressAsync(string outputPath, List<string> inputPaths, string format, string password)
        {
            IsBusy = true;
            ProgressPercent = 0;
            StatusText = "开始压缩...";

            try
            {
                await Task.Run(() => _zipService.Compress(outputPath, inputPaths, format, password));
                ProgressPercent = 100;
                StatusText = "压缩完成";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"压缩失败: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ExtractAsync(string archivePath, string outputPath, string password, bool refreshAfter = false)
        {
            IsBusy = true;
            ProgressPercent = 0;
            StatusText = "开始解压...";

            try
            {
                await Task.Run(() => _zipService.Extract(archivePath, outputPath, password));
                ProgressPercent = 100;
                StatusText = "解压完成";

                if (refreshAfter)
                {
                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"解压失败: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task DeleteAsync(IReadOnlyList<FileItem> selectedItems)
        {
            foreach (var item in selectedItems)
            {
                var path = item.FullPath ?? Path.Combine(CurrentPath, item.Name);
                if (item.IsDirectory)
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.Delete(path);
                }
            }

            StatusText = $"已删除 {selectedItems.Count} 个项目";
            await RefreshAsync();
        }

        public async Task MoveAsync(IReadOnlyList<FileItem> selectedItems, string targetPath)
        {
            foreach (var item in selectedItems)
            {
                var srcPath = item.FullPath ?? Path.Combine(CurrentPath, item.Name);
                var destPath = Path.Combine(targetPath, item.Name);

                if (item.IsDirectory)
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(srcPath, destPath);
                }
                else
                {
                    File.Move(srcPath, destPath);
                }
            }

            StatusText = $"已移动 {selectedItems.Count} 个项目";
            await RefreshAsync();
        }

        public async Task RenameAsync(FileItem item, string newName)
        {
            var oldPath = item.FullPath ?? Path.Combine(CurrentPath, item.Name);
            var newPath = Path.Combine(CurrentPath, newName);

            if (item.IsDirectory)
            {
                Directory.Move(oldPath, newPath);
            }
            else
            {
                File.Move(oldPath, newPath);
            }

            await RefreshAsync();
        }

        private static List<FileItem> LoadItems(string path, CancellationToken token)
        {
            var list = new List<FileItem>();

            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();

                var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
                list.Add(new FileItem
                {
                    Name = name,
                    IsDirectory = true,
                    Icon = "\uE8B7",
                    Type = "文件夹",
                    SizeText = "",
                    Modified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm"),
                    FullPath = dir
                });
            }

            foreach (var file in Directory.GetFiles(path).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();

                var name = Path.GetFileName(file);
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var info = new FileInfo(file);
                list.Add(new FileItem
                {
                    Name = name,
                    IsDirectory = false,
                    Icon = GetFileIcon(ext),
                    Type = GetFileType(ext),
                    SizeText = FormatSize(info.Length),
                    Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    FullPath = file
                });
            }

            return list;
        }

        private static string GetFileType(string ext) => ext switch
        {
            ".7z" or ".zip" or ".rar" or ".tar" or ".gz" or ".bz2" or ".xz" or ".zst" => "压缩文件",
            ".txt" or ".log" or ".md" => "文本文件",
            ".exe" => "应用程序",
            ".dll" => "动态链接库",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".ico" => "图像文件",
            ".mp3" or ".wav" or ".flac" or ".aac" => "音频文件",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "视频文件",
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "Office 文档",
            ".pdf" => "PDF 文档",
            ".cpp" or ".c" or ".h" or ".cs" or ".py" or ".js" or ".ts" => "源代码文件",
            ".htm" or ".html" or ".css" => "网页文件",
            _ => $"{ext.TrimStart('.').ToUpperInvariant()} 文件"
        };

        private static string GetFileIcon(string ext) => ext switch
        {
            ".7z" or ".zip" or ".rar" or ".tar" or ".gz" or ".bz2" or ".xz" => "\uE8AC",
            ".exe" => "\uECAA",
            ".dll" => "\uE950",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "\uE91B",
            ".txt" or ".log" or ".md" => "\uE8A5",
            ".pdf" => "\uE8A5",
            ".mp3" or ".wav" or ".flac" => "\uC36A",
            ".mp4" or ".avi" or ".mkv" => "\uE8B2",
            ".doc" or ".docx" or ".xls" or ".xlsx" => "\uE8A5",
            ".cpp" or ".c" or ".h" or ".cs" => "\uE943",
            ".htm" or ".html" => "\uE774",
            _ => "\uE8A5"
        };

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            if (bytes < 1024 * 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024):F1} MB";
            }

            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}