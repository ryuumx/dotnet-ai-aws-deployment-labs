using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;

namespace LectureSummarizer.API.Services
{
    public class BedrockService : IBedrockService
    {
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private const string ModelId = "anthropic.claude-3-5-sonnet-20241022-v2:0";

        public BedrockService(IAmazonBedrockRuntime bedrockClient)
        {
            _bedrockClient = bedrockClient;
        }

        public async Task<string> SummarizeLectureAsync(string lectureText)
        {
            var prompt = $@"Please provide a comprehensive summary of this lecture. Focus on:
1. Main topics and key concepts
2. Important points and takeaways
3. Any conclusions or recommendations

Lecture content:
{lectureText}

Please provide a well-structured summary in bullet points or short paragraphs:";

            var request = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 1000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var requestBody = Encoding.UTF8.GetBytes(jsonRequest);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = ModelId,
                Body = new MemoryStream(requestBody),
                ContentType = "application/json"
            };

            var response = await _bedrockClient.InvokeModelAsync(invokeRequest);
            
            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();
            
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var content = jsonResponse.GetProperty("content")[0].GetProperty("text").GetString();
            
            return content ?? "Unable to generate summary.";
        }

        public async Task<string> SummarizeLectureFromImagesAsync(List<byte[]> images)
        {
            var contentItems = new List<object>();
            
            // Add the prompt text
            contentItems.Add(new
            {
                type = "text",
                text = @"Please analyze these lecture slides and provide a comprehensive summary. Focus on:
1. Main topics and key concepts
2. Important points and takeaways
3. Any conclusions or recommendations

Please provide a well-structured summary in bullet points or short paragraphs:"
            });

            // Add each image
            foreach (var image in images)
            {
                contentItems.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = "image/jpeg",
                        data = Convert.ToBase64String(image)
                    }
                });
            }

            var request = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 2000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentItems.ToArray()
                    }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var requestBody = Encoding.UTF8.GetBytes(jsonRequest);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = ModelId,
                Body = new MemoryStream(requestBody),
                ContentType = "application/json"
            };

            var response = await _bedrockClient.InvokeModelAsync(invokeRequest);
            
            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();
            
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var content = jsonResponse.GetProperty("content")[0].GetProperty("text").GetString();
            
            return content ?? "Unable to generate summary.";
        }
    }
}
