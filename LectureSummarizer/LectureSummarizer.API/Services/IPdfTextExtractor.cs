namespace LectureSummarizer.API.Services
{
    public interface IPdfTextExtractor
    {
        Task<string> ExtractTextAsync(byte[] pdfContent);
    }
}