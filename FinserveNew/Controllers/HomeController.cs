using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FinserveNew.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        // Default Index - redirects to appropriate dashboard based on role
        [Authorize]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("AdminDashboard");
            }
            else if (User.IsInRole("HR"))
            {
                return RedirectToAction("HRDashboard");
            }
            else if (User.IsInRole("Employee"))
            {
                return RedirectToAction("EmployeeDashboard");
            }
            // Fallback for users without proper roles
            return RedirectToAction("Login", "Account");
        }

        // Admin Dashboard - Only accessible by Admin
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            ViewData["UserRole"] = "Admin";

            // TODO: Add admin-specific dashboard data here
            // Example:
            // ViewBag.TotalEmployees = await _context.Employees.CountAsync();
            // ViewBag.TotalInvoices = await _context.Invoices.CountAsync();

            return View("~/Views/Admins/Dashboard.cshtml");
        }

        // HR Dashboard - Only accessible by HR
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HRDashboard()
        {
            ViewData["Title"] = "HR Dashboard";
            ViewData["UserRole"] = "HR";

            // 

            return View("~/Views/HR/Dashboard.cshtml");
        }

        // Employee Dashboard - Only accessible by Employee
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> EmployeeDashboard()
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

        // ================== HELPER METHODS ==================

        private async Task<string> GetCurrentEmployeeId()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Add null check BEFORE accessing properties
            if (currentUser == null)
            {
                _logger.LogWarning("No authenticated user found");
                return null; // or throw an exception
            }

            _logger.LogInformation($"Current user: {currentUser.UserName}, Email: {currentUser.Email}");

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Username == currentUser.UserName || e.Email == currentUser.Email);

            if (employee == null)
            {
                _logger.LogWarning($"No employee found for username: {currentUser.UserName} or email: {currentUser.Email}");
            }

            return employee?.EmployeeID;
        }

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

        // Helper method to get date range
        private List<DateOnly> GetDateRange(DateOnly startDate, DateOnly endDate)
        {
            var dates = new List<DateOnly>();
            var current = startDate;

            while (current <= endDate)
            {
                dates.Add(current);
                current = current.AddDays(1);
            }

            return dates;
        }

        // Privacy page - accessible to all authenticated users
        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        // Error page - accessible to all
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}   