using EmployeeCrudApp.Models;

namespace EmployeeCrudApp.Services;

public interface IRoleRepository
{
    IEnumerable<Role> GetAll();
    Role? GetByName(string name);
    void Add(Role role);
    void Update(Role role);
}
