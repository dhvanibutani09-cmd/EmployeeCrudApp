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
    public bool IsPinVerified { get; set; } // Global legacy (kept for compatibility)
    public bool IsNotesVerified { get; set; }
    public bool IsHabitsVerified { get; set; }
    public bool IsTranslatorVerified { get; set; }
    public bool IsPdfVerified { get; set; }
    public List<Goal> Goals { get; set; } = new List<Goal>();
    public List<Goal> RecentGoals { get; set; } = new List<Goal>();
    
    public int TotalGoals { get; set; }
    public int CompletedGoals { get; set; }
    public int ActiveGoals { get; set; }
    public int OverdueGoals { get; set; }

}
