using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;

namespace LectureSummarizer.API.Services
{
    public class PdfTextExtractor : IPdfTextExtractor
    {
        public async Task<string> ExtractTextAsync(byte[] pdfContent)
        {
            return await Task.Run(() =>
            {
                var text = new StringBuilder();
                
                using (var reader = new PdfReader(pdfContent))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                    }
                }
                
                return text.ToString();
            });
        }
    }
}