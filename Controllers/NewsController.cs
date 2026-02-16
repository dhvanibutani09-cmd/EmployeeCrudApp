using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System;

namespace EmployeeCrudApp.Controllers
{
    public class NewsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;

        public NewsController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            // Reading API Key from "NewsApi:ApiKey" for security, with fallback to old path
            _apiKey = _configuration["NewsApi:ApiKey"] ?? _configuration["NewsApiSettings:ApiKey"] ?? string.Empty;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Search(string query)
        {
            ViewBag.Query = query;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetGlobalNews(string q = "")
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return Json(new { status = "error", message = "News API Key is missing in configuration." });
            }

            // Default to "latest" if search keyword is empty
            string searchQuery = string.IsNullOrWhiteSpace(q) ? "latest" : q.Trim();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp/1.0");
            // User requirements: 
            // - Pass apiKey in query string
            // - Always sort by publishedAt
            // - Limit results to 20 using pageSize=20
            // - Use everything endpoint
            string url = $"https://newsapi.org/v2/everything?q={System.Net.WebUtility.UrlEncode(searchQuery)}&sortBy=publishedAt&pageSize=20&apiKey={_apiKey}";

            try
            {
                var response = await client.GetAsync(url);
                
                // 1. Check if HTTP response status is successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        if (errorData.TryGetProperty("message", out var apiMsg)) {
                            return Json(new { status = "error", message = $"NewsAPI Error: {apiMsg.GetString()}" });
                        }
                    } catch { /* parse failed, fallback to status code */ }
                    
                    return Json(new { status = "error", message = $"HTTP Error: {response.StatusCode}. Unable to fetch news." });
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // 2. Check if API returns status: "error" in the payload
                if (root.TryGetProperty("status", out var status) && status.GetString() == "error")
                {
                    string errorMsg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown NewsAPI error";
                    return Json(new { status = "error", message = errorMsg });
                }
                
                if (root.TryGetProperty("articles", out var articles))
                {
                    // Backend sorting as a safety measure and filtering
                    var sortedArticles = articles.EnumerateArray()
                        .Where(a => {
                            if (!a.TryGetProperty("title", out var t) || string.IsNullOrEmpty(t.GetString())) return false;
                            if (t.GetString() == "[Removed]") return false;
                            // Ensure publishedAt exists to avoid exceptions during sort
                            if (!a.TryGetProperty("publishedAt", out var p) || string.IsNullOrEmpty(p.GetString())) return false;
                            return true;
                        })
                        .OrderByDescending(a => a.GetProperty("publishedAt").GetString())
                        .Take(20)
                        .ToList();
                    
                    // Use explicit serialization to avoid issues with JsonResult and JsonElement collections
                    var resultJson = JsonSerializer.Serialize(new { status = "ok", articles = sortedArticles });
                    return Content(resultJson, "application/json");
                }
                
                return Content(JsonSerializer.Serialize(new { status = "error", message = "No articles found in the response." }), "application/json");
            }
            catch (HttpRequestException httpEx)
            {
                return Json(new { status = "error", message = $"Network Error: {httpEx.Message}. Check your connection." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = $"System Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNews(string country = "", string category = "", string query = "")
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_NEWS_API_KEY_HERE")
            {
                return Json(new { status = "error", message = "API Key is missing or invalid in appsettings.json" });
            }

            var client = _httpClientFactory.CreateClient();
            string baseUrl;
            var queryParams = new List<string>();
            queryParams.Add($"apiKey={_apiKey}");
            
            // curated list of supported languages by NewsAPI to avoid 400 errors
            // ar,de,en,es,fr,he,it,nl,no,pt,ru,se,ud,zh
            // mapping common Indian languages to English as fallback since NewsAPI has limited support
            string currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string[] supportedLangs = { "ar", "de", "en", "es", "fr", "he", "it", "nl", "no", "pt", "ru", "se", "ud", "zh" };
            
            // If current lang is not supported (e.g. hi, gu, bn), fallback to 'en' but maybe we can depend on 'query' in local language?
            // Actually, NewsAPI 'q' param supports any language. 'language' param filters results.
            // If we want Hindi news, we should probably set language=hi IF supported, otherwise allow mixed?
            // NewsAPI documentation says language param restricts results.
            // Unfortunately NewsAPI FREE plan heavily favors English. Let's try to use the current lang if supported, else default to 'en'.
            
            string apiLang = supportedLangs.Contains(currentLang) ? currentLang : "";
            if (!string.IsNullOrEmpty(apiLang))
            {
                queryParams.Add($"language={apiLang}");
            }

            if (!string.IsNullOrEmpty(query))
            {
                // When searching with a query, use 'everything' endpoint for global results across all fields
                // 'everything' doesn't support category or country filters directly
                baseUrl = "https://newsapi.org/v2/everything";
                // Wrapping the query in quotes can help NewsAPI find more exact matches, 
                // especially for multi-word city names.
                queryParams.Add($"q=\"{query}\"");
                queryParams.Add("sortBy=publishedAt");
            }
            else
            {
                // When viewing trends without a search term, use 'top-headlines'
                // Use the configured BaseUrl if available, otherwise default to v2/top-headlines
                baseUrl = _configuration["NewsApiSettings:BaseUrl"] ?? "https://newsapi.org/v2/top-headlines";

                if (!string.IsNullOrEmpty(country))
                {
                    queryParams.Add($"country={country}");
                }

                if (!string.IsNullOrEmpty(category))
                {
                    queryParams.Add($"category={category}");
                }

                // If no filters are provided, top-headlines requires at least country, category or source.
                if (string.IsNullOrEmpty(country) && string.IsNullOrEmpty(category))
                {
                    queryParams.Add("category=general");
                }
            }

            var url = $"{baseUrl}?{string.Join("&", queryParams)}";

            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp");
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                return Json(new { status = "error", message = $"API Error: {response.StatusCode}", details = errorContent });
            }
            catch (HttpRequestException httpEx)
            {
                return Json(new { status = "error", message = "News service is unreachable.", details = httpEx.Message });
            }
            catch (System.Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEverything(string query, string sortBy = "publishedAt")
        {
             if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_NEWS_API_KEY_HERE")
            {
                return Json(new { status = "error", message = "API Key is missing or invalid in appsettings.json" });
            }

            var client = _httpClientFactory.CreateClient();
            var baseUrl = "https://newsapi.org/v2/everything";
            
            string currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string[] supportedLangs = { "ar", "de", "en", "es", "fr", "he", "it", "nl", "no", "pt", "ru", "se", "ud", "zh" };
            string apiLang = supportedLangs.Contains(currentLang) ? currentLang : "en";
            
            var url = $"{baseUrl}?apiKey={_apiKey}&q={query}&sortBy={sortBy}&language={apiLang}";

            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp");
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                return Json(new { status = "error", message = $"API Error: {response.StatusCode}" });
            }
            catch (HttpRequestException httpEx)
            {
                return Json(new { status = "error", message = "News service is unreachable.", details = httpEx.Message });
            }
            catch (System.Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
        public IActionResult CityNews()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCityNews(string city = "", string country = "")
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_NEWS_API_KEY_HERE")
            {
                return Json(new { status = "error", message = "API Key is missing or invalid" });
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp");

            // Trusted Indian news sources
            // Removed restricted Indian domains for unrestricted search
            string domains = ""; 
            
            string currentLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string[] supportedLangs = { "ar", "de", "en", "es", "fr", "he", "it", "nl", "no", "pt", "ru", "se", "ud", "zh" };

            // If current lang is not supported (e.g. hi), we omit language param to allow mixed results (which might include Hindi)
            // rather than forcing English. Trusted domains will ensure relevance.
            string apiLangParam = supportedLangs.Contains(currentLang) ? $"&language={currentLang}" : "";

            // 1. Try city + country
            string query = string.Join(" ", new[] { city, country }.Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrEmpty(query))
            {
                return Json(new { status = "error", message = "City or Country is required" });
            }

            string domainsParam = !string.IsNullOrEmpty(domains) ? $"&domains={domains}" : "";

            // Detect if the query is a known NewsAPI category
            string[] categories = { "business", "entertainment", "general", "health", "science", "sports", "technology" };
            string lowerQuery = query.ToLower().Trim();
            // Handle common variations like "sport" -> "sports"
            if (lowerQuery == "sport") lowerQuery = "sports";
            
            bool isCategory = categories.Contains(lowerQuery);

            string everythingUrl;
            if (isCategory)
            {
                // If it's a category, use top-headlines for better quality and images
                string countryCode = country.ToLower() == "india" ? "in" : (country.Length == 2 ? country.ToLower() : "in");
                everythingUrl = $"https://newsapi.org/v2/top-headlines?category={lowerQuery}&country={countryCode}&pageSize=100&apiKey={_apiKey}{apiLangParam}";
            }
            else
            {
                everythingUrl = $"https://newsapi.org/v2/everything?q={System.Net.WebUtility.UrlEncode(query)}{domainsParam}&sortBy=publishedAt&pageSize=100&apiKey={_apiKey}{apiLangParam}";
            }

            try
            {
                var response = await client.GetAsync(everythingUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    if (data.TryGetProperty("totalResults", out var total) && total.GetInt32() > 0)
                    {
                        return Content(content, "application/json");
                    }
                }

                // 2. Fallback to Country news if city specific search fails OR city was empty
                if (!string.IsNullOrEmpty(country))
                {
                    // Map common country names to 2-letter codes if necessary, or just search by name
                    // Here we try search by country name in domains first as it's more specific for trusted domains
                    string countryFallbackUrl = $"https://newsapi.org/v2/everything?q={System.Net.WebUtility.UrlEncode(country)}{domainsParam}&sortBy=publishedAt&pageSize=100&apiKey={_apiKey}{apiLangParam}";
                    
                    var fallbackResponse = await client.GetAsync(countryFallbackUrl);
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        var fallbackContent = await fallbackResponse.Content.ReadAsStringAsync();
                        return Content(fallbackContent, "application/json");
                    }
                }
                
                // If everything fails, return the first response or a friendly error
                return response.IsSuccessStatusCode 
                    ? Content(await response.Content.ReadAsStringAsync(), "application/json") 
                    : Json(new { status = "error", message = "No news found even with fallback." });
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network/DNS errors gracefully
                return Json(new { status = "error", message = "News service is currently unreachable (Network Error).", details = httpEx.Message });
            }
            catch (System.Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}
