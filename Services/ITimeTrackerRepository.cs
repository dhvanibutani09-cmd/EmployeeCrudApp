using EmployeeCrudApp.Models;
using System.Collections.Generic;

namespace EmployeeCrudApp.Services
{
    public interface ITimeTrackerRepository
    {
        IEnumerable<TimeEntry> GetAll(string userId);
        TimeEntry? GetById(int id, string userId);
        void Add(TimeEntry entry);
        void Update(TimeEntry entry, string userId);
        void Delete(int id, string userId);
    }
}
