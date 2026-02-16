using EmployeeCrudApp.Models;
using System.Text.Json;

namespace EmployeeCrudApp.Services;

public class JsonGoalRepository : IGoalRepository
{
    private readonly string _filePath;

    public JsonGoalRepository(IWebHostEnvironment webHostEnvironment)
    {
        _filePath = Path.Combine(webHostEnvironment.ContentRootPath, "goals.json");
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "[]");
        }
    }

    private List<Goal> ReadData()
    {
        if (!File.Exists(_filePath)) return new List<Goal>();
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<Goal>();
        return JsonSerializer.Deserialize<List<Goal>>(json) ?? new List<Goal>();
    }

    private void WriteData(List<Goal> goals)
    {
        var json = JsonSerializer.Serialize(goals, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public IEnumerable<Goal> GetAll(string userId)
    {
        return ReadData().Where(g => g.UserId == userId);
    }

    public Goal? GetById(int id)
    {
        return ReadData().FirstOrDefault(g => g.Id == id);
    }

    public void Add(Goal goal)
    {
        var goals = ReadData();
        goal.Id = goals.Count > 0 ? goals.Max(g => g.Id) + 1 : 1;
        goals.Add(goal);
        WriteData(goals);
    }

    public void Update(Goal goal)
    {
        var goals = ReadData();
        var index = goals.FindIndex(g => g.Id == goal.Id);
        if (index != -1)
        {
            goals[index] = goal;
            WriteData(goals);
        }
    }

    public void Delete(int id)
    {
        var goals = ReadData();
        var goal = goals.FirstOrDefault(g => g.Id == id);
        if (goal != null)
        {
            goals.Remove(goal);
            WriteData(goals);
        }
    }
}
