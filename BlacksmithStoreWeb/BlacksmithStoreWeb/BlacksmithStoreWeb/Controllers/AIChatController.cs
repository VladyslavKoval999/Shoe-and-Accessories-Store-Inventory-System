using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Data;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace BlacksmithStoreWeb.Controllers
{
    public class AIChatController : Controller
    {
        private readonly StoreContext _context;

        private readonly string _apiKey = "***********************"; //

        public AIChatController(StoreContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (request == null || request.History == null || !request.History.Any())
                return Json(new { success = false, reply = "Я вас слухаю... Напишіть щось!" });

            try
            {
                var products = _context.Products
                    .Where(p => p.BasePrice > 0)
                    .Select(p => new ProductMiniDto
                    {
                        Name = p.Name ?? "Товар",
                        BasePrice = p.BasePrice,
                        ProductType = p.ProductType ?? "Інше"
                    })
                    .Take(200)
                    .ToList();

                if (string.IsNullOrEmpty(_apiKey))
                {
                    return GenerateLocalFallbackResponse(request.History.Last().Text.ToLower(), products);
                }

                using var client = new HttpClient();

                string workingModel = await GetFirstAvailableModelAsync(client, _apiKey);

                string catalogInfo = "Асортимент: " + string.Join("; ", products.Select(p => $"{p.Name} ({p.ProductType}) - {Math.Round(p.BasePrice)} ₴"));
                string systemPrompt = $"Ти — консультант преміум-магазину Blacksmith Store. Каталог: {catalogInfo}. Відповідай українською, коротко і привітно. Допомагай з вибором. Для виділення назв товарів використовуй жирний шрифт (наприклад, **Nike**). Для переліку товарів використовуй марковані списки (починай з *).";

                var url = $"https://generativelanguage.googleapis.com/v1beta/{workingModel}:generateContent?key={_apiKey}";

                var chatHistory = new List<object>();
                for (int i = 0; i < request.History.Count; i++)
                {
                    var h = request.History[i];
                    if (i == 0 && h.Role == "user")
                    {
                        chatHistory.Add(new
                        {
                            role = "user",
                            parts = new[] { new { text = $"[СИСТЕМНА ІНСТРУКЦІЯ]: {systemPrompt}\n\nЗапит клієнта: {h.Text}" } }
                        });
                    }
                    else
                    {
                        chatHistory.Add(new
                        {
                            role = h.Role == "user" ? "user" : "model",
                            parts = new[] { new { text = h.Text } }
                        });
                    }
                }

                var payload = new { contents = chatHistory };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    int statusCode = (int)response.StatusCode;

                    if (statusCode == 503 || statusCode == 429)
                    {
                        return Json(new
                        {
                            success = true,
                            reply = "Ой, зараз дуже багато звернень і я трохи не встигаю! 😅 Будь ласка, зачекайте буквально хвилинку і надішліть свій запит ще раз."
                        });
                    }

                    return GenerateLocalFallbackResponse(request.History.Last().Text.ToLower(), products);
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var resContent) &&
                    resContent.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var replyText = parts[0].GetProperty("text").GetString();
                    return Json(new { success = true, reply = replyText });
                }

                return Json(new { success = false, reply = "Помилка розбору відповіді від сервера." });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = true,
                    reply = "Зв'язок з ШІ тимчасово втрачено. Наш асортимент включає стильне взуття та аксесуари. Що саме вас цікавить?"
                });
            }
        }

        private async Task<string> GetFirstAvailableModelAsync(HttpClient client, string apiKey)
        {
            try
            {
                var response = await client.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var models = doc.RootElement.GetProperty("models").EnumerateArray();

                    foreach (var model in models)
                    {
                        var name = model.GetProperty("name").GetString();
                        var methods = model.GetProperty("supportedGenerationMethods").EnumerateArray().Select(m => m.GetString()).ToList();

                        if (methods.Contains("generateContent") && name.Contains("gemini"))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }

            return "models/gemini-1.5-flash";
        }

        private IActionResult GenerateLocalFallbackResponse(string message, List<ProductMiniDto> products)
        {
            if (message.Contains("привіт") || message.Contains("добрий день") || message.Contains("вітаю"))
                return Json(new { success = true, reply = "Вітаю у Blacksmith Store! Який товар вас цікавить: взуття чи аксесуари?" });

            bool wantsShoes = message.Contains("взут") || message.Contains("кросівк") || message.Contains("кед") || message.Contains("черевик");
            bool wantsAccessories = message.Contains("аксесуар") || message.Contains("рюкзак") || message.Contains("сумк") || message.Contains("шнурк");

            var foundProducts = products.Where(p =>
                message.Contains(p.Name.ToLower()) ||
                (wantsShoes && p.ProductType == "Взуття") ||
                (wantsAccessories && p.ProductType == "Аксесуар")
            ).Take(3).ToList();

            if (foundProducts.Any())
            {
                string reply = "Ось що я можу запропонувати з нашого каталогу: " +
                               string.Join(", ", foundProducts.Select(p => $"'{p.Name}' за {Math.Round(p.BasePrice)} ₴"));
                reply += ". Чи бажаєте дізнатися деталі про якийсь із них?";
                return Json(new { success = true, reply = reply });
            }

            return Json(new { success = true, reply = "Підкажіть точніше, що саме ви шукаєте? Наш асортимент включає стильне взуття та преміальні аксесуари." });
        }
    }

    public class ChatRequest
    {
        public List<ChatMessage> History { get; set; } = new List<ChatMessage>();
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class ProductMiniDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public string ProductType { get; set; } = string.Empty;
    }
}