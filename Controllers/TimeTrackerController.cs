using EmployeeCrudApp.Models;
using EmployeeCrudApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EmployeeCrudApp.Controllers
{
    public class TimeTrackerController : Controller
    {
        private readonly ITimeTrackerRepository _timeTrackerRepository;

        public TimeTrackerController(ITimeTrackerRepository timeTrackerRepository)
        {
            _timeTrackerRepository = timeTrackerRepository;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.Name) ?? "";
            var entries = _timeTrackerRepository.GetAll(userId);

            var sortedEntries = entries.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ToList();
            return View(sortedEntries);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,User,Private")]
        public IActionResult SaveEntry([FromBody] TimeEntry entry)
        {
            var userId = User.FindFirstValue(ClaimTypes.Name) ?? "";
            entry.UserId = userId;
            // No backend date overrides: Use exactly what the browser sent
            _timeTrackerRepository.Add(entry);
            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,User,Private")]
        public IActionResult UpdateEntry([FromBody] TimeEntry entry)
        {
            var userId = User.FindFirstValue(ClaimTypes.Name) ?? "";
            var existing = _timeTrackerRepository.GetById(entry.Id, userId);
            if (existing == null) return NotFound();

            existing.TaskName = entry.TaskName;
            existing.DurationInSeconds = entry.DurationInSeconds;

            // Recalculate EndTime based on StartTime and new duration
            existing.EndTime = existing.StartTime.AddSeconds(entry.DurationInSeconds);
            _timeTrackerRepository.Update(existing, userId);
            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,User,Private")]
        public IActionResult DeleteEntry(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.Name) ?? "";
            _timeTrackerRepository.Delete(id, userId);
            return Json(new { success = true });
        }
    }
}
