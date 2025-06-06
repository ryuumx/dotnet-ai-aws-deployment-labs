namespace LectureSummarizer.API.Services
{
    public interface IPdfTextExtractor
    {
        Task<string> ExtractTextAsync(byte[] pdfContent);
        Task<List<byte[]>> ConvertPdfToImagesAsync(byte[] pdfContent, string orientation = "portrait");
    }
}
