namespace LectureSummarizer.Shared.Models
{
    public class SummaryResponse
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}