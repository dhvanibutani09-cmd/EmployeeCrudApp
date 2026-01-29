using EmployeeCrudApp.Models;
using System.Text.Json;

namespace EmployeeCrudApp.Services;

public class JsonTimeTrackerRepository : ITimeTrackerRepository
{
    private readonly string _filePath;

    public JsonTimeTrackerRepository(IWebHostEnvironment webHostEnvironment)
    {
        _filePath = Path.Combine(webHostEnvironment.ContentRootPath, "time_entries.json");
    }

    public IEnumerable<TimeEntry> GetAll(string userId)
    {
        return LoadAll().Where(e => e.UserId == userId);
    }

    public TimeEntry? GetById(int id, string userId)
    {
        return GetAll(userId).FirstOrDefault(e => e.Id == id);
    }

    public void Add(TimeEntry entry)
    {
        var allEntries = LoadAll();
        entry.Id = allEntries.Any() ? allEntries.Max(e => e.Id) + 1 : 1;
        allEntries.Add(entry);
        SaveAll(allEntries);
    }

    public void Update(TimeEntry entry, string userId)
    {
        var allEntries = LoadAll();
        var index = allEntries.FindIndex(e => e.Id == entry.Id && e.UserId == userId);
        if (index != -1)
        {
            allEntries[index].TaskName = entry.TaskName;
            allEntries[index].StartTime = entry.StartTime;
            allEntries[index].EndTime = entry.EndTime;
            allEntries[index].DurationInSeconds = entry.DurationInSeconds;
            allEntries[index].Date = entry.Date;
            SaveAll(allEntries);
        }
    }

    public void Delete(int id, string userId)
    {
        var allEntries = LoadAll();
        var entry = allEntries.FirstOrDefault(e => e.Id == id && e.UserId == userId);
        if (entry != null)
        {
            allEntries.Remove(entry);
            SaveAll(allEntries);
        }
    }

    private List<TimeEntry> LoadAll()
    {
        if (!File.Exists(_filePath)) return new List<TimeEntry>();
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<TimeEntry>>(json) ?? new List<TimeEntry>();
    }

    private void SaveAll(List<TimeEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
