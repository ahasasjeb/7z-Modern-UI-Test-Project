using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevenZipManager
{
    public sealed partial class CompressDialog : ContentDialog
    {
        public CompressDialog(List<FileItem> selectedItems, string currentPath)
        {
            InitializeComponent();
            
            var firstItem = selectedItems.FirstOrDefault();
            if (firstItem != null)
            {
                var baseName = firstItem.Name;
                FileNameBox.Text = Path.GetFileNameWithoutExtension(baseName);
            }
            OutputPathBox.Text = currentPath;
        }

        public string Format
        {
            get
            {
                return ((ComboBoxItem)FormatComboBox.SelectedItem).Content.ToString()?.ToLowerInvariant() ?? "7z";
            }
        }

        public string OutputPath
        {
            get
            {
                var fileName = FileNameBox.Text.Trim();
                var ext = Format;
                if (!fileName.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += $".{ext}";
                }
                return Path.Combine(OutputPathBox.Text.Trim(), fileName);
            }
        }

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
