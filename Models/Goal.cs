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
    [FutureDate(ErrorMessage = "Target Date must be in the future.")]
    public DateTime TargetDate { get; set; }
    
    [Required]
    [Range(0.1, double.MaxValue, ErrorMessage = "Target Value must be greater than 0.")]
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    
    public string Category { get; set; } = "Personal"; // Health, Finance, Study, Personal
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string Status
    {
        get
        {
            if (CurrentValue >= TargetValue) return "Completed";
            if (DateTime.Now.Date > TargetDate.Date && ProgressPercentage < 100) return "Overdue";
            return "Active";
        }
    }

    public int ProgressPercentage
    {
        get
        {
            if (TargetValue == 0) return 0;
            var percentage = (int)((CurrentValue * 100) / TargetValue);
            return Math.Min(percentage, 100); // Cap at 100%
        }
    }

    public string ProgressBarColor
    {
        get
        {
            if (Status == "Completed") return "bg-success";
            if (Status == "Overdue") return "bg-danger";
            
            var p = ProgressPercentage;
            if (p < 25) return "bg-danger";
            if (p < 50) return "bg-warning";
            if (p < 75) return "bg-info";
            return "bg-primary";
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
