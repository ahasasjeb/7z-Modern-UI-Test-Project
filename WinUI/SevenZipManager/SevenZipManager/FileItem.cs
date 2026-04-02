namespace SevenZipManager
{
    public class FileItem
    {
        public string Name { get; set; } = "";
        public bool IsDirectory { get; set; }
        public string Icon { get; set; } = "\uE8A5";
        public string Type { get; set; } = "";
        public string SizeText { get; set; } = "";
        public string Modified { get; set; } = "";
        public string? FullPath { get; set; }
    }
}