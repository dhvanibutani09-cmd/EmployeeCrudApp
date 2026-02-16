using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using EmployeeCrudApp.Models;
using EmployeeCrudApp.Services;

namespace EmployeeCrudApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly INoteRepository _noteRepository;
        private readonly IHabitRepository _habitRepository;
        private readonly IUserRepository _userRepository;

        private readonly IGoalRepository _goalRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public DashboardController(INoteRepository noteRepository, IHabitRepository habitRepository, IUserRepository userRepository, IGoalRepository goalRepository, IHttpClientFactory httpClientFactory)
        {
            _noteRepository = noteRepository;
            _habitRepository = habitRepository;
            _userRepository = userRepository;
            _goalRepository = goalRepository;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetWeather(string location)
        {
            if (string.IsNullOrEmpty(location))
                return Json(new { success = false, message = "Location is required" });

            // Create a CancellationToken with a 10-second timeout
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp/1.0");
                
                // Fetch from wttr.in with JSON format
                string url = $"https://wttr.in/{System.Net.WebUtility.UrlEncode(location)}?m&format=j1&lang=en";
                
                try
                {
                    var response = await client.GetAsync(url, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(content) && content.Trim().StartsWith("{"))
                        {
                            return Content(content, "application/json");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"wttr.in failed, trying fallback: {ex.Message}");
                }

                // Fallback to Open-Meteo if wttr.in fails
                return await GetWeatherFromOpenMeteo(location);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Weather service error: {ex.Message}" });
            }
        }

        private async Task<IActionResult> GetWeatherFromOpenMeteo(string location)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "EmployeeCrudApp/1.0");

                double lat = 0, lon = 0;
                string cityName = location;

                // 1. Resolve coordinates
                if (location.Contains(","))
                {
                    var parts = location.Split(',');
                    if (parts.Length >= 2 && double.TryParse(parts[0], out lat) && double.TryParse(parts[1], out lon))
                    {
                        // Coordinates resolved
                    }
                }
                else
                {
                    // Geocode city name via Open-Meteo Geocoding API
                    var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={System.Net.WebUtility.UrlEncode(location)}&count=1&language=en&format=json";
                    var geoRes = await client.GetAsync(geoUrl);
                    if (geoRes.IsSuccessStatusCode)
                    {
                        var geoContent = await geoRes.Content.ReadAsStringAsync();
                        using var geoDoc = System.Text.Json.JsonDocument.Parse(geoContent);
                        if (geoDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                        {
                            var first = results[0];
                            lat = first.GetProperty("latitude").GetDouble();
                            lon = first.GetProperty("longitude").GetDouble();
                            cityName = first.GetProperty("name").GetString();
                        }
                    }
                }

                if (lat == 0 && lon == 0) return Json(new { success = false, message = "Could not resolve location coordinates." });

                // 2. Fetch Forecast
                var forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,surface_pressure,uv_index&daily=weather_code,sunrise,sunset&timezone=auto";
                var res = await client.GetAsync(forecastUrl);
                
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    var current = root.GetProperty("current");
                    var daily = root.GetProperty("daily");

                    // Map to a wttr.in-like structure that the frontend expects
                    var mappedResponse = new
                    {
                        current_condition = new[] {
                            new {
                                temp_C = current.GetProperty("temperature_2m").GetRawText(),
                                weatherDesc = new[] { new { value = MapWeatherCode(current.GetProperty("weather_code").GetInt32()) } },
                                uvIndex = current.GetProperty("uv_index").GetRawText(),
                                pressure = current.GetProperty("surface_pressure").GetRawText(),
                                humidity = current.GetProperty("relative_humidity_2m").GetRawText(),
                                FeelsLikeC = current.GetProperty("apparent_temperature").GetRawText()
                            }
                        },
                        weather = new[] {
                            new {
                                astronomy = new[] {
                                    new {
                                        sunrise = daily.GetProperty("sunrise")[0].GetString()?.Length > 11 ? daily.GetProperty("sunrise")[0].GetString()?.Substring(11) : "06:00 AM",
                                        sunset = daily.GetProperty("sunset")[0].GetString()?.Length > 11 ? daily.GetProperty("sunset")[0].GetString()?.Substring(11) : "06:00 PM"
                                    }
                                },
                                hourly = new[] { new { chanceofrain = current.GetProperty("precipitation").GetRawText() } }
                            }
                        },
                        nearest_area = new[] {
                            new {
                                areaName = new[] { new { value = cityName } },
                                country = new[] { new { value = "" } }
                            }
                        }
                    };

                    return Content(System.Text.Json.JsonSerializer.Serialize(mappedResponse), "application/json");
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Fallback weather error: {ex.Message}" });
            }

            return Json(new { success = false, message = "All weather services are currently unreachable." });
        }

        private string MapWeatherCode(int code)
        {
            return code switch {
                0 => "Clear sky",
                1 or 2 or 3 => "Mainly clear, partly cloudy, and overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                71 or 73 or 75 => "Snow fall",
                95 or 96 or 99 => "Thunderstorm",
                _ => "Unknown"
            };
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index()
        {
            var email = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
            var user = _userRepository.GetByEmail(email);
            
            var allGoals = _goalRepository.GetAll(email).OrderByDescending(g => g.CreatedDate).ToList();

            var viewModel = new DashboardViewModel
            {
                Notes = _noteRepository.GetAll(email).OrderByDescending(n => n.CreatedAt).ToList(),
                Habits = _habitRepository.GetAll(email).OrderByDescending(h => h.CreatedAt).ToList(),
                PermittedWidgets = user?.PermittedWidgets ?? new List<string>(),
                HasSecurityPin = !string.IsNullOrEmpty(user?.SecurityPin),
                IsPinVerified = HttpContext.Session.GetString("PinVerified") == "true",
                IsNotesVerified = HttpContext.Session.GetString("PinVerified_Notes") == "true",
                IsHabitsVerified = HttpContext.Session.GetString("PinVerified_Habit") == "true",
                IsTranslatorVerified = HttpContext.Session.GetString("PinVerified_Translator") == "true",
                IsPdfVerified = HttpContext.Session.GetString("PinVerified_PDF") == "true",
                Goals = allGoals,
                RecentGoals = allGoals
            };

            // Calculate Goal Stats
            viewModel.TotalGoals = viewModel.Goals.Count;
            viewModel.CompletedGoals = viewModel.Goals.Count(g => g.Status == "Done");
            viewModel.ActiveGoals = viewModel.Goals.Count(g => g.Status == "Active");
            viewModel.OverdueGoals = viewModel.Goals.Count(g => g.Status == "Late");

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult VerifyPin(string pin, string widgetName)
        {
            var email = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
            var user = _userRepository.GetByEmail(email);

            if (user != null && user.SecurityPin == pin)
            {
                if (!string.IsNullOrEmpty(widgetName))
                {
                    HttpContext.Session.SetString($"PinVerified_{widgetName}", "true");
                }
                else
                {
                    HttpContext.Session.SetString("PinVerified", "true");
                }
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Invalid PIN" });
        }

        [HttpPost]
        public IActionResult AddNote(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var userId = User.Identity?.Name ?? string.Empty;
                _noteRepository.Add(new Note { Text = text, UserId = userId });
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Note text cannot be empty." });
        }

        [HttpPost]
        public IActionResult EditNote(int id, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var userId = User.Identity?.Name ?? string.Empty;
                var note = new Note { Id = id, Text = text };
                _noteRepository.Update(note, userId);
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Note text cannot be empty." });
        }

        [HttpPost]
        public IActionResult DeleteNote(int id)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            _noteRepository.Delete(id, userId);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult AddHabit(string name, string description, string frequency, string customDays, DateTime? startDate)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var userId = User.Identity?.Name ?? string.Empty;
                var days = new List<DayOfWeek>();
                if (frequency == "Custom" && !string.IsNullOrEmpty(customDays))
                {
                    try
                    {
                        days = customDays.Split(',')
                                         .Select(d => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), d))
                                         .ToList();
                    }
                    catch { /* Ignore invalid days */ }
                }

                _habitRepository.Add(new Habit 
                { 
                    Name = name, 
                    Description = description, 
                    UserId = userId,
                    Frequency = frequency,
                    CustomDays = days,
                    StartDate = startDate ?? DateTime.Today
                });
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Habit name cannot be empty." });
        }

        [HttpPost]
        public IActionResult ToggleHabit(int id)
        {
            var userId = User.Identity?.Name ?? string.Empty;
            var habit = _habitRepository.GetById(id);
            
            if (habit != null && habit.UserId == userId)
            {
                var today = DateTime.Today;
                if (habit.CompletedDates.Any(d => d.Date == today))
                {
                    habit.CompletedDates.RemoveAll(d => d.Date == today);
                }
                else
                {
                    habit.CompletedDates.Add(today);
                }
                _habitRepository.Update(habit);
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Habit not found." });
        }

        [HttpPost]
        public IActionResult DeleteHabit(int id)
        {
             var userId = User.Identity?.Name ?? string.Empty;
             // Ideally check ownership before delete but current repo structure might assume id is enough or handle it.
             // For safety let's assume we should check, but simple delete for now based on logic.
             // Actually repo.Delete(id, userId) would be safer but I only defined Delete(id).
             // Let's stick to simple delete for this MVP or improved if I changed repo.
             // I'll check existence first.
             var habit = _habitRepository.GetById(id);
             if (habit != null && habit.UserId == userId)
             {
                 _habitRepository.Delete(id);
                 return Json(new { success = true });
             }
             return Json(new { success = false, message = "Habit not found." });
        }



        [HttpPost]
        public IActionResult AddGoal(Goal goal)
        {
            if (ModelState.IsValid)
            {
                goal.UserId = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
                goal.IsCompleted = false;
                goal.CreatedDate = DateTime.Now;
                
                _goalRepository.Add(goal);
                return Json(new { success = true });
            }
            
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join("\n", errors) });
        }

        [HttpPost]
        public IActionResult ToggleGoalCompletion(int id)
        {
            var userId = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
            var goal = _goalRepository.GetById(id);
            if (goal != null && goal.UserId == userId)
            {
                goal.IsCompleted = !goal.IsCompleted;
                _goalRepository.Update(goal);
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Goal not found." });
        }

        [HttpPost]
        public IActionResult EditGoal(Goal goal)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
                var existingGoal = _goalRepository.GetById(goal.Id);
                
                if (existingGoal != null && existingGoal.UserId == userId)
                {
                    existingGoal.Title = goal.Title;
                    existingGoal.StartDate = goal.StartDate;
                    existingGoal.EndDate = goal.EndDate;
                    existingGoal.Category = goal.Category;
                    existingGoal.Priority = goal.Priority;
                    
                    _goalRepository.Update(existingGoal);
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Goal not found or access denied." });
            }
            
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join("\n", errors) });
        }

        [HttpPost]
        public IActionResult DeleteGoal(int id)
        {
            var userId = User.FindFirst("Email")?.Value ?? User.Identity?.Name ?? string.Empty;
            var goal = _goalRepository.GetById(id);
            if (goal != null && goal.UserId == userId)
            {
                _goalRepository.Delete(id);
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Goal not found." });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
