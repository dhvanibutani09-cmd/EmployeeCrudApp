namespace EmployeeCrudApp.Models;

public class DashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int TotalUsers { get; set; }
    public List<Employee> RecentEmployees { get; set; } = new List<Employee>();
    public List<Note> Notes { get; set; } = new List<Note>();
    public List<Habit> Habits { get; set; } = new List<Habit>();
    public int NewUsersToday { get; set; }
    public List<string> PermittedWidgets { get; set; } = new List<string>();
    public bool HasSecurityPin { get; set; }
    public bool IsPinVerified { get; set; }

}
