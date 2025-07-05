using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;

namespace FinserveNew.Controllers
{
    //[Authorize(Roles = "Employee")]
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
                .Include(l => l.LeaveType) // Include leave type for display
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            // Calculate dynamic leave balances
            var currentEmployeeId = "E001"; // TODO: Get from authentication
            var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);

            ViewBag.LeaveBalances = leaveBalances;

            return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", leaves);
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
                .Include(l => l.LeaveType) // Include leave type for display
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            return View(leave);
        }

        // ================== SHOW CREATE LEAVE FORM ==================
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("📋 Loading Create Leave form");

            // Load leave types from database
            await PopulateLeaveTypeDropdownAsync();

            // Calculate dynamic leave balances
            var currentEmployeeId = "E001"; // TODO: Get from authentication
            var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);

            ViewBag.LeaveBalances = leaveBalances;

            _logger.LogInformation("✅ Create Leave form loaded successfully");
            return View("~/Views/Employee/Leaves/Create.cshtml");
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
                // Set EmployeeID and clear any validation error for it
                leave.EmployeeID = "E001";
                ModelState.Remove("EmployeeID");

                // Validate leave type exists in database
                if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
                {
                    ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
                    _logger.LogWarning($"❌ Invalid leave type: {leave.LeaveTypeID}");
                }

                // Validate leave balance
                var currentYear = DateTime.Now.Year;
                var leaveDays = leave.LeaveDays;
                var hasBalance = await HasSufficientLeaveBalanceAsync(leave.EmployeeID, leave.LeaveTypeID, leaveDays, currentYear);

                if (!hasBalance)
                {
                    var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
                    var balance = await GetRemainingLeaveBalanceAsync(leave.EmployeeID, leave.LeaveTypeID, currentYear);
                    ModelState.AddModelError("", $"Insufficient {leaveType?.TypeName} balance. You have {balance} days remaining but requested {leaveDays} days.");
                }

                if (ModelState.IsValid)
                {
                    _logger.LogInformation("✅ Model is valid, proceeding with save");

                    // Set default values
                    leave.Status = "Pending";

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
            await PopulateLeaveTypeDropdownAsync();

            // Recalculate leave balances for form redisplay
            var currentEmployeeId = "E001";
            var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);
            ViewBag.LeaveBalances = leaveBalances;

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

            await PopulateLeaveTypeDropdownAsync();

            // Calculate dynamic leave balances
            var currentEmployeeId = "E001";
            var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);
            ViewBag.LeaveBalances = leaveBalances;

            return View("~/Views/Employee/Leaves/Edit.cshtml", leave);
        }

        // ================== UPDATE LEAVE (POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeaveModel leave)
        {
            if (id != leave.LeaveID)
                return NotFound();

            // Validate leave type exists in database
            if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
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

            await PopulateLeaveTypeDropdownAsync();

            // Recalculate leave balances
            var currentEmployeeId = "E001";
            var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);
            ViewBag.LeaveBalances = leaveBalances;

            return View(leave);
        }

        // ================== SHOW DELETE CONFIRMATION ==================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
                return NotFound();

            return View("~/Views/Employee/Leaves/Delete.cshtml", leave);
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

        // ================== CALCULATE DYNAMIC LEAVE BALANCES ==================
        private async Task<Dictionary<string, object>> CalculateLeaveBalancesAsync(string employeeId, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            _logger.LogInformation($"🧮 Calculating leave balances for employee {employeeId} for year {year}");

            var leaveBalances = new Dictionary<string, object>();

            try
            {
                // Get all leave types with their default allocations
                var leaveTypes = await _context.LeaveTypes.ToListAsync();
                _logger.LogInformation($"📋 Found {leaveTypes.Count} leave types in database");

                foreach (var leaveType in leaveTypes)
                {
                    // FIXED: Calculate total approved/pending leave days for this employee, leave type, and year
                    // Using DateOnly.DayNumber for proper calculation
                    var usedDays = await _context.Leaves
                        .Where(l => l.EmployeeID == employeeId
                                && l.LeaveTypeID == leaveType.LeaveTypeID
                                && l.StartDate.Year == year
                                && (l.Status == "Approved" || l.Status == "Pending"))
                        .SumAsync(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

                    var remainingDays = leaveType.DefaultDaysPerYear - usedDays;

                    leaveBalances[leaveType.TypeName] = new
                    {
                        LeaveTypeID = leaveType.LeaveTypeID,
                        TypeName = leaveType.TypeName,
                        DefaultDays = leaveType.DefaultDaysPerYear,
                        UsedDays = usedDays,
                        RemainingDays = Math.Max(0, remainingDays) // Ensure not negative
                    };

                    _logger.LogInformation($"📊 {leaveType.TypeName}: {remainingDays}/{leaveType.DefaultDaysPerYear} remaining (Used: {usedDays})");
                }

                if (!leaveTypes.Any())
                {
                    _logger.LogWarning("⚠️ No leave types found in database - this might be the root cause of the issue");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating leave balances");
            }

            return leaveBalances;
        }

        // ================== CHECK IF EMPLOYEE HAS SUFFICIENT LEAVE BALANCE ==================
        private async Task<bool> HasSufficientLeaveBalanceAsync(string employeeId, int leaveTypeId, int requestedDays, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return false;

            // FIXED: Use DateOnly.DayNumber for proper calculation
            var usedDays = await _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"))
                .SumAsync(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

            var remainingDays = leaveType.DefaultDaysPerYear - usedDays;

            return remainingDays >= requestedDays;
        }

        // ================== GET REMAINING LEAVE BALANCE ==================
        private async Task<int> GetRemainingLeaveBalanceAsync(string employeeId, int leaveTypeId, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return 0;

            // FIXED: Use DateOnly.DayNumber for proper calculation
            var usedDays = await _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"))
                .SumAsync(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

            return Math.Max(0, leaveType.DefaultDaysPerYear - usedDays);
        }

        // ================== LOAD LEAVE TYPES FROM DATABASE ==================
        private async Task PopulateLeaveTypeDropdownAsync()
        {
            _logger.LogInformation("🔧 Loading leave types from database");

            try
            {
                // FIXED: Add more detailed logging to debug the issue
                var leaveTypesQuery = _context.LeaveTypes.OrderBy(lt => lt.TypeName);
                _logger.LogInformation($"🔍 Executing query: {leaveTypesQuery.ToQueryString()}");

                var leaveTypes = await leaveTypesQuery
                    .Select(lt => new SelectListItem
                    {
                        Value = lt.LeaveTypeID.ToString(),
                        Text = lt.TypeName
                    })
                    .ToListAsync();

                _logger.LogInformation($"📊 Query returned {leaveTypes.Count} leave types");

                if (!leaveTypes.Any())
                {
                    _logger.LogWarning("⚠️ No leave types found in database");

                    // FIXED: Add a default option instead of showing "No leave types available"
                    leaveTypes.Add(new SelectListItem { Value = "", Text = "-- Select Leave Type --", Disabled = true });

                    // Log more details for debugging
                    var totalCount = await _context.LeaveTypes.CountAsync();
                    _logger.LogWarning($"⚠️ Total leave types in database: {totalCount}");
                }
                else
                {
                    // Add a default "Please select" option at the top
                    leaveTypes.Insert(0, new SelectListItem { Value = "", Text = "-- Select Leave Type --" });
                }

                ViewData["LeaveTypeID"] = leaveTypes;
                _logger.LogInformation($"✅ Loaded {leaveTypes.Count} leave types from database");

                // Debug: Log each leave type
                foreach (var item in leaveTypes)
                {
                    _logger.LogInformation($"📋 Leave Type: {item.Text} (Value: {item.Value})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading leave types from database");
                ViewData["LeaveTypeID"] = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Error loading leave types" }
                };
            }
        }

        // ================== VALIDATE LEAVE TYPE EXISTS IN DATABASE ==================
        private async Task<bool> IsValidLeaveTypeAsync(int leaveTypeId)
        {
            try
            {
                var exists = await _context.LeaveTypes.AnyAsync(lt => lt.LeaveTypeID == leaveTypeId);
                _logger.LogInformation($"🔍 Leave type {leaveTypeId} exists: {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating leave type {leaveTypeId}");
                return false;
            }
        }

        // ================== HELPER: CHECK IF LEAVE EXISTS ==================
        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.LeaveID == id);
        }

        // ================== API ENDPOINT FOR DYNAMIC BALANCE CHECK ==================
        [HttpGet]
        public async Task<IActionResult> GetLeaveBalance(int leaveTypeId)
        {
            try
            {
                if (!await IsValidLeaveTypeAsync(leaveTypeId))
                {
                    return Json(new { success = false, message = "Invalid leave type" });
                }

                var currentEmployeeId = "E001"; // TODO: Get from authentication
                var currentYear = DateTime.Now.Year;

                var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
                if (leaveType == null)
                {
                    return Json(new { success = false, message = "Leave type not found" });
                }

                var remainingDays = await GetRemainingLeaveBalanceAsync(currentEmployeeId, leaveTypeId, currentYear);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        maxDays = leaveType.DefaultDaysPerYear,
                        remaining = remainingDays,
                        name = leaveType.TypeName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting leave balance for type {leaveTypeId}");
                return Json(new { success = false, message = "Error retrieving balance" });
            }
        }

        // ================== DEBUGGING ENDPOINT - REMOVE IN PRODUCTION ==================
        [HttpGet]
        public async Task<IActionResult> DebugLeaveTypes()
        {
            try
            {
                var leaveTypes = await _context.LeaveTypes.ToListAsync();
                return Json(new
                {
                    count = leaveTypes.Count,
                    types = leaveTypes.Select(lt => new
                    {
                        id = lt.LeaveTypeID,
                        name = lt.TypeName,
                        defaultDays = lt.DefaultDaysPerYear
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}