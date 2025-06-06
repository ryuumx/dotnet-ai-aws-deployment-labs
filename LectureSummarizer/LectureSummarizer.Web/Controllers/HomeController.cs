using LectureSummarizer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace LectureSummarizer.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SummarizeLecture(IFormFile file, string orientation = "portrait")
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Please select a PDF file to upload.";
                return View("Index");
            }

            try
            {
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:5131";
                
                using var formContent = new MultipartFormDataContent();
                using var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                formContent.Add(fileContent, "file", file.FileName);
                formContent.Add(new StringContent(orientation), "orientation");

                var response = await _httpClient.PostAsync($"{apiBaseUrl}/api/summary/summarize", formContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var summary = JsonSerializer.Deserialize<SummaryResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    ViewBag.Summary = summary;
                }
                else
                {
                    ViewBag.Error = "Failed to process the PDF. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"An error occurred: {ex.Message}";
            }

            return View("Index");
        }
    }
}
