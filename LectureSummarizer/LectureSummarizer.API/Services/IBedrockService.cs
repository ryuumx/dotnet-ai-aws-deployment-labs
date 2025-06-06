namespace LectureSummarizer.API.Services
{
    public interface IBedrockService
    {
        Task<string> SummarizeLectureAsync(string lectureText);
        Task<string> SummarizeLectureFromImagesAsync(List<byte[]> images);
    }
}
