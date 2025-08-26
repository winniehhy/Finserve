using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinserveNew.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportController> _logger;

        public ReportController(AppDbContext context, ILogger<ReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Report/ReviewReports
        public async Task<IActionResult> ReviewReports()
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                
                // Payroll Summary
                var payrollSummary = await GetPayrollSummaryAsync(currentYear);
                ViewBag.PayrollSummary = payrollSummary;

                // Employee Summary
                var employeeSummary = await GetEmployeeSummaryAsync();
                ViewBag.EmployeeSummary = employeeSummary;

                // Invoice Summary (existing)
                var invoiceSummary = await GetInvoiceSummaryAsync();
                ViewBag.InvoiceSummary = invoiceSummary;

                // Get payroll report data for the embedded report
                var payrollReportData = await GetPayrollReportDataAsync();
                ViewBag.PayrollReportData = payrollReportData;

                return View("~/Views/Admins/Report/ReviewReports.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading review reports");
                TempData["Error"] = "An error occurred while loading the reports.";
                return View("~/Views/Admins/Report/ReviewReports.cshtml");
            }
        }

        // GET: Report/PayrollReport
        public async Task<IActionResult> PayrollReport(
                    string reportType = "register",
                    string periodType = "monthly",
                    int? month = null,
                    int year = 0,
                    string employeeId = null,
                    DateTime? dateFrom = null,
                    DateTime? dateTo = null)
        {
            if (year == 0) year = DateTime.Now.Year;
            if (!month.HasValue && periodType == "monthly") month = DateTime.Now.Month;

            var query = _context.Payrolls
                .Include(p => p.Employee)
                .Include(p => p.Approvals)
                .Where(p => p.PaymentStatus == "Completed") // Only show completed payrolls
                .AsQueryable();

            // Apply filters based on period type
            switch (periodType)
            {
                case "monthly":
                    if (month.HasValue)
                        query = query.Where(p => p.Month == month.Value && p.Year == year);
                    else
                        query = query.Where(p => p.Year == year);
                    break;
                case "yearly":
                    query = query.Where(p => p.Year == year);
                    break;
                case "quarterly":
                    // Implement quarterly logic if needed
                    break;
                case "custom":
                    if (dateFrom.HasValue)
                        query = query.Where(p => p.CreatedDate >= dateFrom.Value);
                    if (dateTo.HasValue)
                        query = query.Where(p => p.CreatedDate <= dateTo.Value.AddDays(1));
                    break;
            }

            // Apply employee filter (only for register report)
            if (!string.IsNullOrEmpty(employeeId) && reportType == "register")
            {
                query = query.Where(p => p.EmployeeID == employeeId);
            }

            var payrolls = await query.OrderBy(p => p.EmployeeID) // Sort by employee ID
                .ThenBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToListAsync();

            var allEmployees = await _context.Employees
                .OrderBy(e => e.EmployeeID)
                .ToListAsync();

            var viewModel = new PayrollReportViewModel
            {
                Payrolls = payrolls,
                AllEmployees = allEmployees,
                ReportType = reportType,
                PeriodType = periodType,
                Month = month,
                Year = year,
                EmployeeId = employeeId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                TotalPayrolls = payrolls.Count,
                TotalNetWages = payrolls.Sum(p => p.TotalWages),
                TotalEmployerCost = payrolls.Sum(p => p.TotalEmployerCost),
                AverageSalary = payrolls.Any() ? payrolls.Average(p => p.BasicSalary) : 0,
                CompletedPayrolls = payrolls.Count,
                AvailableYears = await GetAvailableYearsAsync()
            };

            // Pass filter values to view
            ViewBag.ReportType = reportType;
            ViewBag.PeriodType = periodType;
            ViewBag.Month = month;
            ViewBag.Year = year;
            ViewBag.EmployeeId = employeeId;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return PartialView("_PayrollReportContent", viewModel);
        }

        // GET: Report/EmployeeReport
        public async Task<IActionResult> EmployeeReport(
            string? department = null,
            string? status = null,
            string? role = null,
            DateTime? joinDateFrom = null,
            DateTime? joinDateTo = null,
            string? reportType = null)
        {
            try
            {
                var employeeData = await GetEmployeeReportDataAsync(department, status, role, joinDateFrom, joinDateTo);
                
                var reportViewModel = new EmployeeReportViewModel
                {
                    Department = department,
                    Status = status,
                    Role = role,
                    JoinDateFrom = joinDateFrom,
                    JoinDateTo = joinDateTo,
                    Employees = employeeData.Employees,
                    Summary = employeeData.Summary,
                    AvailableDepartments = await GetAvailableDepartmentsAsync(),
                    AvailableRoles = await GetAvailableRolesAsync(),
                    StatusDistribution = employeeData.StatusDistribution,
                    AgeDistribution = employeeData.AgeDistribution,
                    TenureDistribution = employeeData.TenureDistribution
                };

                // Pass report type to view
                ViewBag.ReportType = reportType;

                return PartialView("_EmployeeReportContent", reportViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating employee report partial");
                return PartialView("_EmployeeReportContent", new EmployeeReportViewModel());
            }
        }

        // Helper Methods
        private async Task<object> GetPayrollSummaryAsync(int year)
        {
            var payrolls = await _context.Payrolls
                .Where(p => p.Year == year && p.PaymentStatus == "Completed") // Only completed payrolls
                .ToListAsync();

            return new
            {
                TotalPayrolls = payrolls.Count,
                TotalAmount = payrolls.Sum(p => p.TotalWages),
                TotalEmployerCost = payrolls.Sum(p => p.TotalEmployerCost),
                AverageSalary = payrolls.Any() ? payrolls.Average(p => p.BasicSalary) : 0,
                PendingPayrolls = 0, // Not applicable for completed payrolls
                CompletedPayrolls = payrolls.Count
            };
        }

        private async Task<PayrollReportViewModel> GetPayrollReportDataAsync()
        {
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            var payrolls = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.PaymentStatus == "Completed") // Only completed payrolls
                .OrderBy(p => p.EmployeeID) // Sort by employee ID
                .ThenBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToListAsync();

            var allEmployees = await _context.Employees
                .OrderBy(e => e.EmployeeID)
                .ToListAsync();

            return new PayrollReportViewModel
            {
                Payrolls = payrolls,
                AllEmployees = allEmployees,
                ReportType = "register",
                PeriodType = "monthly",
                Month = currentMonth,
                Year = currentYear,
                TotalPayrolls = payrolls.Count,
                TotalNetWages = payrolls.Sum(p => p.TotalWages),
                TotalEmployerCost = payrolls.Sum(p => p.TotalEmployerCost),
                AverageSalary = payrolls.Any() ? payrolls.Average(p => p.BasicSalary) : 0,
                CompletedPayrolls = payrolls.Count,
                AvailableYears = await GetAvailableYearsAsync()
            };
        }

        private async Task<object> GetEmployeeSummaryAsync()
        {
            var employees = await _context.Employees.ToListAsync();
            var currentDate = DateOnly.FromDateTime(DateTime.Now);

            return new
            {
                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(e => e.ResignationDate == null || e.ResignationDate > currentDate),
                NewHires = employees.Count(e => e.JoinDate >= currentDate.AddMonths(-1)),
                PendingConfirmation = employees.Count(e => e.ConfirmationStatus == "Pending")
            };
        }

        private async Task<object> GetInvoiceSummaryAsync()
        {
            var invoices = await _context.Invoices.ToListAsync();
            
            return new
            {
                TotalInvoices = invoices.Count,
                PaidAmount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                PendingAmount = invoices.Where(i => i.Status == "Pending").Sum(i => i.TotalAmount),
                OverdueAmount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount)
            };
        }

        private async Task<EmployeeReportData> GetEmployeeReportDataAsync(string? department, string? status, string? role, DateTime? joinDateFrom, DateTime? joinDateTo)
        {
            var query = _context.Employees
                .Include(e => e.Role)
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(e => e.ConfirmationStatus == status);

            if (!string.IsNullOrEmpty(role))
                query = query.Where(e => e.Role.RoleName == role);

            if (joinDateFrom.HasValue)
                query = query.Where(e => e.JoinDate >= DateOnly.FromDateTime(joinDateFrom.Value));

            if (joinDateTo.HasValue)
                query = query.Where(e => e.JoinDate <= DateOnly.FromDateTime(joinDateTo.Value));

            var employees = await query.OrderBy(e => e.EmployeeID).ToListAsync();

            // Calculate summary
            var currentDate = DateOnly.FromDateTime(DateTime.Now);
            var summary = new EmployeeSummaryData
            {
                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(e => e.ResignationDate == null || e.ResignationDate > currentDate),
                ResignedEmployees = employees.Count(e => e.ResignationDate != null && e.ResignationDate <= currentDate),
                ConfirmedEmployees = employees.Count(e => e.ConfirmationStatus == "Confirmed"),
                PendingConfirmation = employees.Count(e => e.ConfirmationStatus == "Pending")
            };

            // Status distribution
            var statusDistribution = employees
                .GroupBy(e => e.ConfirmationStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .Cast<object>()
                .ToList();

            // Age distribution
            var ageDistribution = employees
                .Select(e => DateTime.Now.Year - e.DateOfBirth.Year)
                .GroupBy(age => age / 10 * 10) // Group by decades
                .Select(g => new { AgeRange = $"{g.Key}-{g.Key + 9}", Count = g.Count() })
                .Cast<object>()
                .ToList();

            // Tenure distribution
            var tenureDistribution = employees
                .Select(e => DateTime.Now.Year - e.JoinDate.Year)
                .GroupBy(tenure => tenure < 1 ? "< 1 year" : tenure < 5 ? "1-5 years" : tenure < 10 ? "5-10 years" : "10+ years")
                .Select(g => new { TenureRange = g.Key, Count = g.Count() })
                .Cast<object>()
                .ToList();

            return new EmployeeReportData
            {
                Employees = employees,
                Summary = summary,
                StatusDistribution = statusDistribution,
                AgeDistribution = ageDistribution,
                TenureDistribution = tenureDistribution
            };
        }

        private async Task<List<int>> GetAvailableYearsAsync()
        {
            var years = await _context.Payrolls
                .Where(p => p.PaymentStatus == "Completed") // Only completed payrolls
                .Select(p => p.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            return years.Any() ? years : new List<int> { DateTime.Now.Year };
        }

        private async Task<List<Employee>> GetAvailableEmployeesAsync()
        {
            return await _context.Employees
                .OrderBy(e => e.EmployeeID)
                .ToListAsync();
        }

        private async Task<List<string>> GetAvailableDepartmentsAsync()
        {
            return await _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Position))
                .Select(e => e.Position)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
        }

        private async Task<List<string>> GetAvailableRolesAsync()
        {
            return await _context.Employees
                .Include(e => e.Role)
                .Where(e => e.Role != null)
                .Select(e => e.Role.RoleName)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
        }
    }
}