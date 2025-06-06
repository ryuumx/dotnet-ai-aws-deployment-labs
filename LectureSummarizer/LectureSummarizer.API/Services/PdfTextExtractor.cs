using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LectureSummarizer.API.Services
{
    public class PdfTextExtractor : IPdfTextExtractor
    {
        public async Task<string> ExtractTextAsync(byte[] pdfContent)
        {
            // This method now returns a placeholder since we'll be using images
            // The actual processing will be done by converting PDF to images
            return await Task.FromResult("PDF converted to images for processing");
        }

        public async Task<List<byte[]>> ConvertPdfToImagesAsync(byte[] pdfContent, string orientation = "portrait")
        {
            return await Task.Run(async () =>
            {
                var images = new List<byte[]>();
                const int maxImages = 10; // Limit to 10 pages for better performance and API compliance
                
                try
                {
                    using var library = DocLib.Instance;

                    // Use a high resolution that works for both orientations
                    var maxDimension = 1200; 
                    using var tempdocReader = library.GetDocReader(pdfContent, new PageDimensions(maxDimension, maxDimension));
                    var tempPageReader = tempdocReader.GetPageReader(0);
                    var width = tempPageReader.GetPageWidth();
                    var height = tempPageReader.GetPageHeight();

                    using var docReader = library.GetDocReader(pdfContent, new PageDimensions(Math.Min(width, height), Math.Max(width, height)));
                    for (int i = 0; i < docReader.GetPageCount() && i < maxImages; i++)
                    {
                        using var pageReader = docReader.GetPageReader(i);
                        var rawBytes = pageReader.GetImage();

                        var actualBytes = rawBytes.Length;
                        if (actualBytes == 0)
                        {
                            Console.WriteLine($"Page {i + 1}: No data, skipping");
                            continue;
                        }

                        // Calculate stride (bytes per row including padding)
                        var stride = actualBytes / height;
                        var bytesPerPixel = stride / width;

                        Image? image = null;

                        // If there's padding, we need to remove it
                        if (stride != width * 4) // Assuming BGRA32
                        {
                            var cleanBytes = new byte[width * height * 4];
                            for (int y = 0; y < height; y++)
                            {
                                var sourceOffset = y * stride;
                                var destOffset = y * width * 4;
                                Array.Copy(rawBytes, sourceOffset, cleanBytes, destOffset, width * 4);
                            }

                            image = Image.LoadPixelData<Bgra32>(cleanBytes, width, height);
                        }
                        else
                        {
                            image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
                        }

                        using var ms = new MemoryStream();
                        await image.SaveAsJpegAsync(ms);
                        var jpegBytes = ms.ToArray();

                        images.Add(jpegBytes);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening PDF: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    
                    // Create a single fallback image
                    var fallbackImage = await CreateFallbackImageAsync(1);
                    if (fallbackImage.Length > 0)
                    {
                        images.Add(fallbackImage);
                    }
                }
                
                Console.WriteLine($"Total images generated: {images.Count}");
                return images;
            });
        }

        private async Task<byte[]> CreateFallbackImageAsync(int pageNumber, int width = 800, int height = 600)
        {
            try
            {
                
                using var image = new Image<Rgba32>(width, height);
                
                // Create a simple white image with some basic content
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (y < 50 || y > height - 50 || x < 50 || x > width - 50)
                        {
                            image[x, y] = new Rgba32(200, 200, 200, 255); // Light gray border
                        }
                        else
                        {
                            image[x, y] = new Rgba32(255, 255, 255, 255); // White background
                        }
                    }
                }
                
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating fallback image: {ex.Message}");
                return new byte[0];
            }
        }

        private void SaveImageForDebug(byte[] imageBytes, int pageIndex)
        {
            try
            {
                var debugFolder = Path.Combine(Path.GetTempPath(), "LectureSummarizerDebug");
                Directory.CreateDirectory(debugFolder);
                
                var fileName = $"page_{pageIndex:D2}.jpg";
                var filePath = Path.Combine(debugFolder, fileName);
                
                File.WriteAllBytes(filePath, imageBytes);
                Console.WriteLine($"Debug: Saved image to {filePath} ({imageBytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Failed to save image - {ex.Message}");
            }
        }
    }
}
