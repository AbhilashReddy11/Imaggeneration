using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace YourNamespace.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(IHttpClientFactory httpClientFactory, IWebHostEnvironment webHostEnvironment)
        {
            _httpClient = httpClientFactory.CreateClient();
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateImage(string prompt, IFormFile imageFile)
        {
            var openAiApiKey = "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";
            var imageGenerationApiUrl = "https://api.openai.com/v1/images/generations";
            var imageVariationApiUrl = "https://api.openai.com/v1/images/variations";
            var imageSize = "1024x1024";

            try
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    // Image Generation
                    var requestData = new
                    {
                        prompt,
                        n = 1,
                        size = imageSize
                    };
                    var jsonPayload = JsonSerializer.Serialize(requestData);

                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);
                    var response = await _httpClient.PostAsync(imageGenerationApiUrl, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        return BadRequest($"Failed to generate image. Error: {errorResponse}");
                    }

                    var responseData = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonSerializer.Deserialize<JsonElement>(responseData);
                    var imageUrl = responseObject.GetProperty("data")[0].GetProperty("url").GetString();

                    ViewBag.ImageUrl = imageUrl;
                }

                if (imageFile != null)
                {
                    // Image Variation Generation
                    var imageVariations = await GenerateVariations(imageFile, openAiApiKey, imageVariationApiUrl);
                    ViewBag.ImageVariations = imageVariations;
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to generate image. Error: {ex.Message}");
            }

            return View("Index");
        }

        private async Task<List<string>> GenerateVariations(IFormFile imageFile, string openAiApiKey, string apiUrl, int n = 2, string size = "1024x1024")
        {
            var imageVariations = new List<string>();

            try
            {
                var imagePath = await UploadImage(imageFile);

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5); // Set the timeout to 5 minutes
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);

                    var requestContent = new MultipartFormDataContent();
                    requestContent.Add(new StringContent(n.ToString()), "n");
                    requestContent.Add(new StringContent(size), "size");
                    requestContent.Add(new StreamContent(imageFile.OpenReadStream()), "image", imageFile.FileName);

                    var response = await httpClient.PostAsync(apiUrl, requestContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Failed to generate image variations. Error: {errorResponse}");
                    }

                    var responseData = await response.Content.ReadAsStreamAsync();
                    imageVariations = GetImageVariations(responseData);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate image variations. Error: {ex.Message}");
            }

            return imageVariations;
        }

        private async Task<string> UploadImage(IFormFile imageFile)
        {
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return filePath;
        }

        private List<string> GetImageVariations(Stream responseStream)
        {
            var imageVariations = new List<string>();

            using (var reader = new StreamReader(responseStream))
            {
                var responseJson = reader.ReadToEnd();

                // Deserialize the JSON response
                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (responseObject.TryGetProperty("data", out var dataProperty) && dataProperty.ValueKind == JsonValueKind.Array)
                {
                    // Iterate over the data array to extract the image URLs
                    foreach (var dataItem in dataProperty.EnumerateArray())
                    {
                        if (dataItem.TryGetProperty("url", out var urlProperty) && urlProperty.ValueKind == JsonValueKind.String)
                        {
                            var imageUrl = urlProperty.GetString();
                            imageVariations.Add(imageUrl);
                        }
                    }
                }
                else
                {
                    throw new Exception("Failed to parse image variations from the API response.");
                }
            }

            return imageVariations;
        }
    }
}
