namespace LectureSummarizer.Shared.Models
{
    public class SummaryRequest
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
    }
}