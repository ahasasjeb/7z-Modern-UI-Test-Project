using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevenZipManager
{
    public sealed partial class CompressDialog : ContentDialog
    {
        private bool _isInitializing;
        private readonly int _cpuCount = Environment.ProcessorCount;
        private ulong _autoSolidBytes = 1UL << 20;
        private bool _isAdjustingArchivePath;

        public CompressDialog(List<FileItem> selectedItems, string currentPath)
        {
            _isInitializing = true;
            InitializeComponent();

            var firstItem = selectedItems.FirstOrDefault();
            var baseName = firstItem != null ? Path.GetFileNameWithoutExtension(firstItem.Name) : "archive";
            ArchivePathBox.Text = Path.Combine(currentPath, baseName + ".7z");

            InitializeThreadOptions();
            ApplyFormatRules();
            UpdateSolidAutoValue();
            UpdateMemoryInfo();
            UpdateArchivePathExtension();
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

                var ext = GetOutputExtension();
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
                    DictionarySize = NormalizeSizeTo7z(GetComboText(DictionaryComboBox)),
                    WordSize = GetWordSize(),
                    ThreadCount = GetThreadCount(),
                    SolidArchive = IsSolidArchiveEnabled(),
                    SolidBlockSize = GetSolidBlockSize(),
                    EncryptionMethod = GetTagValue(EncryptionMethodComboBox, "aes256"),
                    EncryptHeaders = EncryptNamesCheckBox.IsChecked == true,
                    Password = Password,
                    VolumeSize = NormalizeSizeTo7z(VolumeSizeBox.Text),
                    DeleteSourceFiles = DeleteSourceCheckBox.IsChecked == true,
                    CreateSfx = CreateSfxCheckBox.IsChecked == true,
                    CompressSharedFiles = CompressSharedCheckBox.IsChecked == true,
                    AdditionalParameters = string.IsNullOrWhiteSpace(AdditionalParametersBox.Text) ? null : AdditionalParametersBox.Text.Trim()
                };
            }
        }

        private static string? GetComboText(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
            {
                return text.Trim();
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


        private string GetOutputExtension()
        {
            if (Format == "7z" && CreateSfxCheckBox.IsChecked == true)
            {
                return ".exe";
            }

            return GetExtensionForFormat(Format);
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

        private static string? NormalizeSizeTo7z(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var value = text.Trim().ToLowerInvariant();
            value = value.Replace(" ", string.Empty);
            value = value.Replace("kb", "k");
            value = value.Replace("mb", "m");
            value = value.Replace("gb", "g");
            value = value.Replace("tb", "t");

            return value;
        }

        private void InitializeThreadOptions()
        {
            ThreadCountComboBox.Items.Clear();
            ThreadCountComboBox.Items.Add(new ComboBoxItem { Content = "0" });
            for (var i = 1; i <= _cpuCount; i++)
            {
                ThreadCountComboBox.Items.Add(new ComboBoxItem { Content = i.ToString(CultureInfo.InvariantCulture) });
            }

            ThreadCountComboBox.SelectedIndex = 0;
            CpuThreadInfoText.Text = "/ " + _cpuCount.ToString(CultureInfo.InvariantCulture);
        }

        private int GetCompressionLevel()
        {
            if (LevelComboBox.SelectedItem is ComboBoxItem selected && selected.Content is string text)
            {
                var split = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 0 && int.TryParse(split[0], out var level))
                {
                    return level;
                }
            }

            return 5;
        }

        private string? GetMethod()
        {
            var text = GetComboText(MethodComboBox);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.ToLowerInvariant();
        }

        private int? GetWordSize()
        {
            var text = GetComboText(WordSizeComboBox);
            if (int.TryParse(text, out var value))
            {
                return value;
            }

            return null;
        }

        private int GetThreadCount()
        {
            if (ThreadCountComboBox.SelectedItem is ComboBoxItem selected && selected.Content is string text && int.TryParse(text, out var value))
            {
                return value;
            }

            return 0;
        }

        private bool IsSolidArchiveEnabled()
        {
            if (SolidBlockComboBox.SelectedIndex == 1)
            {
                return false;
            }

            var text = GetComboText(SolidBlockComboBox);
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            return text != "非固实";
        }

        private string? GetSolidBlockSize()
        {
            if (SolidBlockComboBox.SelectedIndex == 0)
            {
                return To7zSizeToken(_autoSolidBytes);
            }

            var text = GetComboText(SolidBlockComboBox);
            if (string.IsNullOrWhiteSpace(text) || text == "固实")
            {
                return null;
            }

            if (text == "非固实")
            {
                return "off";
            }

            return NormalizeSizeTo7z(text);
        }

        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            ApplyFormatRules();
            UpdateSolidAutoValue();
            UpdateMemoryInfo();
            UpdateArchivePathExtension();
        }

        private void ApplyFormatRules()
        {
            var is7z = Format == "7z";
            var isZip = Format == "zip";
            var supportsEncryption = is7z || isZip;

            DictionaryComboBox.IsEnabled = is7z;
            WordSizeComboBox.IsEnabled = is7z;
            SolidBlockComboBox.IsEnabled = is7z;
            CreateSfxCheckBox.IsEnabled = is7z;
            EncryptNamesCheckBox.IsEnabled = is7z;

            if (!is7z)
            {
                CreateSfxCheckBox.IsChecked = false;
            }

            if (is7z)
            {
                SetMethodItems(new[] { "LZMA2", "LZMA", "PPMd", "BZip2" }, "LZMA2");
            }
            else if (isZip)
            {
                SetMethodItems(new[] { "Deflate", "BZip2", "LZMA", "PPMd", "Copy" }, "Deflate");
            }
            else
            {
                SetMethodItems(new[] { "Copy" }, "Copy");
            }

            if (!supportsEncryption)
            {
                PasswordBox.Password = string.Empty;
                ConfirmPasswordBox.Password = string.Empty;
                EncryptNamesCheckBox.IsChecked = false;
                EncryptionMethodComboBox.SelectedIndex = 0;
                EncryptionMethodComboBox.IsEnabled = false;
            }
            else
            {
                EncryptionMethodComboBox.IsEnabled = true;
            }
        }

        private void SetMethodItems(IEnumerable<string> items, string preferred)
        {
            MethodComboBox.Items.Clear();
            foreach (var item in items)
            {
                MethodComboBox.Items.Add(new ComboBoxItem { Content = item });
            }

            var index = items.ToList().FindIndex(x => x == preferred);
            MethodComboBox.SelectedIndex = index >= 0 ? index : 0;
        }

        private void OnCompressionParameterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            UpdateSolidAutoValue();
            UpdateMemoryInfo();
        }

        private void OnCreateSfxChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            UpdateArchivePathExtension();
        }

        private void UpdateArchivePathExtension()
        {
            if (_isAdjustingArchivePath)
            {
                return;
            }

            try
            {
                _isAdjustingArchivePath = true;

                var current = ArchivePathBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(current))
                {
                    current = "archive";
                }

                ArchivePathBox.Text = Path.ChangeExtension(current, GetOutputExtension().TrimStart('.'));
            }
            finally
            {
                _isAdjustingArchivePath = false;
            }
        }

        private void UpdateSolidAutoValue()
        {
            _autoSolidBytes = CalculateAutoSolidBytes();

            if (SolidBlockComboBox.Items.Count > 0 && SolidBlockComboBox.Items[0] is ComboBoxItem item)
            {
                item.Content = "自动 (" + FormatBytes(_autoSolidBytes) + ")";
            }
        }

        private ulong CalculateAutoSolidBytes()
        {
            var dict = GetDictionaryBytesOrDefault();
            var is7z = Format == "7z";
            var method = GetMethod() ?? "lzma2";
            var chunkSize = GetLzma2ChunkSize(dict);

            var blockSize = chunkSize;
            if (is7z)
            {
                ulong maxSize = 1UL << 32;
                if (method == "lzma2")
                {
                    blockSize = chunkSize << 6;
                    maxSize = 1UL << 34;
                }
                else
                {
                    var dict2 = dict;
                    if (method == "bzip2")
                    {
                        dict2 /= 100000;
                        if (dict2 < 1)
                        {
                            dict2 = 1;
                        }

                        dict2 *= 100000;
                    }

                    blockSize = dict2 << 7;
                }

                const ulong minSize = 1UL << 24;
                if (blockSize < minSize)
                {
                    blockSize = minSize;
                }

                if (blockSize > maxSize)
                {
                    blockSize = maxSize;
                }
            }

            return blockSize;
        }

        private ulong GetDictionaryBytesOrDefault()
        {
            var dictText = NormalizeSizeTo7z(GetComboText(DictionaryComboBox));
            var dictBytes = ParseSizeToBytes(dictText);
            if (dictBytes == 0)
            {
                return 1UL << 25;
            }

            return dictBytes;
        }

        private static ulong GetLzma2ChunkSize(ulong dict)
        {
            var chunkSize = dict << 2;
            const ulong minSize = 1UL << 20;
            const ulong maxSize = 1UL << 28;

            if (chunkSize < minSize)
            {
                chunkSize = minSize;
            }

            if (chunkSize > maxSize)
            {
                chunkSize = maxSize;
            }

            if (chunkSize < dict)
            {
                chunkSize = dict;
            }

            chunkSize += minSize - 1;
            chunkSize &= ~(minSize - 1);
            return chunkSize;
        }

        private void UpdateMemoryInfo()
        {
            var usage = CalculateMemoryUsage();
            if (!usage.IsKnown)
            {
                CompressionMemoryText.Text = "压缩所需内存: -";
                DecompressionMemoryText.Text = "解压所需内存: -";
                return;
            }

            CompressionMemoryText.Text = "压缩所需内存: " + FormatBytes(usage.CompressMemory);
            DecompressionMemoryText.Text = "解压所需内存: " + FormatBytes(usage.DecompressMemory);
        }

        private (ulong CompressMemory, ulong DecompressMemory, bool IsKnown) CalculateMemoryUsage()
        {
            var level = GetCompressionLevel();
            var format = Format;
            var method = GetMethod() ?? "copy";

            if (level == 0)
            {
                const ulong mem = 1UL << 20;
                return (mem, mem, true);
            }

            var numThreads = GetEffectiveThreadCount();
            var size = 0UL;
            uint numMainZipThreads = 1;

            if (format == "zip")
            {
                uint numSubThreads = 1;
                if (method == "lzma" && numThreads > 1 && level >= 5)
                {
                    numSubThreads = 2;
                }

                numMainZipThreads = numThreads / numSubThreads;
                if (numMainZipThreads > 1)
                {
                    size += (ulong)numMainZipThreads * ((ulong)IntPtr.Size << 23);
                }
                else
                {
                    numMainZipThreads = 1;
                }
            }

            var dict64 = GetDictionaryBytesOrDefault();

            switch (method)
            {
                case "lzma":
                case "lzma2":
                {
                    var dict = dict64 >= uint.MaxValue ? uint.MaxValue : (uint)dict64;
                    if (dict == 0)
                    {
                        dict = 1;
                    }

                    var hs = dict - 1;
                    hs |= hs >> 1;
                    hs |= hs >> 2;
                    hs |= hs >> 4;
                    hs |= hs >> 8;
                    hs >>= 1;
                    if (hs >= (1U << 24))
                    {
                        hs >>= 1;
                    }

                    hs |= (1U << 16) - 1;
                    if (level < 5)
                    {
                        hs |= (256U << 10) - 1;
                    }

                    hs++;

                    var size1 = (ulong)hs * 4;
                    size1 += (ulong)dict * 4;
                    if (level >= 5)
                    {
                        size1 += (ulong)dict * 4;
                    }

                    size1 += 2UL << 20;

                    uint numThreads1 = 1;
                    if (numThreads > 1 && level >= 5)
                    {
                        size1 += (2UL << 20) + (4UL << 20);
                        numThreads1 = 2;
                    }

                    var numBlockThreads = numThreads / numThreads1;
                    if (numBlockThreads == 0)
                    {
                        numBlockThreads = 1;
                    }

                    ulong chunkSize = 0;
                    if (method != "lzma" && numBlockThreads != 1)
                    {
                        chunkSize = GetLzma2ChunkSize(dict);
                    }

                    if (chunkSize == 0)
                    {
                        const ulong blockSizeMax = 0xFFFF0000UL;
                        var blockSize = (ulong)dict + (1UL << 16) + (numThreads1 > 1 ? (1UL << 20) : 0);
                        blockSize += blockSize >> (blockSize < (1UL << 30) ? 1 : 2);
                        if (blockSize >= blockSizeMax)
                        {
                            blockSize = blockSizeMax;
                        }

                        size += numBlockThreads * (size1 + blockSize);
                    }
                    else
                    {
                        size += numBlockThreads * (size1 + chunkSize);
                        var numPackChunks = numBlockThreads + (numBlockThreads / 8) + 1;
                        size += numPackChunks * chunkSize;
                    }

                    var decomp = (ulong)dict + (2UL << 20);
                    return (size, decomp, true);
                }

                case "ppmd":
                {
                    var decomp = dict64 + (2UL << 20);
                    return (size + decomp, decomp, true);
                }

                case "deflate":
                {
                    const ulong size1 = 4UL << 20;
                    size += size1 * numMainZipThreads;
                    const ulong decomp = 2UL << 20;
                    return (size, decomp, true);
                }

                case "bzip2":
                {
                    const ulong decomp = 7UL << 20;
                    const ulong memForOneThread = 10UL << 20;
                    return (size + memForOneThread * numThreads, decomp, true);
                }

                case "copy":
                {
                    const ulong mem = 1UL << 20;
                    return (mem, mem, true);
                }
            }

            return (0, 0, false);
        }

        private uint GetEffectiveThreadCount()
        {
            var selected = GetThreadCount();
            if (selected <= 0)
            {
                return (uint)Math.Max(1, _cpuCount);
            }

            return (uint)selected;
        }

        private static ulong ParseSizeToBytes(string? size)
        {
            if (string.IsNullOrWhiteSpace(size))
            {
                return 0;
            }

            var value = size.Trim().ToLowerInvariant();
            if (value.EndsWith("k") && ulong.TryParse(value[..^1], out var kb))
            {
                return kb * 1024UL;
            }

            if (value.EndsWith("m") && ulong.TryParse(value[..^1], out var mb))
            {
                return mb * 1024UL * 1024UL;
            }

            if (value.EndsWith("g") && ulong.TryParse(value[..^1], out var gb))
            {
                return gb * 1024UL * 1024UL * 1024UL;
            }

            if (value.EndsWith("t") && ulong.TryParse(value[..^1], out var tb))
            {
                return tb * 1024UL * 1024UL * 1024UL * 1024UL;
            }

            return ulong.TryParse(value, out var bytes) ? bytes : 0;
        }

        private static string To7zSizeToken(ulong bytes)
        {
            if (bytes == 0)
            {
                return "0";
            }

            if (bytes % (1UL << 30) == 0)
            {
                return (bytes >> 30).ToString(CultureInfo.InvariantCulture) + "g";
            }

            if (bytes % (1UL << 20) == 0)
            {
                return (bytes >> 20).ToString(CultureInfo.InvariantCulture) + "m";
            }

            if (bytes % (1UL << 10) == 0)
            {
                return (bytes >> 10).ToString(CultureInfo.InvariantCulture) + "k";
            }

            return bytes.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("F0", CultureInfo.InvariantCulture) + " KB";
            }

            if (bytes < 1024L * 1024 * 1024)
            {
                return (bytes / (1024.0 * 1024)).ToString("F0", CultureInfo.InvariantCulture) + " MB";
            }

            return (bytes / (1024.0 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture) + " GB";
        }

        private void OnShowPasswordChanged(object sender, RoutedEventArgs e)
        {
            var mode = ShowPasswordCheckBox.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            PasswordBox.PasswordRevealMode = mode;
            ConfirmPasswordBox.PasswordRevealMode = mode;
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, App.WindowHandle);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var fileName = Path.GetFileName(OutputPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = "archive" + GetOutputExtension();
                }

                ArchivePathBox.Text = Path.Combine(folder.Path, fileName);
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(Password))
            {
                return;
            }

            if (Password != ConfirmPasswordBox.Password)
            {
                args.Cancel = true;
            }
        }
    }
}
