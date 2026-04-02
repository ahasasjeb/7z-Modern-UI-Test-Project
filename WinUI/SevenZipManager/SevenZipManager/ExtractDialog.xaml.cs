using System.IO;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevenZipManager
{
    public sealed partial class ExtractDialog : ContentDialog
    {
        public ExtractDialog(string sourceFile)
        {
            InitializeComponent();
            SourceFileBox.Text = sourceFile;
            var dir = Path.GetDirectoryName(sourceFile) ?? "";
            var name = Path.GetFileNameWithoutExtension(sourceFile);
            OutputPathBox.Text = Path.Combine(dir, name);
        }

        public string OutputPath => OutputPathBox.Text.Trim();
        public string Password => PasswordBox.Password;

        private async void OnBrowseClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            var windowHandle = App.WindowHandle;
            InitializeWithWindow.Initialize(picker, windowHandle);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                OutputPathBox.Text = folder.Path;
            }
        }
    }
}
