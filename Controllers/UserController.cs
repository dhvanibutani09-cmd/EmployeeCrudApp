using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeCrudApp.Services;
using EmployeeCrudApp.Models;

namespace EmployeeCrudApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly Microsoft.Extensions.Localization.IStringLocalizer<UserController> _localizer;
        private readonly IConfiguration _configuration;
        private readonly IWidgetRepository _widgetRepository;
        private readonly IRoleRepository _roleRepository;

        public UserController(IUserRepository userRepository, IEmailService emailService, Microsoft.Extensions.Localization.IStringLocalizer<UserController> localizer, IConfiguration configuration, IWidgetRepository widgetRepository, IRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _localizer = localizer;
            _configuration = configuration;
            _widgetRepository = widgetRepository;
            _roleRepository = roleRepository;
        }

        public IActionResult Index(string sortOrder, string currentFilter, string searchString, string filter = null)
        {
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.EmailSortParm = sortOrder == "Email" ? "email_desc" : "Email";
            ViewBag.LoginSortParm = sortOrder == "Login" ? "login_desc" : "Login";
            ViewBag.DateSortParm = sortOrder == "Date" ? "date_desc" : "Date";
            ViewBag.RoleSortParm = sortOrder == "Role" ? "role_desc" : "Role";

            if (searchString != null)
            {
                // pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;
            ViewBag.FilterType = filter;

            var users = _userRepository.GetAll();

            if (!User.IsInRole("Admin"))
            {
                users = users.Where(u => u.Role != "Private");
            }

            if (filter == "today")
            {
                users = users.Where(u => u.LastLoginDate.HasValue && u.LastLoginDate.Value.Date == DateTime.Today);
            }

            if (!String.IsNullOrEmpty(searchString))
            {
                users = users.Where(s => s.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                                       || s.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    users = users.OrderByDescending(s => s.Name);
                    break;
                case "Email":
                    users = users.OrderBy(s => s.Email);
                    break;
                case "email_desc":
                    users = users.OrderByDescending(s => s.Email);
                    break;
                case "Login":
                    users = users.OrderBy(s => s.LoginCount);
                    break;
                case "login_desc":
                    users = users.OrderByDescending(s => s.LoginCount);
                    break;
                case "Date":
                    users = users.OrderBy(s => s.LastLoginDate);
                    break;
                case "date_desc":
                    users = users.OrderByDescending(s => s.LastLoginDate);
                    break;
                case "Role":
                    users = users.OrderBy(s => s.Role);
                    break;
                case "role_desc":
                    users = users.OrderByDescending(s => s.Role);
                    break;
                default:
                    users = users.OrderBy(s => s.Name);
                    break;
            }

            return View(users.ToList());
        }

        // Keep UserList for backward compatibility or redirect it
        public IActionResult UserList()
        {
            return RedirectToAction(nameof(Index));
        }

        // Keep AdminList for backward compatibility or redirect it
        public IActionResult AdminList()
        {
            return RedirectToAction(nameof(Index));
        }

        // Add User (Normal User)
        public IActionResult Create()
        {
            ViewBag.Role = string.Empty;
            ViewBag.AllWidgets = _widgetRepository.GetAll().Select(w => w.Name).ToList();
            return View();
        }

        // Add Admin
        public IActionResult CreateAdmin()
        {
            ViewBag.Role = "Admin";
            return View("Create"); // Reuse Create view
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            // Redirect to Index where the Modal handles creation
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult VerifyAddUserOtp()
        {
            if (!TempData.ContainsKey("PendingUser"))
            {
                return RedirectToAction(nameof(UserList));
            }
            
            var userJson = TempData["PendingUser"] as string;
            if (!string.IsNullOrEmpty(userJson))
            {
                var user = System.Text.Json.JsonSerializer.Deserialize<User>(userJson);
                ViewBag.Role = user?.Role;
            }

            TempData.Keep("PendingUser");
            return View();
        }

        [HttpPost]
        public IActionResult VerifyAddUserOtp(string otp)
        {
            if (TempData.ContainsKey("PendingUser"))
            {
                var userJson = TempData["PendingUser"] as string;
                if (!string.IsNullOrEmpty(userJson))
                {
                    var user = System.Text.Json.JsonSerializer.Deserialize<User>(userJson);
                    
                    if (user != null && user.Otp == otp && user.OtpExpiry > DateTime.Now)
                    {
                        // OTP Verified
                        user.IsEmailVerified = true;
                        user.Otp = null;
                        user.OtpExpiry = null;
                        
                        // Role inheritance is handled by RoleId in repository
                        // Link RoleId if possible based on Role name for legacy support
                        var role = _roleRepository.GetByName(user.Role);
                        if (role != null)
                        {
                            user.RoleId = role.Id;
                        }

                        // Set Lock Flags
                        user.RoleLocked = true;
                        user.WidgetLocked = true;
                        
                        _userRepository.Add(user);
                        
                        return RedirectToAction(nameof(Index));
                    }
                }
                TempData.Keep("PendingUser"); // Keep data for retry
            }
            
            ModelState.AddModelError(string.Empty, _localizer["Invalid or expired OTP provided."]);
            return View();
        }

        public IActionResult Edit(int id)
        {
            var user = _userRepository.GetById(id);
            if (user == null) return NotFound();
            user.IsEditable = true;
            ViewBag.AllWidgets = _widgetRepository.GetAll().Select(w => w.Name).ToList();
            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(int id, User user)
        {
            var existingUser = _userRepository.GetById(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            // Remove Password validation if it's empty (meaning no change)
            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            if (ModelState.IsValid)
            {
                // Role and Widget Lock System Check
                var submittedWidgets = user.PermittedWidgets ?? new List<string>();
                var existingWidgets = existingUser.PermittedWidgets ?? new List<string>();
                
                bool roleChanged = existingUser.Role != user.Role;
                bool widgetsChanged = !submittedWidgets.OrderBy(w => w).SequenceEqual(existingWidgets.OrderBy(w => w));

                if (roleChanged || widgetsChanged)
                {
                    var currentUserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                    var adminEmails = _configuration.GetSection("AdminSettings:AdminEmails").Get<List<string>>();
                    bool isSuperAdmin = adminEmails != null && !string.IsNullOrEmpty(currentUserEmail) && adminEmails.Contains(currentUserEmail);

                    // Allow if it's from the User List Edit (IsEditable is true) OR if it's a Super Admin
                    if (!user.IsEditable && !isSuperAdmin)
                    {
                        ModelState.AddModelError("Role", _localizer["Role and widget permissions cannot be modified from this source."]);
                        ViewBag.AllWidgets = _widgetRepository.GetAll().Select(w => w.Name).ToList();
                        return View(user);
                    }
                }

                // Role and Widget update is handled by RoleId inheritance
                // Update editable fields

                // Update editable fields
                existingUser.Name = user.Name;
                existingUser.Email = user.Email;
                existingUser.SecurityPin = user.SecurityPin;
                existingUser.IsEmailVerified = user.IsEmailVerified;
                
                // Update Role link
                if (existingUser.RoleId != user.RoleId)
                {
                    existingUser.RoleId = user.RoleId;
                    // Reset permitted widgets to new role defaults if role changes
                    existingUser.PermittedWidgets = null; 
                }

                // Update password only if provided
                if (!string.IsNullOrWhiteSpace(user.Password))
                {
                    existingUser.Password = user.Password;
                    existingUser.ConfirmPassword = user.ConfirmPassword;
                }

                _userRepository.Update(existingUser);
                return RedirectToAction(nameof(Index));
            }

            // If we are here, something failed, redisplay form.
            // Be sure to pass back the original properties that might not be in the form if needed, 
            // but here 'user' object from basic binding should be enough for the view to re-render errors.
            return View(user);
        }

        public IActionResult Delete(int id)
        {
            var user = _userRepository.GetById(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var user = _userRepository.GetById(id);
             string role = user?.Role ?? "User";

            _userRepository.Delete(id);
            
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Details(int id)
        {
            var user = _userRepository.GetById(id);
            if (user == null) return NotFound();
            return View(user);
        }

        public IActionResult GetLoginHistory(int id, string filter = "all")
        {
            var user = _userRepository.GetById(id);
            if (user == null) return NotFound();

            var history = user.LoginHistory.OrderByDescending(d => d).AsEnumerable();

            if (filter == "7days")
            {
                history = history.Where(d => d >= DateTime.Now.AddDays(-7));
            }
            else if (filter == "30days")
            {
                history = history.Where(d => d >= DateTime.Now.AddDays(-30));
            }

            return PartialView("_LoginHistoryPartial", history.ToList());
        }

        [HttpGet]
        public IActionResult GetRolePermissions()
        {
            var roles = _roleRepository.GetAll();
            return PartialView("_RolePermissionsModal", roles);
        }

        [HttpPost]
        public IActionResult UpdateRolePermissions([FromBody] List<Role> roles)
        {
            try
            {
                foreach (var role in roles)
                {
                    var existingRole = _roleRepository.GetByName(role.Name);
                    if (existingRole != null)
                    {
                        existingRole.CanViewUsers = role.CanViewUsers;
                        existingRole.CanAddUser = role.CanAddUser;
                        existingRole.CanEditUser = role.CanEditUser;
                        existingRole.CanDeleteUser = role.CanDeleteUser;
                        existingRole.CanAccessDashboard = role.CanAccessDashboard;
                        existingRole.CanAccessWidgets = role.CanAccessWidgets;
                        existingRole.CanAccessSettings = role.CanAccessSettings;
                        _roleRepository.Update(existingRole);
                    }
                }
                return Json(new { success = true, message = _localizer["Permissions updated successfully."].Value });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetRoles()
        {
            var roles = _roleRepository.GetAll();
            return Json(roles.Select(r => new { 
                id = r.Id, 
                name = r.Name, 
                defaultWidgets = r.PermittedWidgets 
            }));
        }

        [HttpGet]
        public IActionResult GetAvailableWidgets()
        {
            var widgets = _widgetRepository.GetAll().Select(w => w.Name).ToList();
            return Json(widgets);
        }

        [HttpPost]
        public IActionResult CreateAjax([FromBody] User user)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(user.Name) || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
                {
                    return Json(new { success = false, message = _localizer["All fields are required."].Value });
                }

                var existingUser = _userRepository.GetByEmail(user.Email);
                if (existingUser != null)
                {
                    return Json(new { success = false, message = _localizer["Email already exists."].Value });
                }

                // Simplified creation for internal use
                user.IsEmailVerified = true;
                user.CreatedDate = DateTime.Now;
                user.RoleLocked = true;
                user.WidgetLocked = true;
                
                // Fetch the role's name and force role permissions
                var role = _roleRepository.GetAll().FirstOrDefault(r => r.Id == user.RoleId);
                if (role == null)
                {
                    return Json(new { success = false, message = _localizer["Invalid role selected."].Value });
                }

                user.Role = role.Name;
                user.PermittedWidgets = role.PermittedWidgets; // Sync initially for verification

                _userRepository.Add(user);
                return Json(new { success = true, message = _localizer["User created successfully."].Value });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult RolePermissions()
        {
            var roles = _roleRepository.GetAll().ToList();
            var widgets = _widgetRepository.GetAll().Select(w => w.Name).ToList();

            var viewModel = new RolePermissionsViewModel
            {
                AllAvailableWidgets = widgets,
                Roles = roles.Select(r => new RoleInfo
                {
                    RoleId = r.Id,
                    RoleName = r.Name,
                    PermittedWidgets = r.PermittedWidgets
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult SaveRolePermissions([FromBody] List<RoleUpdateModel> roleUpdates)
        {
            try
            {
                if (roleUpdates == null || !roleUpdates.Any())
                    return Json(new { success = false, message = "No updates provided." });

                foreach (var update in roleUpdates)
                {
                    var role = _roleRepository.GetAll().FirstOrDefault(r => r.Id == update.RoleId);
                    if (role != null)
                    {
                        role.PermittedWidgets = update.PermittedWidgets ?? new List<string>();
                        _roleRepository.Update(role);
                    }
                }

                return Json(new { success = true, message = _localizer["Role permissions updated successfully."].Value });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class RoleUpdateModel
        {
            public int RoleId { get; set; }
            public List<string> PermittedWidgets { get; set; }
        }

        private List<string> GetAuthorizedWidgets(string role, List<string> requestedWidgets)
        {
            // Fully dynamic role-based system: trust the submitted widgets for the role
            return requestedWidgets ?? new List<string>();
        }
    }
}
