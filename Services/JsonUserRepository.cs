using EmployeeCrudApp.Models;
using System.Text.Json;

namespace EmployeeCrudApp.Services;

public class JsonUserRepository : IUserRepository
{
    private readonly string _filePath;
    private readonly IRoleRepository _roleRepository;

    public JsonUserRepository(IWebHostEnvironment webHostEnvironment, IRoleRepository roleRepository)
    {
        _filePath = Path.Combine(webHostEnvironment.ContentRootPath, "user.json");
        _roleRepository = roleRepository;
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "[]");
        }
    }

    private List<User> ReadData()
    {
        if (!File.Exists(_filePath)) return new List<User>();
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<User>();
        var users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        
        foreach (var user in users)
        {
            // RBAC Inheritance: Prioritize RoleId, fallback to Role name for legacy support
            var role = _roleRepository.GetAll().FirstOrDefault(r => r.Id == user.RoleId) 
                       ?? _roleRepository.GetByName(user.Role);
            
            if (role != null)
            {
                // Strict RBAC Inheritance: Users always use their role's defined permissions.
                user.PermittedWidgets = role.PermittedWidgets;
                user.Role = role.Name; // Sync name
                user.RoleId = role.Id; // Sync ID
            }
        }
        
        return users;
    }

    private void WriteData(List<User> users)
    {
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public IEnumerable<User> GetAll() => ReadData();

    public User? GetByEmail(string email) => ReadData().FirstOrDefault(u => u.Email == email);

    public User? GetById(int id) => ReadData().FirstOrDefault(u => u.Id == id);

    public void Add(User user)
    {
        var users = ReadData();
        user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
        users.Add(user);
        WriteData(users);
    }

    public void Update(User user)
    {
        var users = ReadData();
        var index = users.FindIndex(u => u.Id == user.Id);
        if (index != -1)
        {
            users[index] = user;
            WriteData(users);
        }
    }

    public void Delete(int id)
    {
        var users = ReadData();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            users.Remove(user);
            WriteData(users);
        }
    }
}
