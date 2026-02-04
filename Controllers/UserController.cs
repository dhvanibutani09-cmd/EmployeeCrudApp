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

        public IActionResult UserList(string filter = null)
        {
            var users = _userRepository.GetAll().Where(u => u.Role == "User");

            if (filter == "today")
            {
                users = users.Where(u => u.LastLoginDate.HasValue && u.LastLoginDate.Value.Date == DateTime.Today);
                ViewBag.CurrentFilter = "today";
            }

            return View(users.ToList());
        }

        public IActionResult AdminList(string sortOrder, string currentFilter, string searchString)
        {
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.EmailSortParm = sortOrder == "Email" ? "email_desc" : "Email";
            ViewBag.LoginSortParm = sortOrder == "Login" ? "login_desc" : "Login";
            ViewBag.DateSortParm = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                // pageNumber = 1; // Pagination not implemented yet
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            var admins = _userRepository.GetAll().Where(u => u.Role == "Admin");

            if (!String.IsNullOrEmpty(searchString))
            {
                admins = admins.Where(s => s.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) 
                                       || s.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    admins = admins.OrderByDescending(s => s.Name);
                    break;
                case "Email":
                    admins = admins.OrderBy(s => s.Email);
                    break;
                case "email_desc":
                    admins = admins.OrderByDescending(s => s.Email);
                    break;
                case "Login":
                    admins = admins.OrderBy(s => s.LoginHistory.Count(d => d.Date == DateTime.Today));
                    break;
                case "login_desc":
                    admins = admins.OrderByDescending(s => s.LoginHistory.Count(d => d.Date == DateTime.Today));
                    break;
                case "Date":
                    admins = admins.OrderBy(s => s.LastLoginDate);
                    break;
                case "date_desc":
                    admins = admins.OrderByDescending(s => s.LastLoginDate);
                    break;
                default:
                    admins = admins.OrderBy(s => s.Name);
                    break;
            }

            return View(admins.ToList());
        }

        // Add User (Normal User)
        public IActionResult Create()
        {
            ViewBag.Role = "User";
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
                user.Role = role; // Set role

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
                        
                        if (user.Role == "Admin")
                        {
                            return RedirectToAction(nameof(AdminList));
                        }
                        return RedirectToAction(nameof(UserList));
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
                 if (user.Role == "Admin")
                {
                    return RedirectToAction(nameof(AdminList));
                }
                return RedirectToAction(nameof(UserList));
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
            
            if (role == "Admin")
            {
                return RedirectToAction(nameof(AdminList));
            }
            return RedirectToAction(nameof(UserList));
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
