using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        public async Task<IActionResult> AdminDashboard()
        {
            try
            {
                ViewData["Title"] = "Admin Dashboard";
                ViewData["UserRole"] = "Admin";

                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Get recent claims using same logic as HR Index (exclude soft-deleted claims)
                var recentClaimApplications = await _context.Claims
                    .Include(c => c.Employee)
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(10) // Get recent 10 claims for admin dashboard
                    .ToListAsync();

                // Get claim statistics (also excluding soft-deleted)
                var totalPendingClaims = await _context.Claims
                    .Where(c => c.Status == "Pending" && !c.IsDeleted)
                    .CountAsync();

                var totalApprovedClaims = await _context.Claims
                    .Where(c => c.Status == "Approved" && !c.IsDeleted && c.CreatedDate.Year == currentYear)
                    .CountAsync();

                var totalClaimAmount = await _context.Claims
                    .Where(c => c.Status == "Approved" && !c.IsDeleted && c.CreatedDate.Year == currentYear)
                    .SumAsync(c => c.ClaimAmount);

                // Get leave statistics  
                var totalPendingLeaves = await _context.Leaves
                    .Where(l => l.Status == "Pending")
                    .CountAsync();

                var recentLeaveApplications = await _context.Leaves
                    .Include(l => l.Employee)
                    .Include(l => l.LeaveType)
                    .OrderByDescending(l => l.CreatedDate)
                    .Take(10)
                    .ToListAsync();

                // Get employee statistics
                var totalEmployees = await _context.Employees.CountAsync();
                var activeEmployees = await _context.Employees
                    .Where(e => e.ResignationDate == null || e.ResignationDate > DateOnly.FromDateTime(DateTime.Now))
                    .CountAsync();

                // Invoice statistics (if you have invoices)
                var totalInvoices = await _context.Invoices.CountAsync();
                var pendingInvoices = await _context.Invoices.Where(i => i.Status == "Pending").CountAsync();
                var sentInvoices = await _context.Invoices.Where(i => i.Status == "Sent").CountAsync();
                var paidInvoices = await _context.Invoices.Where(i => i.Status == "Paid").CountAsync();

                var recentInvoices = await _context.Invoices
                    .OrderByDescending(i => i.IssueDate)
                    .Take(10)
                    .ToListAsync();

                var thisMonthTotal = await _context.Invoices
                    .Where(i => i.IssueDate.Month == currentMonth && i.IssueDate.Year == currentYear && i.Status == "Paid")
                    .SumAsync(i => i.TotalAmount);

                var outstanding = await _context.Invoices
                    .Where(i => i.Status == "Pending" || i.Status == "Sent" || i.Status == "Overdue")
                    .SumAsync(i => i.TotalAmount);

                // Set ViewBag properties for claims (using HR Index logic)
                ViewBag.RecentClaimApplications = recentClaimApplications;
                ViewBag.TotalPendingClaims = totalPendingClaims;
                ViewBag.TotalApprovedClaims = totalApprovedClaims;
                ViewBag.TotalClaimAmount = totalClaimAmount;

                // Set ViewBag properties for leaves
                ViewBag.RecentLeaveApplications = recentLeaveApplications;
                ViewBag.TotalPendingLeaves = totalPendingLeaves;

                // Set ViewBag properties for employees
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.ActiveEmployees = activeEmployees;

                // Set ViewBag properties for invoices
                ViewBag.TotalInvoices = totalInvoices;
                ViewBag.Pending = pendingInvoices;
                ViewBag.Sent = sentInvoices;
                ViewBag.Paid = paidInvoices;
                ViewBag.RecentInvoices = recentInvoices;
                ViewBag.ThisMonthTotal = thisMonthTotal;
                ViewBag.Outstanding = outstanding;

                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");

                _logger.LogInformation("✅ Admin Dashboard loaded successfully");
                return View("~/Views/Admins/Dashboard.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error loading Admin dashboard");

                // Set default values
                ViewBag.RecentClaimApplications = new List<Claim>();
                ViewBag.TotalPendingClaims = 0;
                ViewBag.TotalApprovedClaims = 0;
                ViewBag.TotalClaimAmount = 0;
                ViewBag.RecentLeaveApplications = new List<LeaveModel>();
                ViewBag.TotalPendingLeaves = 0;
                ViewBag.TotalEmployees = 0;
                ViewBag.ActiveEmployees = 0;
                ViewBag.TotalInvoices = 0;
                ViewBag.Pending = 0;
                ViewBag.Sent = 0;
                ViewBag.Paid = 0;
                ViewBag.RecentInvoices = new List<Invoice>();
                ViewBag.ThisMonthTotal = 0m;
                ViewBag.Outstanding = 0m;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");

                TempData["Error"] = "An error occurred while loading the dashboard.";
                return View("~/Views/Admins/Dashboard.cshtml");
            }
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
                    .Where(c => !c.IsDeleted) // Add this line to match HR Index filtering
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

                // Calculate current payroll status for the dashboard
                var currentPayrollStatus = "Not Started";
                
                // Get all active employees who should have payroll records
                var activeEmployeeIds = await _context.Employees
                    .Where(e => e.ResignationDate == null || e.ResignationDate > DateOnly.FromDateTime(DateTime.Now))
                    .Select(e => e.EmployeeID)
                    .ToListAsync();

                if (activeEmployeeIds.Any())
                {
                    // Get payroll records for current month/year for active employees
                    var currentMonthPayrolls = await _context.Payrolls
                        .Where(p => p.Month == currentMonth && p.Year == currentYear && activeEmployeeIds.Contains(p.EmployeeID))
                        .ToListAsync();

                    if (currentMonthPayrolls.Any())
                    {
                        // Check if all active employees have payroll records
                        var employeesWithPayroll = currentMonthPayrolls.Select(p => p.EmployeeID).Distinct().ToList();
                        var employeesWithoutPayroll = activeEmployeeIds.Except(employeesWithPayroll).ToList();

                        if (employeesWithoutPayroll.Any())
                        {
                            // Not all employees have payroll records
                            currentPayrollStatus = "Pending";
                        }
                        else
                        {
                            // All employees have payroll records, check if all are completed
                            var completedPayrolls = currentMonthPayrolls.Where(p => p.PaymentStatus == "Completed").Count();
                            var totalPayrolls = currentMonthPayrolls.Count;

                            if (completedPayrolls == totalPayrolls)
                            {
                                currentPayrollStatus = "Completed";
                            }
                            else
                            {
                                // Check the status distribution
                                var pendingCount = currentMonthPayrolls.Where(p => p.PaymentStatus == "Pending").Count();
                                var pendingApprovalCount = currentMonthPayrolls.Where(p => p.PaymentStatus == "Pending Approval").Count();
                                var approvedCount = currentMonthPayrolls.Where(p => p.PaymentStatus == "Approved").Count();
                                var rejectedCount = currentMonthPayrolls.Where(p => p.PaymentStatus == "Rejected").Count();

                                if (pendingCount > 0 || rejectedCount > 0)
                                {
                                    currentPayrollStatus = "Pending";
                                }
                                else if (pendingApprovalCount > 0)
                                {
                                    currentPayrollStatus = "Processing";
                                }
                                else if (approvedCount > 0)
                                {
                                    currentPayrollStatus = "Pending";
                                }
                            }
                        }
                    }
                    else
                    {
                        // No payroll records exist for current month
                        currentPayrollStatus = "Not Started";
                    }
                }

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
                ViewBag.CurrentPayrollStatus = currentPayrollStatus;
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
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }




    }
}