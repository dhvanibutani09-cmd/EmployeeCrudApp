using EmployeeCrudApp.Models;

namespace EmployeeCrudApp.Services;

public interface IGoalRepository
{
    IEnumerable<Goal> GetAll(string userId);
    Goal? GetById(int id);
    void Add(Goal goal);
    void Update(Goal goal);
    void Delete(int id);
}
