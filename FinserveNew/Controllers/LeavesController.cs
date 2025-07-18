using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FinserveNew.Controllers
{
    public class LeavesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LeavesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeavesController(AppDbContext context, ILogger<LeavesController> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // ================== HELPER METHOD TO GET EMPLOYEE ID ==================
        private async Task<string> GetCurrentEmployeeId()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            _logger.LogInformation($"Current user: {currentUser.UserName}, Email: {currentUser.Email}");

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Username == currentUser.UserName || e.Email == currentUser.Email);

            if (employee == null)
            {
                _logger.LogWarning($"No employee found for username: {currentUser.UserName} or email: {currentUser.Email}");
            }

            return employee?.EmployeeID;
        }

        // ================== EMPLOYEE ACTIONS ==================

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> LeaveRecords()
        {
            // Get current employee ID
            var employeeId = await GetCurrentEmployeeId();

            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", new List<LeaveModel>());
            }

            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeID == employeeId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            // Calculate dynamic leave balances
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

            ViewBag.LeaveBalances = leaveBalances;

            // Populate individual ViewBag properties for the view
            PopulateLeaveBalanceViewBag(leaveBalances);

            return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", leaves);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave == null)
            {
                return NotFound();
            }

            return View("~/Views/Employee/Leaves/Details.cshtml", leave);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("📋 Loading Create Leave form");

            await PopulateLeaveTypeDropdownAsync();

            // Get current employee ID
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/Create.cshtml");
            }

            // Calculate dynamic leave balances
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            _logger.LogInformation("✅ Create Leave form loaded successfully");
            return View("~/Views/Employee/Leaves/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(LeaveModel leave)
        {
            _logger.LogInformation("🚀 CREATE LEAVE POST started");
            _logger.LogInformation($"📝 Received leave data: Type={leave.LeaveTypeID}, Start={leave.StartDate}, End={leave.EndDate}");

            try
            {
                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();
                if (string.IsNullOrEmpty(employeeId))
                {
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateLeaveTypeDropdownAsync();
                    return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                }

                // Set EmployeeID and clear any validation error for it
                leave.EmployeeID = employeeId;
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
                    leave.CreatedDate = DateTime.Now;
                    leave.SubmissionDate = DateTime.Now;

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
            var currentEmployeeId = await GetCurrentEmployeeId();
            if (!string.IsNullOrEmpty(currentEmployeeId))
            {
                var leaveBalances = await CalculateLeaveBalancesAsync(currentEmployeeId);
                ViewBag.LeaveBalances = leaveBalances;
                PopulateLeaveBalanceViewBag(leaveBalances);
            }

            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var leave = await _context.Leaves.FindAsync(id);
            if (leave == null || leave.EmployeeID != employeeId)
                return NotFound();

            // Only allow editing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "You can only edit leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            await PopulateLeaveTypeDropdownAsync();

            // Calculate dynamic leave balances
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            return View("~/Views/Employee/Leaves/Edit.cshtml", leave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int id, LeaveModel leave)
        {
            if (id != leave.LeaveID)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var leaveToUpdate = await _context.Leaves.FindAsync(id);
            if (leaveToUpdate == null || leaveToUpdate.EmployeeID != employeeId)
                return NotFound();

            // Only allow editing if status is Pending
            if (leaveToUpdate.Status != "Pending")
            {
                TempData["Error"] = "You can only edit leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            // Validate leave type exists in database
            if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
            {
                ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
            }

            // Validate leave balance for updated leave
            var currentYear = DateTime.Now.Year;
            var leaveDays = leave.LeaveDays;

            // Calculate balance excluding the current leave being edited
            var hasBalance = await HasSufficientLeaveBalanceAsync(employeeId, leave.LeaveTypeID, leaveDays, currentYear, excludeLeaveId: id);

            if (!hasBalance)
            {
                var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
                var balance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear, excludeLeaveId: id);
                ModelState.AddModelError("", $"Insufficient {leaveType?.TypeName} balance. You have {balance} days remaining but requested {leaveDays} days.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update the leave properties
                    leaveToUpdate.LeaveTypeID = leave.LeaveTypeID;
                    leaveToUpdate.StartDate = leave.StartDate;
                    leaveToUpdate.EndDate = leave.EndDate;
                    leaveToUpdate.LeaveDays = leave.LeaveDays;
                    leaveToUpdate.Description = leave.Description;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Leave updated successfully!";
                    return RedirectToAction(nameof(LeaveRecords));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveExists(leaveToUpdate.LeaveID))
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
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave == null)
                return NotFound();

            // Only allow deletion if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "You can only delete leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            return View("~/Views/Employee/Leaves/Delete.cshtml", leave);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return RedirectToAction(nameof(LeaveRecords));

            var leave = await _context.Leaves.FindAsync(id);

            if (leave != null && leave.EmployeeID == employeeId)
            {
                // Only allow deletion if status is Pending
                if (leave.Status != "Pending")
                {
                    TempData["Error"] = "You can only delete leaves that are in Pending status.";
                    return RedirectToAction(nameof(LeaveRecords));
                }

                _context.Leaves.Remove(leave);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Leave deleted successfully!";
            }

            return RedirectToAction(nameof(LeaveRecords));
        }

        // reflect leave balance in Dashboard

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();
                if (string.IsNullOrEmpty(employeeId))
                {
                    TempData["Error"] = "Employee record not found.";
                    return View("~/Views/Employee/Dashboard.cshtml");
                }

                var currentYear = DateTime.Now.Year;

                // Calculate leave balances - USE THE SAME METHOD AS LeaveRecords
                var leaveBalances = await CalculateLeaveBalancesAsync(employeeId, currentYear);

                // Calculate total days used and remaining
                var totalDaysUsed = 0;
                var totalRemainingDays = 0;
                var totalDefaultDays = 0;

                foreach (var balance in leaveBalances)
                {
                    var leaveBalance = balance.Value as dynamic;
                    if (leaveBalance != null)
                    {
                        totalDaysUsed += leaveBalance.UsedDays;
                        totalRemainingDays += leaveBalance.RemainingDays;
                        totalDefaultDays += leaveBalance.DefaultDays;
                    }
                }

                // Get recent leave applications
                var recentLeaves = await _context.Leaves
                    .Include(l => l.Employee)
                    .Include(l => l.LeaveType)
                    .Where(l => l.EmployeeID == employeeId)
                    .OrderByDescending(l => l.CreatedDate)
                    .Take(5)
                    .ToListAsync();

                // Get pending requests count
                var pendingRequestsCount = await _context.Leaves
                    .Where(l => l.EmployeeID == employeeId && l.Status == "Pending")
                    .CountAsync();

                // ✅ ADD MISSING CLAIMS DATA
                var claims = await _context.Claims
                    .Where(c => c.EmployeeID == employeeId)
                    .ToListAsync();

                var recentClaims = claims
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(5)
                    .ToList();

                var totalClaimsCount = claims.Count;
                var approvedClaimsCount = claims.Count(c => c.Status == "Approved");
                var pendingClaimsCount = claims.Count(c => c.Status == "Pending");
                var totalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);
                var approvalRate = totalClaimsCount > 0 ? (approvedClaimsCount * 100.0 / totalClaimsCount) : 0;

                // ✅ ENHANCED: Get approved leave dates for ENTIRE YEAR and organize by month
                var approvedLeaves = await _context.Leaves
                    .Where(l => l.EmployeeID == employeeId &&
                               l.Status == "Approved" &&
                               (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear))
                    .ToListAsync();

                // ✅ ENHANCED: Create comprehensive calendar data for entire year
                var calendarData = new Dictionary<string, List<int>>();

                // Initialize all months for current year
                for (int month = 1; month <= 12; month++)
                {
                    var monthKey = $"{currentYear}-{month:D2}";
                    calendarData[monthKey] = new List<int>();
                }

                // Add leave dates to calendar data
                foreach (var leave in approvedLeaves)
                {
                    var startDate = leave.StartDate;
                    var endDate = leave.EndDate;

                    for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    {
                        if (date.Year == currentYear)
                        {
                            var monthKey = $"{date.Year}-{date.Month:D2}";
                            if (calendarData.ContainsKey(monthKey))
                            {
                                calendarData[monthKey].Add(date.Day);
                            }
                        }
                    }
                }

                // Remove duplicates and sort
                foreach (var key in calendarData.Keys.ToList())
                {
                    calendarData[key] = calendarData[key].Distinct().OrderBy(d => d).ToList();
                }

                // ✅ Set ALL ViewBag properties
                ViewBag.LeaveBalances = leaveBalances;
                ViewBag.TotalLeaveDaysUsed = totalDaysUsed;
                ViewBag.TotalRemainingLeave = totalRemainingDays;
                ViewBag.TotalDefaultDays = totalDefaultDays;
                ViewBag.CurrentYear = currentYear;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.RecentLeaves = recentLeaves;
                ViewBag.PendingRequestsCount = pendingRequestsCount;

                // ✅ NEW: Enhanced calendar data
                ViewBag.CalendarData = calendarData;
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;

                // ✅ ADD MISSING CLAIMS ViewBag PROPERTIES
                ViewBag.TotalClaims = totalClaimsCount;
                ViewBag.ApprovedClaims = approvedClaimsCount;
                ViewBag.PendingClaims = pendingClaimsCount;
                ViewBag.ApprovalRate = Math.Round(approvalRate, 1);
                ViewBag.TotalClaimAmount = totalClaimAmount;
                ViewBag.RecentClaims = recentClaims;

                // For leaves specifically
                ViewBag.PendingLeaves = pendingRequestsCount;

                // This ensures the Dashboard uses the same data as LeaveRecords
                PopulateLeaveBalanceViewBag(leaveBalances);

                _logger.LogInformation($"✅ Dashboard loaded successfully for employee {employeeId}");
                return View("~/Views/Employee/Dashboard.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading dashboard");

                // ✅ SET DEFAULT VALUES FOR ALL ViewBag PROPERTIES TO PREVENT NULL ERRORS
                ViewBag.LeaveBalances = new Dictionary<string, object>();
                ViewBag.TotalLeaveDaysUsed = 0;
                ViewBag.TotalRemainingLeave = 0;
                ViewBag.TotalDefaultDays = 0;
                ViewBag.CurrentYear = DateTime.Now.Year;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.RecentLeaves = new List<LeaveModel>();
                ViewBag.PendingRequestsCount = 0;

                // Calendar defaults
                ViewBag.CalendarData = new Dictionary<string, List<int>>();
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;

                // Claims defaults
                ViewBag.TotalClaims = 0;
                ViewBag.ApprovedClaims = 0;
                ViewBag.PendingClaims = 0;
                ViewBag.ApprovalRate = 0;
                ViewBag.TotalClaimAmount = 0;
                ViewBag.RecentClaims = new List<Claim>();
                ViewBag.PendingLeaves = 0;

                TempData["Error"] = "An error occurred while loading the dashboard.";
                return View("~/Views/Employee/Dashboard.cshtml");
            }
        }
        // ================== HR ACTIONS ==================

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveIndex()
        {
            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/LeaveIndex.cshtml", leaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            return View("~/Views/HR/Leaves/LeaveDetails.cshtml", leave);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessLeave(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            // Only allow processing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            return View("~/Views/HR/Leaves/ProcessLeave.cshtml", leave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessLeave(int id, string action, string? remarks)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            // Only allow processing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Update leave based on action
                if (action == "approve")
                {
                    leave.Status = "Approved";
                    leave.ApprovalDate = DateTime.Now;
                    leave.ApprovedBy = currentUser.Id;
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave approved successfully!";
                }
                else if (action == "reject")
                {
                    leave.Status = "Rejected";
                    leave.ApprovalDate = DateTime.Now;
                    leave.ApprovedBy = currentUser.Id;
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave rejected successfully!";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessLeave), new { id = id });
                }

                await _context.SaveChangesAsync();

                // TODO: Send email notification to employee
                // await SendEmailNotification(leave);

                return RedirectToAction(nameof(LeaveIndex));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while processing leave");
                TempData["Error"] = "An error occurred while processing the leave. Please try again.";
                return RedirectToAction(nameof(ProcessLeave), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing leave");
                TempData["Error"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction(nameof(ProcessLeave), new { id = id });
            }
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> PendingLeaves()
        {
            var pendingLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Pending")
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/PendingLeave.cshtml", pendingLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ApprovedLeaves()
        {
            var approvedLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Approved")
                .OrderByDescending(l => l.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/ApprovedLeave.cshtml", approvedLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> RejectedLeaves()
        {
            var rejectedLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Rejected")
                .OrderByDescending(l => l.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/RejectedLeave.cshtml", rejectedLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> EmployeeLeaveBalance(string employeeId = null)
        {
            if (!string.IsNullOrEmpty(employeeId))
            {
                // Get specific employee details
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == employeeId);

                if (employee == null)
                {
                    return NotFound();
                }

                // Use the same method as Dashboard and LeaveRecords
                var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

                // Get all leave applications for this employee
                var employeeLeaveApplications = await _context.Leaves
                    .Include(l => l.LeaveType)
                    .Include(l => l.Employee)
                    .Where(l => l.EmployeeID == employeeId)
                    .OrderByDescending(l => l.SubmissionDate)
                    .ToListAsync();

                ViewBag.SelectedEmployee = employee;
                ViewBag.LeaveBalances = leaveBalances;
                ViewBag.EmployeeLeaveApplications = employeeLeaveApplications;

                return View("~/Views/HR/Leaves/EmployeeLeaveBalance.cshtml");
            }
            else
            {
                // Show all employees - use the same method for consistency
                var employees = await _context.Employees.ToListAsync();
                var allEmployeeBalances = new Dictionary<string, Dictionary<string, object>>();

                foreach (var emp in employees)
                {
                    var balances = await CalculateLeaveBalancesAsync(emp.EmployeeID);
                    allEmployeeBalances[emp.EmployeeID] = balances;
                }

                ViewBag.Employees = employees;
                ViewBag.AllEmployeeBalances = allEmployeeBalances;

                return View("~/Views/HR/Leaves/EmployeeLeaveBalance.cshtml");
            }
        }



        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveReports()
        {
            var currentYear = DateTime.Now.Year;

            // Get leave statistics
            var leaveStats = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.StartDate.Year == currentYear)
                .GroupBy(l => l.LeaveType.TypeName)
                .Select(g => new
                {
                    LeaveType = g.Key,
                    TotalApplications = g.Count(),
                    ApprovedApplications = g.Count(l => l.Status == "Approved"),
                    PendingApplications = g.Count(l => l.Status == "Pending"),
                    RejectedApplications = g.Count(l => l.Status == "Rejected"),
                    TotalDaysUsed = g.Where(l => l.Status == "Approved").Sum(l => l.LeaveDays)
                })
                .ToListAsync();

            // Get employees with high leave usage (approaching limits)
            var allEmployees = await _context.Employees.ToListAsync();
            var employeesNearLimit = new List<object>();

            foreach (var employee in allEmployees)
            {
                var balances = await CalculateLeaveBalancesAsync(employee.EmployeeID);
                foreach (var balance in balances)
                {
                    var balanceInfo = balance.Value as dynamic;
                    if (balanceInfo != null && balanceInfo.RemainingDays <= 5)
                    {
                        employeesNearLimit.Add(new
                        {
                            EmployeeID = employee.EmployeeID,
                            EmployeeName = employee.Username,
                            LeaveType = balance.Key,
                            RemainingDays = balanceInfo.RemainingDays
                        });
                    }
                }
            }

            ViewBag.LeaveStats = leaveStats;
            ViewBag.EmployeesNearLimit = employeesNearLimit;
            ViewBag.CurrentYear = currentYear;

            return View("~/Views/HR/Leaves/LeaveReports.cshtml");
        }

        // ================== HELPER METHODS ==================

        private void PopulateLeaveBalanceViewBag(Dictionary<string, object> leaveBalances)
        {
            _logger.LogInformation("🔧 Populating individual ViewBag properties for leave balances");

            try
            {
                if (leaveBalances.ContainsKey("Annual Leave"))
                {
                    var annualLeave = leaveBalances["Annual Leave"] as dynamic;
                    ViewBag.AnnualLeaveBalance = annualLeave?.RemainingDays ?? 14;
                }
                else
                {
                    ViewBag.AnnualLeaveBalance = 14;
                }

                if (leaveBalances.ContainsKey("Medical Leave"))
                {
                    var medicalLeave = leaveBalances["Medical Leave"] as dynamic;
                    ViewBag.MedicalLeaveBalance = medicalLeave?.RemainingDays ?? 10;
                }
                else
                {
                    ViewBag.MedicalLeaveBalance = 10;
                }

                if (leaveBalances.ContainsKey("Hospitalization Leave"))
                {
                    var hospitalizationLeave = leaveBalances["Hospitalization Leave"] as dynamic;
                    ViewBag.HospitalizationLeaveBalance = hospitalizationLeave?.RemainingDays ?? 16;
                }
                else
                {
                    ViewBag.HospitalizationLeaveBalance = 16;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error populating individual ViewBag properties");
                ViewBag.AnnualLeaveBalance = 14;
                ViewBag.MedicalLeaveBalance = 10;
                ViewBag.HospitalizationLeaveBalance = 16;
            }
        }

        private async Task<Dictionary<string, object>> CalculateLeaveBalancesAsync(string employeeId, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            _logger.LogInformation($"🧮 Calculating leave balances for employee {employeeId} for year {year}");

            var leaveBalances = new Dictionary<string, object>();

            try
            {
                var leaveTypes = await _context.LeaveTypes.ToListAsync();

                foreach (var leaveType in leaveTypes)
                {
                    // Only count APPROVED leaves for actual balance calculation
                    var approvedLeaves = await _context.Leaves
                        .Where(l => l.EmployeeID == employeeId
                                && l.LeaveTypeID == leaveType.LeaveTypeID
                                && l.StartDate.Year == year
                                && l.Status == "Approved") // Only approved leaves
                        .ToListAsync();

                    // For balance checking (when creating/editing), include pending leaves
                    var pendingLeaves = await _context.Leaves
                        .Where(l => l.EmployeeID == employeeId
                                && l.LeaveTypeID == leaveType.LeaveTypeID
                                && l.StartDate.Year == year
                                && l.Status == "Pending")
                        .ToListAsync();

                    var usedDays = 0;
                    foreach (var leave in approvedLeaves)
                    {
                        // Use LeaveDays if available, otherwise calculate from dates
                        var leaveDuration = leave.LeaveDays > 0 ? leave.LeaveDays :
                                          (leave.EndDate.DayNumber - leave.StartDate.DayNumber) + 1;
                        usedDays += leaveDuration;
                    }

                    var pendingDays = 0;
                    foreach (var leave in pendingLeaves)
                    {
                        // Use LeaveDays if available, otherwise calculate from dates
                        var leaveDuration = leave.LeaveDays > 0 ? leave.LeaveDays :
                                          (leave.EndDate.DayNumber - leave.StartDate.DayNumber) + 1;
                        pendingDays += leaveDuration;
                    }

                    var remainingDays = leaveType.DefaultDaysPerYear - usedDays;

                    leaveBalances[leaveType.TypeName] = new
                    {
                        LeaveTypeID = leaveType.LeaveTypeID,
                        TypeName = leaveType.TypeName,
                        DefaultDays = leaveType.DefaultDaysPerYear,
                        UsedDays = usedDays, // Only approved leaves
                        PendingDays = pendingDays, // Pending leaves separately
                        RemainingDays = Math.Max(0, remainingDays)
                    };
                }

                _logger.LogInformation($"🧮 Calculated balances: {string.Join(", ", leaveBalances.Select(b => $"{b.Key}: {((dynamic)b.Value).RemainingDays}/{((dynamic)b.Value).DefaultDays}"))}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating leave balances");
            }

            return leaveBalances;
        }

        private async Task<bool> HasSufficientLeaveBalanceAsync(string employeeId, int leaveTypeId, int requestedDays, int year = 0, int? excludeLeaveId = null)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return false;

            var query = _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"));

            // Exclude current leave being edited
            if (excludeLeaveId.HasValue)
            {
                query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
            }

            var leaves = await query.ToListAsync();

            var usedDays = 0;
            foreach (var leave in leaves)
            {
                var leaveDuration = leave.LeaveDays > 0 ? leave.LeaveDays :
                                  (leave.EndDate.ToDateTime(TimeOnly.MinValue) - leave.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                usedDays += leaveDuration;
            }

            var remainingDays = leaveType.DefaultDaysPerYear - usedDays;

            return remainingDays >= requestedDays;
        }

        private async Task<int> GetRemainingLeaveBalanceAsync(string employeeId, int leaveTypeId, int year = 0, int? excludeLeaveId = null)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return 0;

            var query = _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"));

            // Exclude current leave being edited
            if (excludeLeaveId.HasValue)
            {
                query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
            }

            var leaves = await query.ToListAsync();

            var usedDays = 0;
            foreach (var leave in leaves)
            {
                var leaveDuration = leave.LeaveDays > 0 ? leave.LeaveDays :
                                  (leave.EndDate.ToDateTime(TimeOnly.MinValue) - leave.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                usedDays += leaveDuration;
            }

            var remainingDays = leaveType.DefaultDaysPerYear - usedDays;

            return Math.Max(0, remainingDays);
        }

        private async Task<Dictionary<string, object>> GetEmployeeLeaveBalance(string employeeId)
        {
            var leaveTypes = await _context.LeaveTypes.ToListAsync();
            var leaveBalances = new Dictionary<string, object>();

            foreach (var leaveType in leaveTypes)
            {
                // Get default days for this leave type
                var defaultDays = leaveType.DefaultDaysPerYear;

                // Calculate used days - ONLY APPROVED leaves
                var usedDays = await _context.Leaves
                    .Where(l => l.EmployeeID == employeeId &&
                               l.LeaveTypeID == leaveType.LeaveTypeID &&
                               l.Status == "Approved" &&
                               l.StartDate.Year == DateTime.Now.Year) // Add year filter
                    .SumAsync(l => l.LeaveDays);

                var remainingDays = defaultDays - usedDays;

                leaveBalances[leaveType.TypeName] = new
                {
                    DefaultDays = defaultDays,
                    UsedDays = usedDays,
                    RemainingDays = Math.Max(0, remainingDays)
                };
            }

            return leaveBalances;
        }


        private int GetDefaultLeaveDays(string leaveType)
        {
            switch (leaveType)
            {
                case "Annual Leave":
                    return 14; 
                case "Medical Leave":
                    return 10; 
                case "Hospitalization Leave":
                    return 16; 
                default:
                    return 0;
            }
        }

        private async Task<bool> IsValidLeaveTypeAsync(int leaveTypeId)
        {
            return await _context.LeaveTypes.AnyAsync(lt => lt.LeaveTypeID == leaveTypeId);
        }

        private async Task PopulateLeaveTypeDropdownAsync()
        {
            var leaveTypes = await _context.LeaveTypes.ToListAsync();
            ViewBag.LeaveTypes = new SelectList(leaveTypes, "LeaveTypeID", "TypeName");
        }

        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.LeaveID == id);
        }
    }
}