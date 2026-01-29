using System;

namespace EmployeeCrudApp.Models
{
    public class TimeEntry
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int DurationInSeconds { get; set; }
        public DateTimeOffset Date { get; set; }

        public string FormattedDuration => TimeSpan.FromSeconds(DurationInSeconds).ToString(@"hh\:mm\:ss");
    }
}
