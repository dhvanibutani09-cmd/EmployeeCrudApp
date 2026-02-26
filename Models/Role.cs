namespace EmployeeCrudApp.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> PermittedWidgets { get; set; } = new List<string>();
    public bool CanViewUsers { get; set; }
    public bool CanAddUser { get; set; }
    public bool CanEditUser { get; set; }
    public bool CanDeleteUser { get; set; }
    public bool CanAccessDashboard { get; set; }
    public bool CanAccessWidgets { get; set; }
    public bool CanAccessSettings { get; set; }
}
