using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SevenZipManager.ViewModels;

namespace SevenZipManager
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            ViewModel = new MainViewModel(new SevenZipService());
            InitializeComponent();
            FileListView.ItemsSource = ViewModel.Items;
            Activated += OnWindowActivated;
        }

        private async void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            Activated -= OnWindowActivated;
            await ViewModel.InitializeAsync();
        }

        private async void OnFileDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem item)
            {
                await ViewModel.OpenItemAsync(item);
            }
        }

        private async void OnParentFolderClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigateParentAsync();
        }

        private async void OnPathQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            await ViewModel.NavigateToPathAsync(args.QueryText);
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshAsync();
        }

        private async void OnCompressClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                await ShowInfoDialogAsync("提示", "请先选择要压缩的文件或文件夹");
                return;
            }

            var dialog = new CompressDialog(selectedItems, ViewModel.CurrentPath)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var inputPaths = selectedItems
                    .Select(i => i.FullPath ?? Path.Combine(ViewModel.CurrentPath, i.Name))
                    .ToList();
                await ViewModel.CompressAsync(dialog.OutputPath, inputPaths, dialog.Format, dialog.Password);
            }
        }

        private async void OnExtractClick(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not FileItem item || item.IsDirectory)
            {
                await ShowInfoDialogAsync("提示", "请选择要解压的压缩文件");
                return;
            }

            var sourceArchive = item.FullPath ?? Path.Combine(ViewModel.CurrentPath, item.Name);
            var dialog = new ExtractDialog(sourceArchive)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.ExtractAsync(sourceArchive, dialog.OutputPath, dialog.Password);
            }
        }

        private async void OnExtractHereClick(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not FileItem item || item.IsDirectory)
            {
                return;
            }

            var sourceArchive = item.FullPath ?? Path.Combine(ViewModel.CurrentPath, item.Name);
            await ViewModel.ExtractAsync(sourceArchive, ViewModel.CurrentPath, "", refreshAfter: true);
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                return;
            }

            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            var storageItems = new List<Windows.Storage.IStorageItem>();
            foreach (var item in selectedItems)
            {
                var path = item.FullPath ?? Path.Combine(ViewModel.CurrentPath, item.Name);
                try
                {
                    if (item.IsDirectory && Directory.Exists(path))
                    {
                        storageItems.Add(Windows.Storage.StorageFolder.GetFolderFromPathAsync(path).AsTask().Result);
                    }
                    else if (File.Exists(path))
                    {
                        storageItems.Add(Windows.Storage.StorageFile.GetFileFromPathAsync(path).AsTask().Result);
                    }
                }
                catch
                {
                }
            }

            if (storageItems.Count > 0)
            {
                dataPackage.SetStorageItems(storageItems);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ViewModel.StatusText = $"已复制 {storageItems.Count} 个项目";
            }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除选中的 {selectedItems.Count} 个项目吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await ViewModel.DeleteAsync(selectedItems);
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = $"删除失败: {ex.Message}";
            }
        }

        private async void OnMoveClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "移动到",
                PrimaryButtonText = "移动",
                CloseButtonText = "取消",
                XamlRoot = Content.XamlRoot
            };

            var inputBox = new TextBox { PlaceholderText = "目标路径" };
            dialog.Content = inputBox;
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var targetPath = inputBox.Text.Trim();
            if (!Directory.Exists(targetPath))
            {
                ViewModel.StatusText = "目标路径不存在";
                return;
            }

            try
            {
                await ViewModel.MoveAsync(selectedItems, targetPath);
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = $"移动失败: {ex.Message}";
            }
        }

        private async void OnRenameClick(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not FileItem item)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "重命名",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = Content.XamlRoot
            };

            var inputBox = new TextBox { Text = item.Name };
            dialog.Content = inputBox;
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var newName = inputBox.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                return;
            }

            try
            {
                await ViewModel.RenameAsync(item, newName);
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = $"重命名失败: {ex.Message}";
            }
        }

        private async void OnPropertiesClick(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not FileItem item)
            {
                return;
            }

            var path = item.FullPath ?? Path.Combine(ViewModel.CurrentPath, item.Name);
            var info = string.Empty;

            if (item.IsDirectory && Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                info = $"名称: {item.Name}\n类型: 文件夹\n位置: {path}\n创建时间: {dirInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n修改时间: {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            }
            else if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                info = $"名称: {item.Name}\n类型: {item.Type}\n大小: {item.SizeText}\n位置: {path}\n创建时间: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n修改时间: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            }

            var dialog = new ContentDialog
            {
                Title = "属性",
                Content = new TextBlock { Text = info, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
                CloseButtonText = "关闭",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            ViewModel.StatusText = "搜索功能开发中...";
        }

        private async void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog { XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem item)
            {
                await ViewModel.OpenItemAsync(item);
            }
        }

        private List<FileItem> GetSelectedItems()
        {
            return FileListView.SelectedItems.Cast<FileItem>().ToList();
        }

        private async System.Threading.Tasks.Task ShowInfoDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
