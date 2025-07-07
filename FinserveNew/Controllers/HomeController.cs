using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FinserveNew.Models;
using FinserveNew.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FinserveNew.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
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
        public IActionResult AdminDashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            ViewData["UserRole"] = "Admin";
            return View("~/Views/Admins/Dashboard.cshtml");
        }

        // HR Dashboard - Only accessible by HR
        [Authorize(Roles = "HR")]
        public IActionResult HRDashboard()
        {
            ViewData["Title"] = "HR Dashboard";
            ViewData["UserRole"] = "HR";
            return View("~/Views/HR/Dashboard.cshtml");
        }

        // Employee Dashboard - Only accessible by Employee
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> EmployeeDashboard()
        {
            ViewData["Title"] = "Employee Dashboard";
            ViewData["UserRole"] = "Employee";

            // Get current employee ID (TODO: Replace with actual authentication)
            var currentEmployeeId = "E001";
            var currentYear = DateTime.Now.Year;

            try
            {
                // Get Claims Data
                var claims = await _context.Claims
                    .Where(c => c.EmployeeID == currentEmployeeId)
                    .OrderByDescending(c => c.CreatedDate)
                    .ToListAsync();

                var recentClaims = claims.Take(5).ToList();
                var totalClaimsCount = claims.Count;
                var approvedClaimsCount = claims.Count(c => c.Status == "Approved");
                var pendingClaimsCount = claims.Count(c => c.Status == "Pending");
                var totalClaimAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.ClaimAmount);

                // Get Leaves Data
                var leaves = await _context.Leaves
                    .Include(l => l.LeaveType)
                    .Where(l => l.EmployeeID == currentEmployeeId)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();

                var recentLeaves = leaves.Take(5).ToList();
                var currentYearLeaves = leaves.Where(l => l.StartDate.Year == currentYear).ToList();
                var approvedLeaves = currentYearLeaves.Where(l => l.Status == "Approved").ToList();
                var pendingLeavesCount = leaves.Count(l => l.Status == "Pending");
                var totalLeaveDaysUsed = approvedLeaves.Sum(l => l.LeaveDays);

                // Get Leave Types and Balances
                var leaveTypes = await _context.LeaveTypes.ToListAsync();
                var leaveBalances = new Dictionary<string, object>();

                foreach (var leaveType in leaveTypes)
                {
                    var usedDays = currentYearLeaves
                        .Where(l => l.LeaveTypeID == leaveType.LeaveTypeID &&
                                   (l.Status == "Approved" || l.Status == "Pending"))
                        .Sum(l => l.LeaveDays);

                    var remainingDays = Math.Max(0, leaveType.DefaultDaysPerYear - usedDays);

                    leaveBalances[leaveType.TypeName] = new
                    {
                        LeaveTypeID = leaveType.LeaveTypeID,
                        TypeName = leaveType.TypeName,
                        DefaultDays = leaveType.DefaultDaysPerYear,
                        UsedDays = usedDays,
                        RemainingDays = remainingDays
                    };
                }

                // Get approved leave dates for calendar
                var approvedLeaveDates = approvedLeaves
                    .SelectMany(l => GetDateRange(l.StartDate, l.EndDate))
                    .ToList();

                // Calculate approval rate
                var approvalRate = totalClaimsCount > 0 ? (approvedClaimsCount * 100.0 / totalClaimsCount) : 0;

                // Pass data to view
                ViewBag.TotalClaims = totalClaimsCount;
                ViewBag.ApprovedClaims = approvedClaimsCount;
                ViewBag.PendingClaims = pendingClaimsCount;
                ViewBag.PendingLeaves = pendingLeavesCount;
                ViewBag.TotalLeaveDaysUsed = totalLeaveDaysUsed;
                ViewBag.ApprovalRate = Math.Round(approvalRate, 1);
                ViewBag.TotalClaimAmount = totalClaimAmount;
                ViewBag.RecentClaims = recentClaims;
                ViewBag.RecentLeaves = recentLeaves;
                ViewBag.LeaveBalances = leaveBalances;
                ViewBag.ApprovedLeaveDates = approvedLeaveDates;
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.CurrentYear = currentYear;

                // Calculate remaining leave days (total across all types)
                var totalRemainingLeave = leaveBalances.Values
                    .Cast<dynamic>()
                    .Sum(lb => (int)lb.RemainingDays);
                ViewBag.TotalRemainingLeave = totalRemainingLeave;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                // Set default values in case of error
                ViewBag.TotalClaims = 0;
                ViewBag.ApprovedClaims = 0;
                ViewBag.PendingClaims = 0;
                ViewBag.PendingLeaves = 0;
                ViewBag.TotalLeaveDaysUsed = 0;
                ViewBag.ApprovalRate = 0;
                ViewBag.TotalClaimAmount = 0;
                ViewBag.RecentClaims = new List<Claim>();
                ViewBag.RecentLeaves = new List<LeaveModel>();
                ViewBag.LeaveBalances = new Dictionary<string, object>();
                ViewBag.ApprovedLeaveDates = new List<DateOnly>();
                ViewBag.CurrentMonth = DateTime.Now.ToString("MMMM yyyy");
                ViewBag.TotalRemainingLeave = 0;
            }

            return View("~/Views/Employee/Dashboard.cshtml");
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