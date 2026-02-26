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
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Target value must be greater than zero.")]
    public decimal TargetValue { get; set; } = 100;

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Current value cannot be negative.")]
    public decimal CurrentValue { get; set; } = 0;

    public string Category { get; set; } = "Personal"; // Health, Finance, Study, Personal
    public string Priority { get; set; } = "Medium"; // Low, Medium, High
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastReminderDate { get; set; }

    // --- Smart Calculations ---

    public int TotalDays => Math.Max(1, (EndDate.Date - StartDate.Date).Days + 1); // Inclusive

    public decimal DailyTarget => TargetValue / TotalDays;

    public int DaysPassed
    {
        get
        {
            var passed = (DateTime.Now.Date - StartDate.Date).Days;
            return Math.Clamp(passed, 0, TotalDays);
        }
    }

    public int DaysRemaining => Math.Max(0, (EndDate.Date - DateTime.Now.Date).Days);

    public decimal ExpectedProgress => DailyTarget * Math.Min(DaysPassed + 1, TotalDays); // Expected by end of today

    public double ProgressPercentage => (double)Math.Clamp((CurrentValue / TargetValue) * 100, 0, 100);

    // --- Daily Log System ---
    public List<DailyLogEntry> DailyLogs { get; set; } = new List<DailyLogEntry>();

    public class DailyLogEntry
    {
        public DateTime Date { get; set; }
        public decimal Target { get; set; }
        public decimal Actual { get; set; }
        public bool IsToday => Date.Date == DateTime.Now.Date;
        public bool IsPast => Date.Date < DateTime.Now.Date;
        public bool IsFuture => Date.Date > DateTime.Now.Date;
    }

    // --- Enterprise Performance Metrics ---

    public decimal CurrentVelocity => DaysPassed > 0 ? CurrentValue / DaysPassed : CurrentValue;

    public decimal RequiredVelocity
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return 0;
            return DaysRemaining > 0 ? (TargetValue - CurrentValue) / DaysRemaining : (TargetValue - CurrentValue);
        }
    }

    public double PerformanceEfficiency
    {
        get
        {
            if (ExpectedProgress <= 0) return CurrentValue > 0 ? 100 : 0;
            return (double)(CurrentValue / ExpectedProgress) * 100;
        }
    }

    public int HealthScore
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return 100;
            if (DateTime.Now.Date > EndDate.Date) return 0;
            
            double score = PerformanceEfficiency * 0.7 + ProgressPercentage * 0.3;
            
            // Difficulty Multiplier
            if (Difficulty == "Hard") score *= 0.85; // Harder to maintain high health
            else if (Difficulty == "Easy") score *= 1.1; // Easier to maintain high health
            
            if (DaysRemaining <= 1 && ProgressPercentage < 90) score *= 0.5; // Urgency penalty
            
            return (int)Math.Clamp(score, 0, 100);
        }
    }

    public string RemainingTimeDisplay
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return "Completed";
            var remaining = EndDate.Date.AddDays(1) - DateTime.Now;
            if (remaining.TotalSeconds <= 0) return "Late";
            
            if (remaining.TotalDays >= 1)
                return $"{(int)remaining.TotalDays}d {(int)remaining.Hours}h remaining";
            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h {(int)remaining.Minutes}m remaining";
            return $"{(int)remaining.TotalMinutes}m {(int)remaining.Seconds}s remaining";
        }
    }

    public string SmartSuggestion
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return "Goal achieved! Great job.";
            if (DateTime.Now.Date > EndDate.Date) return "Goal is overdue. Focus on immediate completion.";

            if (PerformanceEfficiency >= 110) return "Excellent! You are ahead of schedule. Consider an early finish.";
            if (PerformanceEfficiency >= 95) return "You're on track. Maintain your current pace to succeed.";
            if (PerformanceEfficiency >= 80) return "Slightly behind. You are 'At Risk'. Increase effort to stay on track.";
            if (PerformanceEfficiency >= 60) return $"Behind. Increase daily output to {RequiredVelocity:F1} to finish on time.";
            return $"High risk! You need {RequiredVelocity:F1} per day to recover this goal.";
        }
    }

    public List<string> AchievementBadges
    {
        get
        {
            var badges = new List<string>();
            if (!IsCompleted && CurrentValue < TargetValue) return badges;

            badges.Add("Finisher|bi-check-all|bg-success");
            
            // Check for "Early Bird" (finished with more than 15% time left)
            if (TotalDays > 1 && DaysRemaining > (TotalDays * 0.15))
                badges.Add("Early Bird|bi-lightning-fill|bg-primary");

            // Check for "High Velocity" (average speed was 25% higher than needed)
            if (CurrentVelocity > DailyTarget * 1.25m)
                badges.Add("High Velocity|bi-speedometer|bg-info");
                
            return badges;
        }
    }

    public DateTime? EstimatedCompletionDate
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return null;
            if (DaysPassed <= 0 || CurrentValue <= 0) return null;

            decimal avgDaily = CurrentVelocity;
            if (avgDaily <= 0) return null;

            decimal remaining = TargetValue - CurrentValue;
            int daysNeeded = (int)Math.Ceiling(remaining / avgDaily);
            
            return DateTime.Now.Date.AddDays(daysNeeded);
        }
    }

    public string Status
    {
        get
        {
            if (IsCompleted || CurrentValue >= TargetValue) return "Completed";
            if (DateTime.Now.Date > EndDate.Date) return "Late";
            
            if (PerformanceEfficiency >= 95) return "On Track";
            if (PerformanceEfficiency >= 80) return "At Risk";
            return "Behind";
        }
    }

    public string StatusColor
    {
        get
        {
            return Status switch
            {
                "Completed" => "bg-success",
                "Late" => "bg-danger",
                "On Track" => "bg-primary",
                "At Risk" => "bg-info",
                "Behind" => "bg-warning",
                _ => "bg-secondary"
            };
        }
    }

    public void SyncDailyLogs()
    {
        var totalDays = TotalDays;
        var dailyTarget = DailyTarget;
        var newLogs = new List<DailyLogEntry>();

        for (int i = 0; i < totalDays; i++)
        {
            var date = StartDate.Date.AddDays(i);
            var existing = DailyLogs.FirstOrDefault(l => l.Date.Date == date);
            
            newLogs.Add(new DailyLogEntry
            {
                Date = date,
                Target = dailyTarget,
                Actual = existing?.Actual ?? 0
            });
        }

        DailyLogs = newLogs;
        CurrentValue = DailyLogs.Sum(l => l.Actual);
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
