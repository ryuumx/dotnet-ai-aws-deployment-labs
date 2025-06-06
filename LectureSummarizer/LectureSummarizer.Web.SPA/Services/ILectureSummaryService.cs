using LectureSummarizer.Shared.Models;

namespace LectureSummarizer.Web.SPA.Services
{
    public interface ILectureSummaryService
    {
        Task<SummaryResponse> SummarizeLectureAsync(Stream fileStream, string fileName, string orientation = "portrait");
    }
}
