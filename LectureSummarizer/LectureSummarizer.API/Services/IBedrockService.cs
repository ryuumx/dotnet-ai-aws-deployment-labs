namespace LectureSummarizer.API.Services
{
    public interface IBedrockService
    {
        Task<string> SummarizeLectureAsync(string lectureText);
    }
}