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

        public UserController(IUserRepository userRepository, IEmailService emailService, Microsoft.Extensions.Localization.IStringLocalizer<UserController> localizer)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _localizer = localizer;
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
            return View();
        }

        // Add Admin
        public IActionResult CreateAdmin()
        {
            ViewBag.Role = "Admin";
            return View("Create"); // Reuse Create view
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user, string role = "User")
        {
            if (ModelState.IsValid)
            {
                var existingUser = _userRepository.GetByEmail(user.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", _localizer["Email already exists."]);
                    ViewBag.Role = role;
                    return View(user);
                }

                // Generate OTP
                var otp = new Random().Next(100000, 999999).ToString();
                
                // Store incomplete user in TempData
                user.IsEmailVerified = false;
                user.Otp = otp;
                user.OtpExpiry = DateTime.Now.AddMinutes(5);
                user.PermittedWidgets = user.PermittedWidgets ?? new List<string>();

                var userJson = System.Text.Json.JsonSerializer.Serialize(user);
                TempData["PendingUser"] = userJson;

                // Send Email
                await _emailService.SendEmailAsync(user.Email, "Complete User Creation", $"Your verification code is: {otp}");
                
                return RedirectToAction(nameof(VerifyAddUserOtp));
            }
            ViewBag.Role = role;
            return View(user);
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
            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(User user)
        {
            if (ModelState.IsValid)
            {
                _userRepository.Update(user);
                return RedirectToAction(nameof(Index));
            }
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
    }
}
