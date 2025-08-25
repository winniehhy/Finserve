using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

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
            else if (User.IsInRole("Senior HR"))
            {
                return RedirectToAction("SeniorHRDashboard");
            }
            else if (User.IsInRole("HR"))
            {
                return RedirectToAction("HRDashboard");
            }
            else if (User.IsInRole("Employee"))
            {
                return RedirectToAction("EmployeeDashboard");
            }

            // If user has no recognized role, log them out instead of redirect loop
            return RedirectToAction("Logout", "Account");
        }

        // Admin Dashboard - Only accessible by Admin
        [Authorize(Roles = "Admin")]
        public IActionResult AdminDashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            ViewData["UserRole"] = "Admin";
            return View("~/Views/Admins/Dashboard.cshtml");
        }

        // HR Dashboard - Only accessible by HR
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HRDashboard()
        {
            try
            {
                ViewData["Title"] = "HR Dashboard";
                ViewData["UserRole"] = "HR";

                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get all employees count
                var totalEmployees = await _context.Employees.CountAsync();
                var activeEmployees = await _context.Employees
                    .Where(e => e.ResignationDate == null || e.ResignationDate > DateOnly.FromDateTime(DateTime.Now))
                    .CountAsync();

                // Get leave statistics
                var totalPendingLeaves = await _context.Leaves
                    .Where(l => l.Status == "Pending")
                    .CountAsync();

                var totalApprovedLeaves = await _context.Leaves
                    .Where(l => l.Status == "Approved" && l.StartDate.Year == currentYear)
                    .CountAsync();

                var recentLeaveApplications = await _context.Leaves
                    .Include(l => l.Employee)
                    .Include(l => l.LeaveType)
                    .OrderByDescending(l => l.CreatedDate)
                    .Take(5)
                    .ToListAsync();

                // Get claims statistics
                var totalPendingClaims = await _context.Claims
                    .Where(c => c.Status == "Pending")
                    .CountAsync();

                var totalApprovedClaims = await _context.Claims
                    .Where(c => c.Status == "Approved" && c.CreatedDate.Year == currentYear)
                    .CountAsync();

                var totalClaimAmount = await _context.Claims
                    .Where(c => c.Status == "Approved" && c.CreatedDate.Year == currentYear)
                    .SumAsync(c => c.ClaimAmount);

                var recentClaimApplications = await _context.Claims
                    .Include(c => c.Employee)
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(5)
                    .ToListAsync();

                // Get employees by department/status - FIX THE ISSUE HERE
                var employeesByStatus = await _context.Employees
                    .GroupBy(e => e.ConfirmationStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Convert to a list of objects that can be safely cast - KEEP ONLY THIS ONE
                var employeeStatusList = employeesByStatus.Select(x => new
                {
                    Status = x.Status,
                    Count = x.Count
                }).Cast<object>().ToList();

                // Calendar data for HR - show all approved leaves
                var allApprovedLeaves = await _context.Leaves
                    .Include(l => l.Employee)
                    .Where(l => l.Status == "Approved" &&
                               (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear))
                    .ToListAsync();

                var calendarData = new Dictionary<string, List<object>>();

                // Initialize all months for current year
                for (int month = 1; month <= 12; month++)
                {
                    var monthKey = $"{currentYear}-{month:D2}";
                    calendarData[monthKey] = new List<object>();
                }

                // Add leave dates to calendar data with employee info
                foreach (var leave in allApprovedLeaves)
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
                                calendarData[monthKey].Add(new
                                {
                                    Day = date.Day,
                                    EmployeeName = $"{leave.Employee?.FirstName} {leave.Employee?.LastName}",
                                    LeaveType = leave.LeaveType?.TypeName ?? "Leave"
                                });
                            }
                        }
                    }
                }

                // Set ViewBag properties - USE ONLY THE CONVERTED LIST
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.ActiveEmployees = activeEmployees;
                ViewBag.TotalPendingLeaves = totalPendingLeaves;
                ViewBag.TotalApprovedLeaves = totalApprovedLeaves;
                ViewBag.TotalPendingClaims = totalPendingClaims;
                ViewBag.TotalApprovedClaims = totalApprovedClaims;
                ViewBag.TotalClaimAmount = totalClaimAmount;
                ViewBag.RecentLeaveApplications = recentLeaveApplications;
                ViewBag.RecentClaimApplications = recentClaimApplications;
                //ViewBag.CurrentPayrollStatus = currentPayrollBatch?.Status ?? "Not Started";
                //ViewBag.CurrentPayrollBatch = currentPayrollBatch;
                ViewBag.EmployeesByStatus = employeeStatusList; // USE ONLY THIS ONE - REMOVE THE DUPLICATE
                ViewBag.CalendarData = calendarData;
                ViewBag.CurrentYear = currentYear;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;

                _logger.LogInformation($"✅ HR Dashboard loaded successfully");
                return View("~/Views/HR/Dashboard.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading HR dashboard");

                // Set default values
                ViewBag.TotalEmployees = 0;
                ViewBag.ActiveEmployees = 0;
                ViewBag.TotalPendingLeaves = 0;
                ViewBag.TotalApprovedLeaves = 0;
                ViewBag.TotalPendingClaims = 0;
                ViewBag.TotalApprovedClaims = 0;
                ViewBag.TotalClaimAmount = 0;
                ViewBag.RecentLeaveApplications = new List<LeaveModel>();
                ViewBag.RecentClaimApplications = new List<Claim>();
                ViewBag.CurrentPayrollStatus = "Error";
                ViewBag.CurrentPayrollBatch = null;
                ViewBag.EmployeesByStatus = new List<object>();
                ViewBag.CalendarData = new Dictionary<string, List<object>>();
                ViewBag.CurrentYear = DateTime.Now.Year;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;

                TempData["Error"] = "An error occurred while loading the dashboard.";
                return View("~/Views/HR/Dashboard.cshtml");
            }
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

                // Calculate total days used and remaining - FIXED: Use proper casting
                double totalDaysUsed = 0;
                double totalRemainingDays = 0;
                double totalDefaultDays = 0;

                foreach (var balance in leaveBalances)
                {
                    var leaveBalance = balance.Value as dynamic;
                    if (leaveBalance != null)
                    {
                        // FIXED: Proper casting to double
                        totalDaysUsed += Convert.ToDouble(leaveBalance.UsedDays);
                        totalRemainingDays += Convert.ToDouble(leaveBalance.RemainingDays);
                        totalDefaultDays += Convert.ToDouble(leaveBalance.DefaultDays);
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

                // Claims data
                // Pull claims exactly like the claim management Index: exclude soft-deleted
                var claims = await _context.Claims
                    .Where(c => c.EmployeeID == employeeId && !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedDate)
                    .ToListAsync();

                var recentClaims = claims.Take(5).ToList();

                var totalClaimsCount = claims.Count;
                var approvedClaimsCount = claims.Count(c => c.Status == "Approved");
                var pendingClaimsCount = claims.Count(c => c.Status == "Pending");
                var totalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);
                var approvalRate = totalClaimsCount > 0 ? (approvedClaimsCount * 100.0 / totalClaimsCount) : 0;

                // Get approved leave dates for calendar
                var approvedLeaves = await _context.Leaves
                    .Where(l => l.EmployeeID == employeeId &&
                               l.Status == "Approved" &&
                               (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear))
                    .ToListAsync();

                // Create calendar data
                var calendarData = new Dictionary<string, List<int>>();
                for (int month = 1; month <= 12; month++)
                {
                    var monthKey = $"{currentYear}-{month:D2}";
                    calendarData[monthKey] = new List<int>();
                }

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

                foreach (var key in calendarData.Keys.ToList())
                {
                    calendarData[key] = calendarData[key].Distinct().OrderBy(d => d).ToList();
                }

                // FIXED: Set ViewBag properties with proper casting
                ViewBag.LeaveBalances = leaveBalances;
                ViewBag.TotalLeaveDaysUsed = Math.Round(totalDaysUsed, 1);
                ViewBag.TotalRemainingLeave = Math.Round(totalRemainingDays, 1);
                ViewBag.TotalDefaultDays = Math.Round(totalDefaultDays, 1);
                ViewBag.CurrentYear = currentYear;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.RecentLeaves = recentLeaves;
                ViewBag.PendingRequestsCount = pendingRequestsCount;
                ViewBag.CalendarData = calendarData;
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;

                // Claims ViewBag properties
                ViewBag.TotalClaims = totalClaimsCount;
                ViewBag.ApprovedClaims = approvedClaimsCount;
                ViewBag.PendingClaims = pendingClaimsCount;
                ViewBag.ApprovalRate = Math.Round(approvalRate, 1);
                ViewBag.TotalClaimAmount = totalClaimAmount;
                ViewBag.RecentClaims = recentClaims;
                ViewBag.PendingLeaves = pendingRequestsCount;

                // FIXED: Populate individual leave balances correctly
                PopulateLeaveBalanceViewBag(leaveBalances);

                _logger.LogInformation($"Dashboard loaded successfully for employee {employeeId}");
                _logger.LogInformation($"Leave balances: {string.Join(", ", leaveBalances.Select(b => $"{b.Key}: {((dynamic)b.Value).RemainingDays}/{((dynamic)b.Value).DefaultDays}"))}");

                return View("~/Views/Employee/Dashboard.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");

                // Set default values
                ViewBag.LeaveBalances = new Dictionary<string, object>();
                ViewBag.TotalLeaveDaysUsed = 0.0;
                ViewBag.TotalRemainingLeave = 0.0;
                ViewBag.TotalDefaultDays = 0.0;
                ViewBag.CurrentYear = DateTime.Now.Year;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.RecentLeaves = new List<LeaveModel>();
                ViewBag.PendingRequestsCount = 0;
                ViewBag.CalendarData = new Dictionary<string, List<int>>();
                ViewBag.CurrentMonthIndex = DateTime.Now.Month;
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

        // Senior HR Dashboard - Only accessible by Senior HR
        [Authorize(Roles = "Senior HR")]
        public async Task<IActionResult> SeniorHRDashboard()
        {
            try
            {
                ViewData["Title"] = "Senior HR Dashboard";
                ViewData["UserRole"] = "Senior HR";

                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get payroll statistics
                var pendingPayrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Where(p => p.PaymentStatus == "Pending Approval")
                    .ToListAsync();

                var approvedPayrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Where(p => p.PaymentStatus == "Approved" && p.Year == currentYear && p.Month == currentMonth)
                    .ToListAsync();

                var recentPayrollActivities = await _context.Payrolls
                    .Include(p => p.Employee)
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .Take(10)
                    .ToListAsync();

                // Get employee statistics
                var totalEmployees = await _context.Employees.CountAsync();
                var activeEmployees = await _context.Employees
                    .Where(e => e.ResignationDate == null || e.ResignationDate > DateOnly.FromDateTime(DateTime.Now))
                    .CountAsync();

                // Calculate totals
                var totalPayrollAmount = approvedPayrolls.Sum(p => p.TotalWages);
                var totalPayrolls = await _context.Payrolls
                    .Where(p => p.Year == currentYear && p.Month == currentMonth)
                    .CountAsync();

                // Get payroll status summary
                var payrollStatusSummary = await _context.Payrolls
                    .Where(p => p.Year == currentYear && p.Month == currentMonth)
                    .GroupBy(p => p.PaymentStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var payrollStatusList = payrollStatusSummary.Select(x => new
                {
                    Status = x.Status,
                    Count = x.Count
                }).Cast<object>().ToList();

                // Set ViewBag properties
                ViewBag.PendingPayrollCount = pendingPayrolls.Count;
                ViewBag.ApprovedPayrollCount = approvedPayrolls.Count;
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.ActiveEmployees = activeEmployees;
                ViewBag.TotalPayrollAmount = totalPayrollAmount;
                ViewBag.TotalPayrolls = totalPayrolls;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");

                ViewBag.PendingPayrolls = pendingPayrolls.Take(5).ToList();
                ViewBag.RecentPayrollActivities = recentPayrollActivities.Take(5).ToList();
                ViewBag.PayrollStatusSummary = payrollStatusList;

                _logger.LogInformation("✅ Senior HR Dashboard loaded successfully");
                return View("~/Views/SeniorHR/Dashboard.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading Senior HR dashboard");

                // Set default values
                ViewBag.PendingPayrollCount = 0;
                ViewBag.ApprovedPayrollCount = 0;
                ViewBag.TotalEmployees = 0;
                ViewBag.ActiveEmployees = 0;
                ViewBag.TotalPayrollAmount = 0;
                ViewBag.TotalPayrolls = 0;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.PendingPayrolls = new List<Payroll>();
                ViewBag.RecentPayrollActivities = new List<Payroll>();
                ViewBag.PayrollStatusSummary = new List<object>();

                TempData["Error"] = "An error occurred while loading the dashboard.";
                return View("~/Views/SeniorHR/Dashboard.cshtml");
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
            _logger.LogInformation("Populating individual ViewBag properties for leave balances");

            try
            {
                if (leaveBalances.ContainsKey("Annual Leave"))
                {
                    var annualLeave = leaveBalances["Annual Leave"] as dynamic;
                    ViewBag.AnnualLeaveBalance = annualLeave != null ? Convert.ToDouble(annualLeave.RemainingDays) : 14.0;
                    _logger.LogInformation($"Annual Leave Balance: {ViewBag.AnnualLeaveBalance}");
                }
                else
                {
                    ViewBag.AnnualLeaveBalance = 14.0;
                }

                if (leaveBalances.ContainsKey("Medical Leave"))
                {
                    var medicalLeave = leaveBalances["Medical Leave"] as dynamic;
                    ViewBag.MedicalLeaveBalance = medicalLeave != null ? Convert.ToDouble(medicalLeave.RemainingDays) : 10.0;
                    _logger.LogInformation($"Medical Leave Balance: {ViewBag.MedicalLeaveBalance}");
                }
                else
                {
                    ViewBag.MedicalLeaveBalance = 10.0;
                }

                if (leaveBalances.ContainsKey("Hospitalization Leave"))
                {
                    var hospitalizationLeave = leaveBalances["Hospitalization Leave"] as dynamic;
                    ViewBag.HospitalizationLeaveBalance = hospitalizationLeave != null ? Convert.ToDouble(hospitalizationLeave.RemainingDays) : 16.0;
                    _logger.LogInformation($"Hospitalization Leave Balance: {ViewBag.HospitalizationLeaveBalance}");
                }
                else
                {
                    ViewBag.HospitalizationLeaveBalance = 16.0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating individual ViewBag properties");
                ViewBag.AnnualLeaveBalance = 14.0;
                ViewBag.MedicalLeaveBalance = 10.0;
                ViewBag.HospitalizationLeaveBalance = 16.0;
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

                    double usedDays = 0;
                    foreach (var leave in approvedLeaves)
                    {
                        // Use LeaveDays if available, otherwise calculate from dates
                        var leaveDuration = leave.LeaveDays > 0 ? leave.LeaveDays :
                                          (leave.EndDate.DayNumber - leave.StartDate.DayNumber) + 1;
                        usedDays += leaveDuration;
                    }

                    double pendingDays = 0;
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

                _logger.LogInformation($"🧮 Calculated balances: {string.Join(", ", leaveBalances.Select(b => $"{b.Key}: {((dynamic)b.Value).RemainingDays:0.#}/{((dynamic)b.Value).DefaultDays}"))}");
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


        // Export Employee Dashboard report to Admin view
        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportEmployeeReport()
        {
            // Reuse same data assembly as EmployeeDashboard
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return RedirectToAction(nameof(EmployeeDashboard));
            }

            var currentYear = DateTime.Now.Year;
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId, currentYear);

            double totalDaysUsed = 0, totalRemainingDays = 0, totalDefaultDays = 0;
            foreach (var balance in leaveBalances)
            {
                dynamic lb = balance.Value as dynamic;
                totalDaysUsed += Convert.ToDouble(lb.UsedDays);
                totalRemainingDays += Convert.ToDouble(lb.RemainingDays);
                totalDefaultDays += Convert.ToDouble(lb.DefaultDays);
            }

            var recentLeaves = await _context.Leaves
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeID == employeeId)
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToListAsync();

            var claims = await _context.Claims.Where(c => c.EmployeeID == employeeId && !c.IsDeleted).ToListAsync();
            var recentClaims = claims.OrderByDescending(c => c.CreatedDate).Take(5).ToList();
            var totalClaimsCount = claims.Count;
            var approvedClaimsCount = claims.Count(c => c.Status == "Approved");
            var pendingClaimsCount = claims.Count(c => c.Status == "Pending");
            var totalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);
            var approvalRate = totalClaimsCount > 0 ? (approvedClaimsCount * 100.0 / totalClaimsCount) : 0;

            // approved leaves calendar
            var approvedLeaves = await _context.Leaves
                .Where(l => l.EmployeeID == employeeId && l.Status == "Approved" && (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear))
                .ToListAsync();
            var calendarData = new Dictionary<string, List<int>>();
            for (int m = 1; m <= 12; m++) calendarData[$"{currentYear}-{m:D2}"] = new List<int>();
            foreach (var leave in approvedLeaves)
            {
                for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
                {
                    if (date.Year == currentYear) calendarData[$"{date.Year}-{date.Month:D2}"].Add(date.Day);
                }
            }
            foreach (var k in calendarData.Keys.ToList()) calendarData[k] = calendarData[k].Distinct().OrderBy(d => d).ToList();

            ViewBag.SubmitterName = User.Identity?.Name;
            ViewBag.TotalClaims = totalClaimsCount;
            ViewBag.ApprovedClaims = approvedClaimsCount;
            ViewBag.PendingClaims = pendingClaimsCount;
            ViewBag.ApprovalRate = Math.Round(approvalRate, 1);
            ViewBag.TotalClaimAmount = totalClaimAmount;
            ViewBag.RecentClaims = recentClaims;
            ViewBag.LeaveBalances = leaveBalances;
            ViewBag.RecentLeaves = recentLeaves;
            ViewBag.CalendarData = calendarData;
            ViewBag.TotalLeaveDaysUsed = Math.Round(totalDaysUsed, 1);
            ViewBag.TotalRemainingLeave = Math.Round(totalRemainingDays, 1);
            ViewBag.TotalDefaultDays = Math.Round(totalDefaultDays, 1);

            // Persist the report so Admin can review later
            var payload = new
            {
                SubmitterName = User.Identity?.Name,
                EmployeeId = employeeId,
                CreatedAt = DateTime.Now,
                TotalClaims = totalClaimsCount,
                ApprovedClaims = approvedClaimsCount,
                PendingClaims = pendingClaimsCount,
                ApprovalRate = Math.Round(approvalRate, 1),
                TotalClaimAmount = totalClaimAmount,
                RecentClaims = recentClaims,
                LeaveBalances = leaveBalances,
                RecentLeaves = recentLeaves,
                CalendarData = calendarData,
                TotalLeaveDaysUsed = Math.Round(totalDaysUsed, 1),
                TotalRemainingLeave = Math.Round(totalRemainingDays, 1),
                TotalDefaultDays = Math.Round(totalDefaultDays, 1)
            };
            PersistReport(payload, "employee");
            TempData["Success"] = "Report exported and sent to Admin.";

            return View("~/Views/Admins/Report/employee_report.cshtml");
        }

        // Export HR Dashboard report to Admin view
        [HttpPost]
        [Authorize(Roles = "HR")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportHRReport()
        {
            // Build the same data used in HR dashboard
            var claims = await _context.Claims
                .Include(c => c.Employee)
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
            ViewBag.TotalPendingClaims = claims.Count(c => c.Status == "Pending");
            ViewBag.TotalApprovedClaims = claims.Count(c => c.Status == "Approved");
            ViewBag.TotalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);
            ViewBag.RecentClaimApplications = claims.Take(10).ToList();

            var leaveApps = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();
            ViewBag.TotalPendingLeaves = leaveApps.Count(l => l.Status == "Pending");
            ViewBag.RecentLeaveApplications = leaveApps.Take(10).ToList();

            // Calendar days for approved leaves this year
            var currentYear = DateTime.Now.Year;
            var approvedLeaves = leaveApps.Where(l => l.Status == "Approved" && (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear)).ToList();
            var calendarData = new Dictionary<string, List<int>>();
            for (int m = 1; m <= 12; m++) calendarData[$"{currentYear}-{m:D2}"] = new List<int>();
            foreach (var leave in approvedLeaves)
            {
                for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
                {
                    if (date.Year == currentYear) calendarData[$"{date.Year}-{date.Month:D2}"].Add(date.Day);
                }
            }
            foreach (var k in calendarData.Keys.ToList()) calendarData[k] = calendarData[k].Distinct().OrderBy(d => d).ToList();

            ViewBag.CalendarData = calendarData;
            ViewBag.SubmitterName = User.Identity?.Name;

            var payload = new
            {
                SubmitterName = User.Identity?.Name,
                CreatedAt = DateTime.Now,
                TotalPendingClaims = ViewBag.TotalPendingClaims,
                TotalApprovedClaims = ViewBag.TotalApprovedClaims,
                TotalClaimAmount = ViewBag.TotalClaimAmount,
                RecentClaimApplications = ViewBag.RecentClaimApplications,
                TotalPendingLeaves = ViewBag.TotalPendingLeaves,
                RecentLeaveApplications = ViewBag.RecentLeaveApplications,
                CalendarData = calendarData
            };
            PersistReport(payload, "hr");
            TempData["Success"] = "HR report exported and sent to Admin.";

            return View("~/Views/Admins/Report/HR_report.cshtml");
        }

        // Admin menu - view HR report snapshot
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewHRReport()
        {
            var file = Request.Query["file"].ToString();
            if (!string.IsNullOrEmpty(file))
            {
                if (LoadReport(file, out var json, "hr"))
                {
                    ApplyHrViewBag(json);
                    return View("~/Views/Admins/Report/HR_report.cshtml");
                }
            }
            return await ExportHRReport();
        }

        // Admin menu - view Employee report snapshot (optional filter by employeeId)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewEmployeeReport(string? employeeId)
        {
            var file = Request.Query["file"].ToString();
            if (!string.IsNullOrEmpty(file))
            {
                if (LoadReport(file, out var json, "employee"))
                {
                    ApplyEmployeeViewBag(json);
                    return View("~/Views/Admins/Report/employee_report.cshtml");
                }
            }
            // If no employee specified, just render an empty shell with recent items
            if (string.IsNullOrEmpty(employeeId))
            {
                ViewBag.SubmitterName = "(Employee Report Preview)";
                ViewBag.TotalClaims = 0;
                ViewBag.ApprovedClaims = 0;
                ViewBag.PendingClaims = 0;
                ViewBag.ApprovalRate = 0.0;
                ViewBag.TotalClaimAmount = 0m;
                ViewBag.RecentClaims = new List<Claim>();
                ViewBag.LeaveBalances = new Dictionary<string, object>();
                ViewBag.RecentLeaves = new List<LeaveModel>();
                ViewBag.CalendarData = new Dictionary<string, List<int>>();
                ViewBag.TotalLeaveDaysUsed = 0.0;
                ViewBag.TotalRemainingLeave = 0.0;
                ViewBag.TotalDefaultDays = 0.0;
                return View("~/Views/Admins/Report/employee_report.cshtml");
            }

            // Otherwise, build report for specified employee
            var currentYear = DateTime.Now.Year;
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId, currentYear);

            double totalDaysUsed = 0, totalRemainingDays = 0, totalDefaultDays = 0;
            foreach (var balance in leaveBalances)
            {
                dynamic lb = balance.Value as dynamic;
                totalDaysUsed += Convert.ToDouble(lb.UsedDays);
                totalRemainingDays += Convert.ToDouble(lb.RemainingDays);
                totalDefaultDays += Convert.ToDouble(lb.DefaultDays);
            }

            var recentLeaves = await _context.Leaves
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeID == employeeId)
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToListAsync();

            var claims = await _context.Claims.Where(c => c.EmployeeID == employeeId && !c.IsDeleted).ToListAsync();
            var recentClaims = claims.OrderByDescending(c => c.CreatedDate).Take(5).ToList();
            var totalClaimsCount = claims.Count;
            var approvedClaimsCount = claims.Count(c => c.Status == "Approved");
            var pendingClaimsCount = claims.Count(c => c.Status == "Pending");
            var totalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);
            var approvalRate = totalClaimsCount > 0 ? (approvedClaimsCount * 100.0 / totalClaimsCount) : 0;

            var approvedLeaves = await _context.Leaves
                .Where(l => l.EmployeeID == employeeId && l.Status == "Approved" && (l.StartDate.Year == currentYear || l.EndDate.Year == currentYear))
                .ToListAsync();
            var calendarData = new Dictionary<string, List<int>>();
            for (int m = 1; m <= 12; m++) calendarData[$"{currentYear}-{m:D2}"] = new List<int>();
            foreach (var leave in approvedLeaves)
            {
                for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
                {
                    if (date.Year == currentYear) calendarData[$"{date.Year}-{date.Month:D2}"].Add(date.Day);
                }
            }
            foreach (var k in calendarData.Keys.ToList()) calendarData[k] = calendarData[k].Distinct().OrderBy(d => d).ToList();

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeID == employeeId);
            ViewBag.SubmitterName = emp != null ? $"{emp.FirstName} {emp.LastName}" : employeeId;
            ViewBag.TotalClaims = totalClaimsCount;
            ViewBag.ApprovedClaims = approvedClaimsCount;
            ViewBag.PendingClaims = pendingClaimsCount;
            ViewBag.ApprovalRate = Math.Round(approvalRate, 1);
            ViewBag.TotalClaimAmount = totalClaimAmount;
            ViewBag.RecentClaims = recentClaims;
            ViewBag.LeaveBalances = leaveBalances;
            ViewBag.RecentLeaves = recentLeaves;
            ViewBag.CalendarData = calendarData;
            ViewBag.TotalLeaveDaysUsed = Math.Round(totalDaysUsed, 1);
            ViewBag.TotalRemainingLeave = Math.Round(totalRemainingDays, 1);
            ViewBag.TotalDefaultDays = Math.Round(totalDefaultDays, 1);

            return View("~/Views/Admins/Report/employee_report.cshtml");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult EmployeeReportsList()
        {
            var list = ListReports("employee");
            return View("~/Views/Admins/Report/EmployeeReportsList.cshtml", list);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult HRReportsList()
        {
            var list = ListReports("hr");
            return View("~/Views/Admins/Report/HRReportsList.cshtml", list);
        }

        private class ReportIndexItem
        {
            public string File { get; set; } = string.Empty;
            public string Submitter { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public string Type { get; set; } = string.Empty;
            public string? EmployeeId { get; set; }
        }

        private List<object> ListReports(string type)
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", type);
            Directory.CreateDirectory(root);
            var items = new List<object>();
            foreach (var f in Directory.GetFiles(root, "*.json").OrderByDescending(p => p))
            {
                if (LoadReport(Path.GetFileName(f), out var json, type))
                {
                    var submitter = json.TryGetProperty("SubmitterName", out var s) ? s.GetString() ?? "-" : "-";
                    var created = json.TryGetProperty("CreatedAt", out var c) && c.ValueKind == JsonValueKind.String ? DateTime.Parse(c.GetString()) : System.IO.File.GetCreationTime(f);
                    var empId = json.TryGetProperty("EmployeeId", out var e) ? e.GetString() : null;
                    items.Add(new { File = Path.GetFileName(f), Submitter = submitter, CreatedAt = created, Type = type, EmployeeId = empId });
                }
            }
            return items;
        }

        private void PersistReport(object payload, string type)
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", type);
            Directory.CreateDirectory(root);
            var file = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var full = Path.Combine(root, file);
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(full, json);
        }

        private bool LoadReport(string file, out JsonElement json, string type)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", type);
            var full = Path.Combine(folder, file);
            if (!System.IO.File.Exists(full))
            {
                json = default;
                return false;
            }
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(full));
            json = doc.RootElement.Clone();
            return true;
        }

        private void ApplyEmployeeViewBag(JsonElement json)
        {
            ViewBag.SubmitterName = json.GetProperty("SubmitterName").GetString();
            ViewBag.TotalClaims = json.GetProperty("TotalClaims").GetInt32();
            ViewBag.ApprovedClaims = json.GetProperty("ApprovedClaims").GetInt32();
            ViewBag.PendingClaims = json.GetProperty("PendingClaims").GetInt32();
            ViewBag.ApprovalRate = json.GetProperty("ApprovalRate").GetDouble();
            ViewBag.TotalClaimAmount = json.GetProperty("TotalClaimAmount").GetDecimal();
            ViewBag.RecentClaims = JsonSerializer.Deserialize<List<Claim>>(json.GetProperty("RecentClaims").GetRawText()) ?? new List<Claim>();
            ViewBag.LeaveBalances = JsonSerializer.Deserialize<Dictionary<string, object>>(json.GetProperty("LeaveBalances").GetRawText()) ?? new Dictionary<string, object>();
            ViewBag.RecentLeaves = JsonSerializer.Deserialize<List<LeaveModel>>(json.GetProperty("RecentLeaves").GetRawText()) ?? new List<LeaveModel>();
            ViewBag.CalendarData = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json.GetProperty("CalendarData").GetRawText()) ?? new Dictionary<string, List<int>>();
            ViewBag.TotalLeaveDaysUsed = json.GetProperty("TotalLeaveDaysUsed").GetDouble();
            ViewBag.TotalRemainingLeave = json.GetProperty("TotalRemainingLeave").GetDouble();
            ViewBag.TotalDefaultDays = json.GetProperty("TotalDefaultDays").GetDouble();
        }

        private void ApplyHrViewBag(JsonElement json)
        {
            ViewBag.SubmitterName = json.GetProperty("SubmitterName").GetString();
            ViewBag.TotalPendingClaims = json.GetProperty("TotalPendingClaims").GetInt32();
            ViewBag.TotalApprovedClaims = json.GetProperty("TotalApprovedClaims").GetInt32();
            ViewBag.TotalClaimAmount = json.GetProperty("TotalClaimAmount").GetDecimal();
            ViewBag.RecentClaimApplications = JsonSerializer.Deserialize<List<Claim>>(json.GetProperty("RecentClaimApplications").GetRawText()) ?? new List<Claim>();
            ViewBag.TotalPendingLeaves = json.GetProperty("TotalPendingLeaves").GetInt32();
            ViewBag.RecentLeaveApplications = JsonSerializer.Deserialize<List<LeaveModel>>(json.GetProperty("RecentLeaveApplications").GetRawText()) ?? new List<LeaveModel>();
            ViewBag.CalendarData = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json.GetProperty("CalendarData").GetRawText()) ?? new Dictionary<string, List<int>>();
        }
    }
}