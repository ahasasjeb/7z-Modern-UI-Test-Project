namespace SevenZipManager
{
    public class CompressionOptions
    {
        public string UpdateMode { get; set; } = "add";
        public string PathMode { get; set; } = "relative";
        public string Format { get; set; } = "7z";
        public int CompressionLevel { get; set; } = 5;
        public string? Method { get; set; }
        public string? DictionarySize { get; set; }
        public int? WordSize { get; set; }
        public int ThreadCount { get; set; } = 0;
        public bool SolidArchive { get; set; } = true;
        public string? SolidBlockSize { get; set; }
        public string EncryptionMethod { get; set; } = "aes256";
        public bool EncryptHeaders { get; set; }
        public string Password { get; set; } = "";
        public string? VolumeSize { get; set; }
        public bool DeleteSourceFiles { get; set; }
    }
}