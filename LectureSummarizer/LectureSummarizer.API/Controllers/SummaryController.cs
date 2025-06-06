using LectureSummarizer.API.Services;
using LectureSummarizer.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace LectureSummarizer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : ControllerBase
    {
        private readonly IPdfTextExtractor _pdfExtractor;
        private readonly IBedrockService _bedrockService;
        private readonly ILogger<SummaryController> _logger;

        public SummaryController(
            IPdfTextExtractor pdfExtractor,
            IBedrockService bedrockService,
            ILogger<SummaryController> logger)
        {
            _pdfExtractor = pdfExtractor;
            _bedrockService = bedrockService;
            _logger = logger;
        }

        [HttpPost("summarize")]
        public async Task<ActionResult<SummaryResponse>> SummarizeLecture(IFormFile file, [FromForm] string orientation = "portrait")
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new SummaryResponse
                    {
                        Success = false,
                        ErrorMessage = "No file uploaded."
                    });
                }

                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new SummaryResponse
                    {
                        Success = false,
                        ErrorMessage = "Only PDF files are supported."
                    });
                }

                // Convert PDF to images
                byte[] fileContent;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                var images = await _pdfExtractor.ConvertPdfToImagesAsync(fileContent, orientation);
                
                if (images == null || images.Count == 0)
                {
                    return BadRequest(new SummaryResponse
                    {
                        Success = false,
                        ErrorMessage = "Unable to convert PDF to images."
                    });
                }

                // Generate summary using Bedrock with images
                var summary = await _bedrockService.SummarizeLectureFromImagesAsync(images);

                return Ok(new SummaryResponse
                {
                    Success = true,
                    Summary = summary,
                    FileName = file.FileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing lecture summary request");
                return StatusCode(500, new SummaryResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing your request."
                });
            }
        }
    }
}
