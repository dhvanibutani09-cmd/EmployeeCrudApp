using EmployeeCrudApp.Models;
using System.Text.Json;

namespace EmployeeCrudApp.Services;

public class JsonRoleRepository : IRoleRepository
{
    private readonly string _filePath;

    public JsonRoleRepository(IWebHostEnvironment webHostEnvironment)
    {
        _filePath = Path.Combine(webHostEnvironment.ContentRootPath, "roles.json");
        if (!File.Exists(_filePath))
        {
            var initialRoles = new List<Role>
            {
                new Role { 
                    Id = 1, Name = "Admin", 
                    PermittedWidgets = new List<string> { "Weather Details", "Currency Conversion", "Time Conversion", "Headlines / News", "World Countries", "Personal Notes", "Habit Tracker", "Emergency Numbers", "Language Translator", "PDF Converter", "Goal Tracking" },
                    CanViewUsers = true, CanAddUser = true, CanEditUser = true, CanDeleteUser = true, 
                    CanAccessDashboard = true, CanAccessWidgets = true, CanAccessSettings = true 
                },
                new Role { 
                    Id = 2, Name = "User", 
                    PermittedWidgets = new List<string> { "Weather Details", "Currency Conversion", "Time Conversion", "Headlines / News", "World Countries", "Emergency Numbers", "Language Translator", "Goal Tracking" },
                    CanViewUsers = true, CanAddUser = false, CanEditUser = false, CanDeleteUser = false, 
                    CanAccessDashboard = true, CanAccessWidgets = true, CanAccessSettings = false 
                },
                new Role { 
                    Id = 3, Name = "Private", 
                    PermittedWidgets = new List<string> { "Weather Details", "Time Conversion", "Personal Notes", "Emergency Numbers", "Language Translator" },
                    CanViewUsers = false, CanAddUser = false, CanEditUser = false, CanDeleteUser = false, 
                    CanAccessDashboard = true, CanAccessWidgets = true, CanAccessSettings = false
                },
                new Role { 
                    Id = 4, Name = "Visitor", 
                    PermittedWidgets = new List<string> { "Weather Details", "Currency Conversion", "Time Conversion", "Headlines / News", "World Countries", "Emergency Numbers" },
                    CanViewUsers = false, CanAddUser = false, CanEditUser = false, CanDeleteUser = false, 
                    CanAccessDashboard = true, CanAccessWidgets = false, CanAccessSettings = false
                }
            };
            WriteData(initialRoles);
        }
    }

    private List<Role> ReadData()
    {
        if (!File.Exists(_filePath)) return new List<Role>();
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<Role>();
        return JsonSerializer.Deserialize<List<Role>>(json) ?? new List<Role>();
    }

    private void WriteData(List<Role> roles)
    {
        var json = JsonSerializer.Serialize(roles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public IEnumerable<Role> GetAll() => ReadData();

    public Role? GetByName(string name) => ReadData().FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void Add(Role role)
    {
        var roles = ReadData();
        role.Id = roles.Count > 0 ? roles.Max(r => r.Id) + 1 : 1;
        roles.Add(role);
        WriteData(roles);
    }

    public void Update(Role role)
    {
        var roles = ReadData();
        var index = roles.FindIndex(r => r.Id == role.Id || r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase));
        if (index != -1)
        {
            roles[index] = role;
            WriteData(roles);
        }
    }
}
