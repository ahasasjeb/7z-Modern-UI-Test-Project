using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevenZipManager
{
    public sealed partial class SfxWindow : Window
    {
        private readonly string _archivePath;
        private readonly SevenZipService _service = new();

        public SfxWindow(string archivePath)
        {
            _archivePath = archivePath;
            InitializeComponent();

            var initialDir = Path.GetDirectoryName(archivePath);
            OutputPathBox.Text = string.IsNullOrWhiteSpace(initialDir)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : initialDir;
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, App.WindowHandle);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                OutputPathBox.Text = folder.Path;
            }
        }

        private async void OnExtractClick(object sender, RoutedEventArgs e)
        {
            var outputPath = OutputPathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                await ShowErrorAsync("请输入解压路径");
                return;
            }

            ExtractButton.IsEnabled = false;
            try
            {
                await Task.Run(() => _service.Extract(_archivePath, outputPath, string.Empty));
                Close();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("解压失败: " + ex.Message);
            }
            finally
            {
                ExtractButton.IsEnabled = true;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task ShowErrorAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
