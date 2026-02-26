using System.Collections.Generic;

namespace EmployeeCrudApp.Models
{
    public class RolePermissionsViewModel
    {
        public List<RoleInfo> Roles { get; set; } = new List<RoleInfo>();
        public List<string> AllAvailableWidgets { get; set; } = new List<string>();
    }

    public class RoleInfo
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<string> PermittedWidgets { get; set; } = new List<string>();
    }
}
