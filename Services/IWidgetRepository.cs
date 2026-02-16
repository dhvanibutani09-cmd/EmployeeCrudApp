using EmployeeCrudApp.Models;

namespace EmployeeCrudApp.Services;

public interface IWidgetRepository
{
    IEnumerable<Widget> GetAll();
}
