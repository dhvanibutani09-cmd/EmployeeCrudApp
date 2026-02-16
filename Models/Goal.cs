using System.ComponentModel.DataAnnotations;

namespace EmployeeCrudApp.Models;

public class Goal
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MinLength(3, ErrorMessage = "Title must be at least 3 characters.")]
    public string Title { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public DateTime StartDate { get; set; } = DateTime.Now;

    [Required]
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);
    
    public bool IsCompleted { get; set; } = false;
    
    public string Category { get; set; } = "Personal"; // Health, Finance, Study, Personal
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string Status
    {
        get
        {
            if (IsCompleted) return "Done";
            var today = DateTime.Now.Date;
            if (today < StartDate.Date) return "Upcoming";
            if (today > EndDate.Date) return "Late";
            return "Active";
        }
    }

    public string StatusColor
    {
        get
        {
            return Status switch
            {
                "Done" => "bg-success",
                "Late" => "bg-danger",
                "Upcoming" => "bg-secondary",
                _ => "bg-primary"
            };
        }
    }
}

public class FutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.Date >= DateTime.Now.Date;
        }
        return true;
    }
}
