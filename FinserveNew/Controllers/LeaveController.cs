using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;

namespace FinserveNew.Controllers
{
    // [Authorize] // will uncomment for RBAC later
    public class LeavesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LeavesController> _logger;

        public LeavesController(AppDbContext context, ILogger<LeavesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ================== GET ALL LEAVES ==================
        public async Task<IActionResult> LeaveRecords()
        {
            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            // Set leave balances (you would normally calculate these from the database)
            ViewBag.AnnualLeaveBalance = 10;
            ViewBag.MedicalLeaveBalance = 5;
            ViewBag.HospitalizationLeaveBalance = 16;

            return View(leaves);
        }

        // ================== SHOW LEAVE DETAILS ==================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            return View(leave);
        }

        // ================== SHOW CREATE LEAVE FORM ==================
        public IActionResult Create()
        {
            _logger.LogInformation("📋 Loading Create Leave form");

            // Set hardcoded leave types
            PopulateLeaveTypeDropdown();

            // Set leave balances for the form to display
            ViewBag.AnnualLeaveBalance = 10;
            ViewBag.MedicalLeaveBalance = 5;
            ViewBag.HospitalizationLeaveBalance = 16;

            _logger.LogInformation("✅ Create Leave form loaded successfully");
            return View();
        }

        // ================== CREATE LEAVE (POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveModel leave)
        {
            _logger.LogInformation("🚀 CREATE LEAVE POST started");
            _logger.LogInformation($"📝 Received leave data: Type={leave.LeaveTypeID}, Start={leave.StartDate}, End={leave.EndDate}");

            try
            {
                // Validate leave type first
                if (!IsValidLeaveType(leave.LeaveTypeID))
                {
                    ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
                    _logger.LogWarning($"❌ Invalid leave type: {leave.LeaveTypeID}");
                }

                leave.EmployeeID = "E001";
                if (ModelState.IsValid)
                {
                    _logger.LogInformation("✅ Model is valid, proceeding with save");

                    // Set default values
                    leave.Status = "Pending";
                    

                    // TODO: Set employee ID when authentication is implemented
                    // leave.EmployeeID = GetCurrentEmployeeId();

                    _context.Leaves.Add(leave);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Leave saved successfully with ID: {leave.LeaveID}");

                    TempData["Success"] = "Leave application submitted successfully!";
                    return RedirectToAction(nameof(LeaveRecords));
                }
                else
                {
                    _logger.LogWarning("❌ Model validation failed");

                    foreach (var error in ModelState)
                    {
                        foreach (var modelError in error.Value.Errors)
                        {
                            _logger.LogWarning($"❌ Validation error for {error.Key}: {modelError.ErrorMessage}");
                        }
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "💥 Database error while saving leave");
                ModelState.AddModelError("", "An error occurred while saving the leave. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error while creating leave");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            _logger.LogInformation("🔄 Redisplaying form due to errors");
            PopulateLeaveTypeDropdown();

            // Restore ViewBag data for form redisplay
            ViewBag.AnnualLeaveBalance = 10;
            ViewBag.MedicalLeaveBalance = 5;
            ViewBag.HospitalizationLeaveBalance = 16;

            return View(leave);
        }

        // ================== SHOW EDIT LEAVE FORM ==================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var leave = await _context.Leaves.FindAsync(id);
            if (leave == null)
                return NotFound();

            PopulateLeaveTypeDropdown();

            // Set leave balances for editing
            ViewBag.AnnualLeaveBalance = 10;
            ViewBag.MedicalLeaveBalance = 5;
            ViewBag.HospitalizationLeaveBalance = 16;

            return View(leave);
        }

        // ================== UPDATE LEAVE (POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeaveModel leave)
        {
            if (id != leave.LeaveID)
                return NotFound();

            // Validate leave type
            if (!IsValidLeaveType(leave.LeaveTypeID))
            {
                ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(leave);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Leave updated successfully!";
                    return RedirectToAction(nameof(LeaveRecords));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveExists(leave.LeaveID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while updating leave");
                    ModelState.AddModelError("", "An error occurred while updating the leave.");
                }
            }

            PopulateLeaveTypeDropdown();

            // Restore ViewBag data
            ViewBag.AnnualLeaveBalance = 10;
            ViewBag.MedicalLeaveBalance = 5;
            ViewBag.HospitalizationLeaveBalance = 16;

            return View(leave);
        }

        // ================== SHOW DELETE CONFIRMATION ==================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
                return NotFound();

            return View(leave);
        }

        // ================== DELETE LEAVE (POST) ==================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var leave = await _context.Leaves.FindAsync(id);
            if (leave != null)
            {
                _context.Leaves.Remove(leave);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Leave deleted successfully!";
            }

            return RedirectToAction(nameof(LeaveRecords));
        }

        // ================== HARDCODED LEAVE TYPES - NO DATABASE NEEDED ==================
        private void PopulateLeaveTypeDropdown()
        {
            _logger.LogInformation("🔧 Setting up hardcoded leave types");

            ViewData["LeaveTypeID"] = new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Annual Leave" },
                new SelectListItem { Value = "2", Text = "Medical Leave" },
                new SelectListItem { Value = "3", Text = "Hospitalization Leave" }
            };
        }

        // ================== VALIDATE LEAVE TYPE ==================
        private bool IsValidLeaveType(int leaveTypeId)
        {
            return leaveTypeId >= 1 && leaveTypeId <= 3;
        }

        // ================== GET LEAVE TYPE NAME ==================
        private string GetLeaveTypeName(int leaveTypeId)
        {
            return leaveTypeId switch
            {
                1 => "Annual Leave",
                2 => "Medical Leave",
                3 => "Hospitalization Leave",
                _ => "Unknown"
            };
        }

        // ================== HELPER: CHECK IF LEAVE EXISTS ==================
        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.LeaveID == id);
        }

        // ================== API ENDPOINT FOR BALANCE CHECK ==================
        [HttpGet]
        public IActionResult GetLeaveBalance(int leaveTypeId)
        {
            try
            {
                if (!IsValidLeaveType(leaveTypeId))
                {
                    return Json(new { success = false, message = "Invalid leave type" });
                }

                var balances = new Dictionary<int, object>
                {
                    { 1, new { maxDays = 14, remaining = 10, name = "Annual Leave" } },
                    { 2, new { maxDays = 10, remaining = 5, name = "Medical Leave" } },
                    { 3, new { maxDays = 16, remaining = 16, name = "Hospitalization Leave" } }
                };

                return Json(new { success = true, data = balances[leaveTypeId] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting leave balance for type {leaveTypeId}");
                return Json(new { success = false, message = "Error retrieving balance" });
            }
        }
    }
}