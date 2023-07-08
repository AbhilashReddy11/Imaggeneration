using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace YourNamespace.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _httpClient;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

   
        [HttpPost]
  
        public async Task<IActionResult> GenerateImage(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                // Handle the case where the prompt is missing or empty
                return BadRequest("Prompt is required.");
            }

            var openAiApiKey = "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";
            var apiUrl = "https://api.openai.com/v1/images/generations";
            var imageSize = "1024x1024";

            try
            {
                // Prepare the request payload
                var requestData = new
                {
                    prompt,
                    n = 1,
                    size = imageSize
                };
                var jsonPayload = JsonSerializer.Serialize(requestData);

                // Set up the HTTP request
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");
                    var response = await httpClient.PostAsync(apiUrl, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

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
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to generate image. Error: {ex.Message}");
            }

            return View("Index");
        }

    }
}
