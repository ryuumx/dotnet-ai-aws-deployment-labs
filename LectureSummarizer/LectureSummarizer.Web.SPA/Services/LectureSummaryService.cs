using LectureSummarizer.Shared.Models;
using System.Text.Json;

namespace LectureSummarizer.Web.SPA.Services
{
    public class LectureSummaryService : ILectureSummaryService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public LectureSummaryService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<SummaryResponse> SummarizeLectureAsync(Stream fileStream, string fileName, string orientation = "portrait")
        {
            try
            {
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:5131";
                
                using var formContent = new MultipartFormDataContent();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                formContent.Add(streamContent, "file", fileName);
                formContent.Add(new StringContent(orientation), "orientation");

                var response = await _httpClient.PostAsync($"{apiBaseUrl}/api/summary/summarize", formContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var summary = JsonSerializer.Deserialize<SummaryResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return summary ?? new SummaryResponse 
                    { 
                        Success = false, 
                        ErrorMessage = "Failed to parse response." 
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new SummaryResponse
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode} - {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new SummaryResponse
                {
                    Success = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
        }
    }
}
