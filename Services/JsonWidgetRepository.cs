using EmployeeCrudApp.Models;
using System.Text.Json;

namespace EmployeeCrudApp.Services;

public class JsonWidgetRepository : IWidgetRepository
{
    private readonly string _filePath;

    public JsonWidgetRepository(IWebHostEnvironment webHostEnvironment)
    {
        _filePath = Path.Combine(webHostEnvironment.ContentRootPath, "widgets.json");
        if (!File.Exists(_filePath))
        {
            var initialWidgets = new List<Widget>
            {
                new Widget { Id = 1, Name = "Weather Details" },
                new Widget { Id = 2, Name = "Currency Conversion" },
                new Widget { Id = 3, Name = "Time Conversion" },
                new Widget { Id = 4, Name = "Headlines / News" },
                new Widget { Id = 5, Name = "World Countries" },
                new Widget { Id = 6, Name = "Personal Notes" },
                new Widget { Id = 7, Name = "Habit Tracker" },
                new Widget { Id = 8, Name = "Emergency Numbers" },
                new Widget { Id = 9, Name = "Language Translator" },
                new Widget { Id = 10, Name = "PDF Converter" },
                new Widget { Id = 11, Name = "Goal Tracking" }
            };
            var json = JsonSerializer.Serialize(initialWidgets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }

    private List<Widget> ReadData()
    {
        if (!File.Exists(_filePath)) return new List<Widget>();
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<Widget>();
        return JsonSerializer.Deserialize<List<Widget>>(json) ?? new List<Widget>();
    }

    public IEnumerable<Widget> GetAll() => ReadData();
}
