using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevenZipManager
{
    public sealed partial class CompressDialog : ContentDialog
    {
        private bool _isInitializing;

        public CompressDialog(List<FileItem> selectedItems, string currentPath)
        {
            _isInitializing = true;
            InitializeComponent();
            
            var firstItem = selectedItems.FirstOrDefault();
            if (firstItem != null)
            {
                var baseName = firstItem.Name;
                ArchivePathBox.Text = Path.Combine(currentPath, Path.GetFileNameWithoutExtension(baseName) + ".7z");
            }

            if (string.IsNullOrWhiteSpace(ArchivePathBox.Text))
            {
                ArchivePathBox.Text = Path.Combine(currentPath, "archive.7z");
            }

            ApplyFormatRules();
            _isInitializing = false;
        }

        public string Format
        {
            get
            {
                if (FormatComboBox.SelectedItem is ComboBoxItem selected && selected.Content is string text)
                {
                    return text.ToLowerInvariant();
                }

                return "7z";
            }
        }

        public string OutputPath
        {
            get
            {
                var current = ArchivePathBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(current))
                {
                    current = "archive";
                }

                var ext = GetExtensionForFormat(Format);
                if (!current.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    current = Path.ChangeExtension(current, ext.TrimStart('.'));
                }

                return current;
            }
        }

        public string Password => PasswordBox.Password;

        public CompressionOptions Options
        {
            get
            {
                return new CompressionOptions
                {
                    UpdateMode = GetTagValue(UpdateModeComboBox, "add"),
                    PathMode = GetTagValue(PathModeComboBox, "relative"),
                    Format = Format,
                    CompressionLevel = GetCompressionLevel(),
                    Method = GetMethod(),
                    DictionarySize = GetComboText(DictionaryComboBox),
                    WordSize = GetWordSize(),
                    ThreadCount = (int)Math.Max(0, ThreadCountBox.Value),
                    SolidArchive = SolidArchiveCheckBox.IsChecked == true,
                    SolidBlockSize = GetSolidBlockSize(),
                    EncryptionMethod = GetTagValue(EncryptionMethodComboBox, "aes256"),
                    EncryptHeaders = EncryptNamesCheckBox.IsChecked == true,
                    Password = Password,
                    VolumeSize = string.IsNullOrWhiteSpace(VolumeSizeBox.Text) ? null : VolumeSizeBox.Text.Trim(),
                    DeleteSourceFiles = DeleteSourceCheckBox.IsChecked == true
                };
            }
        }

        private static string GetExtensionForFormat(string format)
        {
            return format switch
            {
                "gzip" => ".gz",
                "bzip2" => ".bz2",
                _ => $".{format}"
            };
        }

        private static string? GetComboText(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
            {
                if (text == "默认")
                {
                    return null;
                }

                return text.ToLowerInvariant();
            }

            return null;
        }

        private static string GetTagValue(ComboBox comboBox, string fallback)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag;
            }

            return fallback;
        }

        private int GetCompressionLevel()
        {
            return LevelComboBox.SelectedIndex switch
            {
                0 => 1,
                1 => 3,
                2 => 5,
                3 => 7,
                4 => 9,
                _ => 5
            };
        }

        private string? GetMethod()
        {
            if (MethodComboBox.SelectedItem is not ComboBoxItem selected || selected.Content is not string value)
            {
                return null;
            }

            if (value == "默认")
            {
                return null;
            }

            return value.ToLowerInvariant();
        }

        private int? GetWordSize()
        {
            if (WordSizeComboBox.SelectedItem is not ComboBoxItem selected || selected.Content is not string text || text == "默认")
            {
                return null;
            }

            if (int.TryParse(text, out var value))
            {
                return value;
            }

            return null;
        }

        private string? GetSolidBlockSize()
        {
            if (SolidBlockComboBox.SelectedItem is not ComboBoxItem selected || selected.Content is not string text)
            {
                return null;
            }

            if (text == "自动")
            {
                return null;
            }

            if (text == "关闭")
            {
                return "off";
            }

            return text.ToLowerInvariant();
        }

        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ArchivePathBox == null)
            {
                return;
            }

            ApplyFormatRules();
            var currentPath = ArchivePathBox.Text;
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                currentPath = "archive";
            }

            ArchivePathBox.Text = Path.ChangeExtension(currentPath, GetExtensionForFormat(Format).TrimStart('.'));
        }

        private void ApplyFormatRules()
        {
            var is7z = Format == "7z";
            var isZip = Format == "zip";
            var supportsAdvancedMethod = is7z || isZip;

            MethodComboBox.IsEnabled = supportsAdvancedMethod;
            DictionaryComboBox.IsEnabled = is7z;
            WordSizeComboBox.IsEnabled = is7z;
            SolidBlockComboBox.IsEnabled = is7z;
            SolidArchiveCheckBox.IsEnabled = is7z;
            EncryptNamesCheckBox.IsEnabled = is7z;

            if (!is7z)
            {
                EncryptNamesCheckBox.IsChecked = false;
                SolidArchiveCheckBox.IsChecked = false;
            }

            if (!supportsAdvancedMethod)
            {
                MethodComboBox.SelectedIndex = 0;
            }
        }

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
                var fileName = Path.GetFileNameWithoutExtension(OutputPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = "archive";
                }

                ArchivePathBox.Text = Path.Combine(folder.Path, fileName + GetExtensionForFormat(Format));
            }
        }
    }
}
