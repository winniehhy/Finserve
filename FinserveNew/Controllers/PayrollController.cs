using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using FinserveNew.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.UI.Services;
using FinserveNew.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace FinserveNew.Controllers
{
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIdGenerationService _idGenerationService;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(
            AppDbContext context,
            IEmailSender emailSender,
            UserManager<ApplicationUser> userManager,
            IIdGenerationService idGenerationService,
            ILogger<PayrollController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _userManager = userManager;
            _idGenerationService = idGenerationService;
            _logger = logger;
        }

        // ========================== HR Actions ========================== //
        // GET: /Payroll - Main entry point (Process page as default)
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Index()
        {
            return await Process();
        }

        // GET: /Payroll/Process
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Process(int month = 0, int year = 0, string employeeId = null)
        {
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;

            var viewModel = new PayrollProcessViewModel
            {
                Month = month,
                Year = year,
                Employees = await _context.Employees
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToListAsync()
            };

            // If employeeId is provided, load that employee's payroll data
            if (!string.IsNullOrEmpty(employeeId))
            {
                viewModel.EmployeeID = employeeId;

                // Try to find existing record for this employee in the selected month/year
                var existingPayroll = await _context.Payrolls
                    .FirstOrDefaultAsync(p => p.EmployeeID == employeeId &&
                                            p.Month == month &&
                                            p.Year == year);

                // If found, check if it can be edited
                if (existingPayroll != null)
                {
                    var canEdit = existingPayroll.PaymentStatus == "Pending" || existingPayroll.PaymentStatus == "Rejected";
                    
                    // If payroll exists but cannot be edited, redirect to view page
                    if (!canEdit)
                    {
                        return RedirectToAction(nameof(ViewPayroll), new { month = month, year = year, employeeId = employeeId });
                    }

                    // If it can be edited, populate the form
                    viewModel.ProjectName = existingPayroll.ProjectName;
                    viewModel.BasicSalary = existingPayroll.BasicSalary;
                    viewModel.EmployerEpf = existingPayroll.EmployerEpf;
                    viewModel.EmployerSocso = existingPayroll.EmployerSocso;
                    viewModel.EmployerEis = existingPayroll.EmployerEis;
                    viewModel.EmployerTax = existingPayroll.EmployerTax;
                    viewModel.EmployerOtherContributions = existingPayroll.EmployerOtherContributions;
                    viewModel.EmployeeEpf = existingPayroll.EmployeeEpf;
                    viewModel.EmployeeSocso = existingPayroll.EmployeeSocso;
                    viewModel.EmployeeEis = existingPayroll.EmployeeEis;
                    viewModel.EmployeeTax = existingPayroll.EmployeeTax;
                    
                    // Pass the current status to the view
                    ViewBag.CurrentPayrollStatus = existingPayroll.PaymentStatus;
                    ViewBag.PayrollID = existingPayroll.PayrollID;
                }
            }

            // Get payroll statuses for employees who have payroll records only
            var employeePayrollStatuses = await _context.Payrolls
                .Where(p => p.Month == month && p.Year == year)
                .Select(p => new { p.EmployeeID, p.PaymentStatus })
                .ToListAsync();

            ViewBag.EmployeePayrollStatuses = employeePayrollStatuses.ToDictionary(x => x.EmployeeID, x => x.PaymentStatus);

            return View("~/Views/HR/Payroll/Process.cshtml", viewModel);
        }

        // GET: /Payroll/Summary
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Summary(int month = 0, int year = 0)
        {
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;

            var payrolls = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Month == month && p.Year == year)
                .ToListAsync();

            var viewModel = new PayrollProcessViewModel
            {
                Month = month,
                Year = year,
                Payrolls = payrolls,
                //TotalEmployerCost = payrolls.Sum(p => p.TotalEmployerCost),
                //TotalWages = payrolls.Sum(p => p.TotalWages)
            };

            return View("~/Views/HR/Payroll/Summary.cshtml", viewModel);
        }


        // POST: /Payroll/Process
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(PayrollProcessViewModel model)
        {
            // Additional server-side validation
            if (!await ValidatePayrollBusinessRules(model))
            {
                model.Employees = await _context.Employees
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToListAsync();

                return View("~/Views/HR/Payroll/Process.cshtml", model);
            }

            if (!ModelState.IsValid)
            {
                model.Employees = await _context.Employees
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToListAsync();

                return View("~/Views/HR/Payroll/Process.cshtml", model);
            }

            // Check for duplicate payroll entry
            var existingEntry = await _context.Payrolls
                .FirstOrDefaultAsync(p => p.EmployeeID == model.EmployeeID &&
                                         p.Month == model.Month &&
                                         p.Year == model.Year);

            if (existingEntry != null)
            {
                var canEdit = existingEntry.PaymentStatus == "Pending" || existingEntry.PaymentStatus == "Rejected";
                if (!canEdit)
                {
                    TempData["Error"] = $"Payroll with status '{existingEntry.PaymentStatus}' cannot be edited.";
                    return RedirectToAction(nameof(Summary), new { month = model.Month, year = model.Year });
                }

                // Update existing entry
                existingEntry.ProjectName = model.ProjectName;
                existingEntry.BasicSalary = model.BasicSalary;
                existingEntry.EmployerEpf = model.EmployerEpf;
                existingEntry.EmployerSocso = model.EmployerSocso;
                existingEntry.EmployerEis = model.EmployerEis;
                existingEntry.EmployerTax = model.EmployerTax;
                existingEntry.EmployerOtherContributions = model.EmployerOtherContributions;
                existingEntry.EmployeeEpf = model.EmployeeEpf;
                existingEntry.EmployeeSocso = model.EmployeeSocso;
                existingEntry.EmployeeEis = model.EmployeeEis;
                existingEntry.EmployeeTax = model.EmployeeTax;
                existingEntry.TotalWages = model.TotalWages;
                existingEntry.TotalEmployerCost = model.TotalEmployerCost;

                // Validate the updated payroll data
                if (!existingEntry.IsValidPayrollData())
                {
                    ModelState.AddModelError("", "Payroll data validation failed. Please check the contribution amounts and percentages.");
                    model.Employees = await _context.Employees
                        .OrderBy(e => e.FirstName)
                        .ThenBy(e => e.LastName)
                        .ToListAsync();
                    return View("~/Views/HR/Payroll/Process.cshtml", model);
                }

                // If it was rejected, reset status to Pending when modified
                if (existingEntry.PaymentStatus == "Rejected")
                {
                    existingEntry.PaymentStatus = "Pending";
                    
                    try
                    {
                        var approvalId = await _idGenerationService.GenerateApprovalIdAsync();
                        var modifiedByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID

                        _context.Approvals.Add(new Approval
                        {
                            ApprovalID = approvalId,
                            ApprovalDate = DateTime.Now,
                            Action = "Modify Rejected Payroll",
                            ActionBy = modifiedByEmployeeId, // Store Employee ID instead of name
                            Status = "Pending",
                            Remarks = "Payroll modified and status reset to Pending",
                            EmployeeID = existingEntry.EmployeeID,
                            PayrollID = existingEntry.PayrollID
                        });

                        TempData["Success"] = "Rejected payroll has been modified and reset to Pending status.";
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Log the error but don't create approval record
                        _logger.LogError(ex, "Could not create approval record for payroll modification");
                        TempData["Success"] = "Rejected payroll has been modified and reset to Pending status. (Note: Approval audit record could not be created)";
                    }
                }
                else
                {
                    TempData["Success"] = "Payroll data updated successfully!";
                }
            }
            else
            {
                // Generate new PayrollID
                var payrollId = await _idGenerationService.GeneratePayrollIdAsync();

                // Create new entry
                var salary = new Payroll
                {
                    PayrollID = payrollId,
                    EmployeeID = model.EmployeeID,
                    Month = model.Month,
                    Year = model.Year,
                    ProjectName = model.ProjectName,
                    BasicSalary = model.BasicSalary,
                    EmployerEpf = model.EmployerEpf,
                    EmployerSocso = model.EmployerSocso,
                    EmployerEis = model.EmployerEis,
                    EmployerTax = model.EmployerTax,
                    EmployerOtherContributions = model.EmployerOtherContributions,
                    EmployeeEpf = model.EmployeeEpf,
                    EmployeeSocso = model.EmployeeSocso,
                    EmployeeEis = model.EmployeeEis,
                    EmployeeTax = model.EmployeeTax,
                    TotalWages = model.TotalWages,
                    TotalEmployerCost = model.TotalEmployerCost,
                    PaymentStatus = "Pending"
                };

                // Validate the new payroll data
                if (!salary.IsValidPayrollData())
                {
                    ModelState.AddModelError("", "Payroll data validation failed. Please check the contribution amounts and percentages.");
                    model.Employees = await _context.Employees
                        .OrderBy(e => e.FirstName)
                        .ThenBy(e => e.LastName)
                        .ToListAsync();
                    return View("~/Views/HR/Payroll/Process.cshtml", model);
                }

                _context.Payrolls.Add(salary);
                TempData["Success"] = "New payroll entry created successfully!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Process), new { month = model.Month, year = model.Year, employeeId = model.EmployeeID });
        }

        // Helper method for payroll business rules validation
        private async Task<bool> ValidatePayrollBusinessRules(PayrollProcessViewModel model)
        {
            var isValid = true;

            // Validate employee exists and is active
            var employee = await _context.Employees.FindAsync(model.EmployeeID);
            if (employee == null)
            {
                ModelState.AddModelError("EmployeeID", "Selected employee does not exist.");
                isValid = false;
            }
            else if (employee.ConfirmationStatus == "Terminated")
            {
                ModelState.AddModelError("EmployeeID", "Cannot create payroll for terminated employee.");
                isValid = false;
            }

            // Validate date ranges
            if (model.Month < 1 || model.Month > 12)
            {
                ModelState.AddModelError("Month", "Month must be between 1 and 12.");
                isValid = false;
            }

            if (model.Year < 2000 || model.Year > DateTime.Now.Year + 1)
            {
                ModelState.AddModelError("Year", "Year must be between 2000 and next year.");
                isValid = false;
            }

            // Validate EPF contributions (should be within reasonable ranges)
            if (model.BasicSalary > 0)
            {
                var employerEpfPercentage = (model.EmployerEpf / model.BasicSalary) * 100;
                var employeeEpfPercentage = (model.EmployeeEpf / model.BasicSalary) * 100;

                if (employerEpfPercentage > 20)
                {
                    ModelState.AddModelError("EmployerEpf", "Employer EPF contribution seems unusually high (>20% of basic salary).");
                    isValid = false;
                }

                if (employeeEpfPercentage > 15)
                {
                    ModelState.AddModelError("EmployeeEpf", "Employee EPF contribution seems unusually high (>15% of basic salary).");
                    isValid = false;
                }

                // Check if total deductions exceed basic salary
                var totalDeductions = model.EmployeeEpf + model.EmployeeSocso + model.EmployeeEis + model.EmployeeTax;
                if (totalDeductions >= model.BasicSalary)
                {
                    ModelState.AddModelError("", "Total employee deductions cannot equal or exceed basic salary.");
                    isValid = false;
                }

                // Net salary should be reasonable (at least 50% of basic salary)
                var netSalary = model.BasicSalary - totalDeductions;
                if (netSalary < (model.BasicSalary * 0.5m))
                {
                    ModelState.AddModelError("", "Net salary after deductions is unusually low. Please verify the deduction amounts.");
                    isValid = false;
                }
            }

            // Validate SOCSO contribution limits (varies by salary level)
            if (model.BasicSalary <= 4000)
            {
                if (model.EmployeeSocso > 19.75m || model.EmployerSocso > 33.25m)
                {
                    ModelState.AddModelError("", "SOCSO contribution amounts exceed maximum limits for this salary level.");
                    isValid = false;
                }
            }

            // Validate EIS contribution limits
            if (model.BasicSalary > 0)
            {
                var maxEis = model.BasicSalary * 0.005m; // 0.5% max
                if (model.EmployeeEis > maxEis || model.EmployerEis > maxEis)
                {
                    ModelState.AddModelError("", "EIS contribution cannot exceed 0.5% of basic salary.");
                    isValid = false;
                }
            }

            return isValid;
        }

        // GET: /Payroll/GetPreviousMonthData
        [HttpGet]
        public async Task<IActionResult> GetPreviousMonthData(string employeeId, int month, int year)
        {
            // Calculate previous month/year
            int prevMonth = month == 1 ? 12 : month - 1;
            int prevYear = month == 1 ? year - 1 : year;

            var previousEntry = await _context.Payrolls
                .FirstOrDefaultAsync(p => p.EmployeeID == employeeId &&
                                         p.Month == prevMonth &&
                                         p.Year == prevYear);

            if (previousEntry == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                basicSalary = previousEntry.BasicSalary,
                projectName = previousEntry.ProjectName,
                employerEpf = previousEntry.EmployerEpf,
                employerSocso = previousEntry.EmployerSocso,
                employerEis = previousEntry.EmployerEis,
                employerTax = previousEntry.EmployerTax,
                employerOtherContributions = previousEntry.EmployerOtherContributions,
                employeeEpf = previousEntry.EmployeeEpf,
                employeeSocso = previousEntry.EmployeeSocso,
                employeeEis = previousEntry.EmployeeEis,
                employeeTax = previousEntry.EmployeeTax
            });
        }

        // GET: /Payroll/HistoryByMonth
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HistoryByMonth(int month = 0, int year = 0)
        {
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;

            // Only show paid payrolls in history
            var entries = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Month == month && p.Year == year && p.PaymentStatus == "Paid")
                .OrderBy(p => p.EmployeeID)
                .ToListAsync();

            var viewModel = new PayrollProcessViewModel
            {
                Month = month,
                Year = year,
                Payrolls = entries
            };

            return View("~/Views/HR/Payroll/History.cshtml", viewModel);
        }

        // POST: Payroll/SendApprovalRequest
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendApprovalRequest(string payrollId)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == payrollId);

            if (payroll == null)
            {
                return NotFound();
            }

            // Check if payroll can be sent for approval
            if (payroll.PaymentStatus != "Pending")
            {
                TempData["Error"] = $"Only payrolls with 'Pending' status can be sent for approval. Current status: {payroll.PaymentStatus}";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }

            try
            {
                // Update status to pending approval
                payroll.PaymentStatus = "Pending Approval";

                // Generate new ApprovalID
                var approvalId = await _idGenerationService.GenerateApprovalIdAsync();

                // Record approval audit entry
                var requestedByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(payroll.Month);

                _context.Approvals.Add(new Approval
                {
                    ApprovalID = approvalId,
                    ApprovalDate = DateTime.Now,
                    Action = "Send for Approval",
                    ActionBy = requestedByEmployeeId, // Store Employee ID instead of name
                    Status = "Pending Approval",
                    Remarks = "Sent for approval",
                    EmployeeID = payroll.EmployeeID,
                    PayrollID = payroll.PayrollID
                });

                await _context.SaveChangesAsync();

                // Notify Senior HR of the approval request
                var seniorHRUsers = await _userManager.GetUsersInRoleAsync("Senior HR");
                foreach (var seniorHRUser in seniorHRUsers)
                {
                    if (!string.IsNullOrEmpty(seniorHRUser.Email))
                    {
                        var subject = $"Payroll Approval Request for {payroll.Employee.FirstName} {payroll.Employee.LastName}";
                        var body = $@"
                    <h2>Payroll Approval Request</h2>
                    <p>A new payroll entry requires your approval:</p>
                    <ul>
                        <li><strong>Employee:</strong> {payroll.Employee.FirstName} {payroll.Employee.LastName}</li>
                        <li><strong>Period:</strong> {monthName} {payroll.Year}</li>
                        <li><strong>Basic Salary:</strong> RM {payroll.BasicSalary:N2}</li>
                        <li><strong>Net Salary:</strong> RM {payroll.TotalWages:N2}</li>
                        <li><strong>Total Cost:</strong> RM {payroll.TotalEmployerCost:N2}</li>
                    </ul>
                    <p><a href='{Url.Action("PayrollDetails", "Payroll", new { id = payroll.PayrollID }, Request.Scheme)}'>Click here to review this payroll</a></p>";

                        await _emailSender.SendEmailAsync(seniorHRUser.Email, subject, body);
                        //await _emailSender.SendEmailAsync("hr001@cubicsoftware.com.my", subject, body);
                    }
                }

                TempData["Success"] = "Payroll has been sent for approval.";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to send for approval: {ex.Message}";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while sending for approval. Please try again.";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
        }

        // POST: Payroll/MarkAsPaid
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(string payrollId)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == payrollId);

            if (payroll == null)
            {
                return NotFound();
            }

            if (payroll.PaymentStatus != "Approved")
            {
                TempData["Error"] = "Only approved payrolls can be marked as paid.";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }

            try
            {
                // Generate new ApprovalID
                var approvalId = await _idGenerationService.GenerateApprovalIdAsync();

                // Update status to paid and record approval trail
                payroll.PaymentStatus = "Paid";

                var paidByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(payroll.Month);

                _context.Approvals.Add(new Approval
                {
                    ApprovalID = approvalId,
                    ApprovalDate = DateTime.Now,
                    Action = "Mark as Paid",
                    ActionBy = paidByEmployeeId, // Store Employee ID instead of name
                    Status = "Paid",
                    Remarks = "Marked as paid",
                    EmployeeID = payroll.EmployeeID,
                    PayrollID = payroll.PayrollID
                });

                await _context.SaveChangesAsync();

                // Notify employee
                if (!string.IsNullOrEmpty(payroll.Employee?.Email))
                {
                    var subject = $"Your Salary for {monthName} {payroll.Year} Has Been Paid";
                    var body = $@"
                <h2>Salary Payment Notification</h2>
                <p>Dear {payroll.Employee.FirstName},</p>
                <p>Your salary for {monthName} {payroll.Year} has been paid to your bank account.</p>
                <ul>
                    <li><strong>Net Amount:</strong> RM {payroll.TotalWages:N2}</li>
                    <li><strong>Period:</strong> {monthName} {payroll.Year}</li>
                </ul>
                <p>You can now view your payslip online.</p>";

                    await _emailSender.SendEmailAsync(payroll.Employee.Email, subject, body);
                }

                TempData["Success"] = "Payroll has been marked as paid.";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to mark as paid: {ex.Message}";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while marking as paid. Please try again.";
                return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
            }
        }


        // ========================== Senior HR Actions ========================== //
        // GET: Payroll/ApprovePayrolls
        [Authorize(Roles = "Senior HR")]
        public async Task<IActionResult> ApprovePayrolls(
            string searchTerm = "",
            int? filterMonth = null,
            int? filterYear = null,
            string filterEmployee = "",
            decimal? minAmount = null,
            decimal? maxAmount = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string activeTab = "")
        {
            try
            {
                // Get all payrolls with their approval history
                var payrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Include(p => p.Approvals)
                        .ThenInclude(a => a.ActionByEmployee)
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .ToListAsync();

                // Get all employees for dropdown
                var allEmployees = await _context.Employees
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToListAsync();

                // Get available years
                var availableYears = payrolls
                    .Select(p => p.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList();

                // Apply filters (status filter removed)
                var filteredPayrolls = ApplyFilters(payrolls, searchTerm, filterMonth, filterYear, 
                                                  filterEmployee, minAmount, maxAmount, 
                                                  dateFrom, dateTo);

                // Create a view model that includes approval-based filtering
                var viewModel = new PayrollApprovalViewModel
                {
                    AllPayrolls = filteredPayrolls,
                    PendingPayrolls = filteredPayrolls.Where(p => p.PaymentStatus == "Pending Approval").ToList(),
                    ApprovedPayrolls = GetApprovedPayrolls(filteredPayrolls),
                    RejectedPayrolls = GetRejectedPayrolls(filteredPayrolls),
                    AllApprovalHistory = GetAllApprovalHistory(filteredPayrolls),
                    
                    // Filter values (status filter removed)
                    SearchTerm = searchTerm,
                    FilterMonth = filterMonth,
                    FilterYear = filterYear,
                    FilterEmployee = filterEmployee,
                    MinAmount = minAmount,
                    MaxAmount = maxAmount,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    
                    // Dropdown data
                    AllEmployees = allEmployees,
                    AvailableYears = availableYears
                };

                // Pass active tab to view
                ViewBag.ActiveTab = activeTab;

                return View("~/Views/SeniorHR/Payroll/ApprovePayrolls.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ApprovePayrolls: {ex.Message}");
                TempData["Error"] = "An error occurred while loading payroll approvals.";
                return RedirectToAction("Index", "Home");
            }
        }


        // GET: Payroll/PayrollDetails/{id}
        [Authorize(Roles = "Senior HR")]
        public async Task<IActionResult> PayrollDetails(string id)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .Include(p => p.Approvals)
                    .ThenInclude(a => a.ActionByEmployee) // Include ActionBy employee details
                .FirstOrDefaultAsync(p => p.PayrollID == id);

            if (payroll == null)
            {
                return NotFound();
            }

            return View("~/Views/SeniorHR/Payroll/PayrollDetails.cshtml", payroll);
        }

        // POST: Payroll/ApprovePayroll/{id}
        [Authorize(Roles = "Senior HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayroll(string id, string comments)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id);

            if (payroll == null)
            {
                return NotFound();
            }

            try
            {
                var approverEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                // Generate new ApprovalID
                var approvalId = await _idGenerationService.GenerateApprovalIdAsync();

                // Update status to approved and record approval entry
                payroll.PaymentStatus = "Approved";
                var monthName = GetMonthName(payroll.Month);
                _context.Approvals.Add(new Approval
                {
                    ApprovalID = approvalId,
                    ApprovalDate = DateTime.Now,
                    Action = "Approve payroll",
                    ActionBy = approverEmployeeId, // Store Employee ID instead of name
                    Status = "Approved",
                    Remarks = comments,
                    EmployeeID = payroll.EmployeeID,
                    PayrollID = payroll.PayrollID
                });

                await _context.SaveChangesAsync();

                // Notify HR
                var hrUsers = await _userManager.GetUsersInRoleAsync("HR");
                foreach (var hrUser in hrUsers)
                {
                    if (!string.IsNullOrEmpty(hrUser.Email))
                    {
                        var subject = $"Payroll Approved for {payroll.Employee.FirstName} {payroll.Employee.LastName}";
                        var message = $@"
                            <h2>Payroll Approval Notification</h2>
                            <p>The payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} for {GetMonthName(payroll.Month)} {payroll.Year} has been approved.</p>
                            <p>You may now proceed with payment.</p>
                            <p><a href='{Url.Action("Summary", "Payroll", new { month = payroll.Month, year = payroll.Year }, Request.Scheme)}'>View Payroll Summary</a></p>";

                        await _emailSender.SendEmailAsync(hrUser.Email, subject, message);
                    }
                }

                TempData["Success"] = $"Payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} has been approved successfully.";
                return RedirectToAction(nameof(ApprovePayrolls));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to process approval: {ex.Message}";
                return RedirectToAction(nameof(PayrollDetails), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while processing the approval. Please try again.";
                return RedirectToAction(nameof(PayrollDetails), new { id });
            }
        }

        // POST: Payroll/RejectPayroll/{id}
        [Authorize(Roles = "Senior HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayroll(string id, string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                TempData["Error"] = "Rejection reason is required.";
                return RedirectToAction(nameof(PayrollDetails), new { id });
            }

            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id);

            if (payroll == null)
            {
                return NotFound();
            }

            try
            {
                // Generate new ApprovalID
                var approvalId = await _idGenerationService.GenerateApprovalIdAsync();

                // Update status to rejected and record approval entry
                payroll.PaymentStatus = "Rejected";
                var rejectedByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                var monthName = GetMonthName(payroll.Month);

                _context.Approvals.Add(new Approval
                {
                    ApprovalID = approvalId,
                    ApprovalDate = DateTime.Now,
                    Action = "Reject payroll",
                    ActionBy = rejectedByEmployeeId, // Store Employee ID instead of name
                    Status = "Rejected",
                    Remarks = reason,
                    EmployeeID = payroll.EmployeeID,
                    PayrollID = payroll.PayrollID
                });

                await _context.SaveChangesAsync();

                // Notify HR
                var hrUsers = await _userManager.GetUsersInRoleAsync("HR");
                foreach (var hrUser in hrUsers)
                {
                    if (!string.IsNullOrEmpty(hrUser.Email))
                    {
                        var subject = $"Payroll Rejected for {payroll.Employee.FirstName} {payroll.Employee.LastName}";
                        var message = $@"
                        <h2>Payroll Rejection Notification</h2>
                        <p>The payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} for {GetMonthName(payroll.Month)} {payroll.Year} has been rejected.</p>
                        <p><strong>Reason:</strong> {reason}</p>
                        <p><a href='{Url.Action("Process", "Payroll", new { month = payroll.Month, year = payroll.Year, employeeId = payroll.EmployeeID }, Request.Scheme)}'>Edit Payroll Entry</a></p>";

                        await _emailSender.SendEmailAsync(hrUser.Email, subject, message);
                    }
                }

                TempData["Success"] = $"Payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} has been rejected.";
                return RedirectToAction(nameof(ApprovePayrolls));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to process rejection: {ex.Message}";
                return RedirectToAction(nameof(PayrollDetails), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while processing the rejection. Please try again.";
                return RedirectToAction(nameof(PayrollDetails), new { id });
            }
        }

        // Helper method
        private string GetMonthName(int month)
        {
            return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
        }


        // ========================== Employee Actions ========================== //
        // GET: /Payroll/Payslips
        [Authorize] // Ensure users are logged in
        public async Task<IActionResult> Payslips(int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.EmployeeID))
            {
                return RedirectToAction("Index", "Home");
            }

            if (!year.HasValue)
            {
                year = DateTime.Now.Year;
            }

            var payslips = await _context.Payrolls
                .Where(p => p.EmployeeID == user.EmployeeID &&
                       p.Year == year &&
                       p.PaymentStatus == "Paid")
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ToListAsync();

            var availableYears = await _context.Payrolls
                .Where(p => p.EmployeeID == user.EmployeeID && p.PaymentStatus == "Paid")
                .Select(p => p.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            ViewBag.SelectedYear = year;
            ViewBag.AvailableYears = availableYears;

            return View("~/Views/Employee/Payslips/Index.cshtml", payslips);
        }

        // GET: /Payroll/ViewPayslip/{id}
        [Authorize]
        public async Task<IActionResult> ViewPayslip(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.EmployeeID))
            {
                return RedirectToAction("Index", "Home");
            }

            var payslip = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id && p.EmployeeID == user.EmployeeID);

            if (payslip == null)
            {
                return NotFound();
            }

            // Only allow viewing of paid payrolls
            if (payslip.PaymentStatus != "Paid")
            {
                return RedirectToAction(nameof(Payslips));
            }

            return View("~/Views/Employee/Payslips/ViewPayslip.cshtml", payslip);
        }

        // GET: /Payroll/DownloadPayslip/{id}
        [Authorize]
        public async Task<IActionResult> DownloadPayslip(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.EmployeeID))
            {
                return RedirectToAction("Index", "Home");
            }

            var payslip = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id && p.EmployeeID == user.EmployeeID);

            if (payslip == null || payslip.PaymentStatus != "Paid")
            {
                return NotFound();
            }

            // Generate the PDF directly from the Razor view
            var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(payslip.Month);
            string fileName = $"Payslip-{payslip.Employee.FirstName}{payslip.Employee.LastName}-{monthName}{payslip.Year}.pdf";

            // Return the PDF with a filename
            return new Rotativa.AspNetCore.ViewAsPdf("~/Views/Employee/Payslips/PayslipPdf.cshtml", payslip)
            {
                FileName = fileName,
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageMargins = { Left = 10, Right = 10, Top = 10, Bottom = 10 }
            };
        }

        // GET: /Payroll/GetPendingPayrollCount
        [HttpGet]
        public async Task<IActionResult> GetPendingPayrollCount()
        {
            try
            {
                int count = await _context.Payrolls
                    .CountAsync(p => p.PaymentStatus == "Pending Approval");
                
                return Json(new { count = count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0 });
            }
        }

        // GET: /Payroll/ViewPayroll - For viewing non-editable payrolls
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ViewPayroll(int month = 0, int year = 0, string employeeId = null)
        {
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;

            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee ID is required to view payroll.";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }

            // Try to find existing record for this employee in the selected month/year
            var existingPayroll = await _context.Payrolls
                .Include(p => p.Employee)
                .Include(p => p.Approvals)
                    .ThenInclude(a => a.ActionByEmployee) // Include ActionBy employee details
                .FirstOrDefaultAsync(p => p.EmployeeID == employeeId &&
                                        p.Month == month &&
                                        p.Year == year);

            if (existingPayroll == null)
            {
                TempData["Error"] = "Payroll record not found.";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }

            var viewModel = new PayrollProcessViewModel
            {
                Month = month,
                Year = year,
                EmployeeID = employeeId,
                ProjectName = existingPayroll.ProjectName,
                BasicSalary = existingPayroll.BasicSalary,
                EmployerEpf = existingPayroll.EmployerEpf,
                EmployerSocso = existingPayroll.EmployerSocso,
                EmployerEis = existingPayroll.EmployerEis,
                EmployerTax = existingPayroll.EmployerTax,
                EmployerOtherContributions = existingPayroll.EmployerOtherContributions,
                EmployeeEpf = existingPayroll.EmployeeEpf,
                EmployeeSocso = existingPayroll.EmployeeSocso,
                EmployeeEis = existingPayroll.EmployeeEis,
                EmployeeTax = existingPayroll.EmployeeTax,
                //TotalWages = existingPayroll.TotalWages,
                //TotalEmployerCost = existingPayroll.TotalEmployerCost
            };

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeId);

            // Pass additional data for the view
            ViewBag.CurrentPayrollStatus = existingPayroll.PaymentStatus;
            ViewBag.PayrollID = existingPayroll.PayrollID;
            ViewBag.Employee = employee;
            ViewBag.IsReadOnly = true;
            
            // Get rejection reason if payroll was rejected
            if (existingPayroll.PaymentStatus == "Rejected")
            {
                var rejectionApproval = existingPayroll.Approvals?
                    .Where(a => a.Status == "Rejected")
                    .OrderByDescending(a => a.ApprovalDate)
                    .FirstOrDefault();
                ViewBag.RejectionReason = rejectionApproval?.Remarks ?? "No rejection reason provided.";
                ViewBag.RejectedBy = rejectionApproval?.ActionByName ?? "Unknown"; // Use ActionByName
                ViewBag.RejectedDate = rejectionApproval?.ApprovalDate;
            }

            return View("~/Views/HR/Payroll/ViewPayroll.cshtml", viewModel);
        }

        // POST: Payroll/BatchSendForApproval
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchSendForApproval(int month, int year)
        {
            try
            {
                // Get all pending payrolls for the specified month/year
                var pendingPayrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Where(p => p.Month == month && p.Year == year && p.PaymentStatus == "Pending")
                    .ToListAsync();

                if (!pendingPayrolls.Any())
                {
                    TempData["Error"] = "No pending payrolls found to send for approval.";
                    return RedirectToAction(nameof(Summary), new { month = month, year = year });
                }

                var requestedByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                int successCount = 0;
                var approvalEntries = new List<Approval>();

                var approvalIds = await _idGenerationService.GenerateBatchApprovalIdsAsync(pendingPayrolls.Count);

                for (int i = 0; i < pendingPayrolls.Count; i++)
                {
                    var payroll = pendingPayrolls[i];
                    payroll.PaymentStatus = "Pending Approval";

                    var approvalEntry = new Approval
                    {
                        ApprovalID = approvalIds[i],
                        ApprovalDate = DateTime.Now,
                        Action = "Send for Approval (Batch)",
                        ActionBy = requestedByEmployeeId, // Store Employee ID instead of name
                        Status = "Pending Approval",
                        Remarks = "Sent for approval via batch operation",
                        EmployeeID = payroll.EmployeeID,
                        PayrollID = payroll.PayrollID
                    };

                    approvalEntries.Add(approvalEntry);
                    successCount++;
                }

                // Add all approval entries and save changes first
                _context.Approvals.AddRange(approvalEntries);
                await _context.SaveChangesAsync();

                // Send email notifications separately (after database update)
                try
                {
                    var seniorHRUsers = await _userManager.GetUsersInRoleAsync("Senior HR");
                    foreach (var seniorHRUser in seniorHRUsers)
                    {
                        if (!string.IsNullOrEmpty(seniorHRUser.Email))
                        {
                            var subject = $"Batch Payroll Approval Request - {monthName} {year}";
                            var body = $@"
                            <h2>Batch Payroll Approval Request</h2>
                            <p><strong>{successCount}</strong> payroll entries require your approval for <strong>{monthName} {year}</strong>:</p>
                            <ul>
                                {string.Join("", pendingPayrolls.Select(p => $"<li>{p.Employee.FirstName} {p.Employee.LastName} - RM {p.TotalWages:N2}</li>"))}
                            </ul>
                            <p><strong>Total Amount:</strong> RM {pendingPayrolls.Sum(p => p.TotalWages):N2}</p>
                            <p><a href='{Url.Action("ApprovePayrolls", "Payroll", null, Request.Scheme)}'>Click here to review these payrolls</a></p>";

                            await _emailSender.SendEmailAsync(seniorHRUser.Email, subject, body);
                        }
                    }
                    TempData["Success"] = $"Successfully sent {successCount} payroll(s) for approval. Senior HR has been notified.";
                }
                catch (Exception emailEx)
                {
                    // Email failed but payrolls were updated successfully
                    Console.WriteLine($"Email notification failed: {emailEx.Message}");
                    TempData["Success"] = $"Successfully sent {successCount} payroll(s) for approval. Note: Email notification to Senior HR failed.";
                }

                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to process batch approval request: {ex.Message}";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
            catch (Exception ex)
            {
                // Log the detailed error
                Console.WriteLine($"Error in BatchSendForApproval: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                TempData["Error"] = "An error occurred while processing the batch approval request. Please try again.";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
        }

        // POST: Payroll/BatchMarkAsPaid
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchMarkAsPaid(int month, int year)
        {
            try
            {
                // Get all approved payrolls for the specified month/year
                var approvedPayrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Where(p => p.Month == month && p.Year == year && p.PaymentStatus == "Approved")
                    .ToListAsync();

                if (!approvedPayrolls.Any())
                {
                    TempData["Error"] = "No approved payrolls found to mark as paid.";
                    return RedirectToAction(nameof(Summary), new { month = month, year = year });
                }

                var paidByEmployeeId = await GetCurrentUserEmployeeIdAsync(); // Store Employee ID
                var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                int successCount = 0;
                var approvalEntries = new List<Approval>();

                // Process each approved payroll and generate unique approval IDs
                var approvalIds = await _idGenerationService.GenerateBatchApprovalIdsAsync(approvedPayrolls.Count);

                for (int i = 0; i < approvedPayrolls.Count; i++)
                {
                    var payroll = approvedPayrolls[i];
                    payroll.PaymentStatus = "Paid";

                    // Create approval audit entry
                    var approvalEntry = new Approval
                    {
                        ApprovalID = approvalIds[i],
                        ApprovalDate = DateTime.Now,
                        Action = "Mark as Paid (Batch)",
                        ActionBy = paidByEmployeeId, // Store Employee ID instead of name
                        Status = "Paid",
                        Remarks = "Marked as paid via batch operation",
                        EmployeeID = payroll.EmployeeID,
                        PayrollID = payroll.PayrollID
                    };

                    approvalEntries.Add(approvalEntry);
                    successCount++;
                }

                // Add all approval entries at once and save database changes first
                _context.Approvals.AddRange(approvalEntries);
                await _context.SaveChangesAsync();

                // Send email notifications separately (after database update)
                int emailSuccessCount = 0;
                int emailFailureCount = 0;

                foreach (var payroll in approvedPayrolls)
                {
                    if (!string.IsNullOrEmpty(payroll.Employee?.Email))
                    {
                        try
                        {
                            var subject = $"Your Salary for {monthName} {year} Has Been Paid";
                            var body = $@"
                            <h2>Salary Payment Notification</h2>
                            <p>Dear {payroll.Employee.FirstName},</p>
                            <p>Your salary for {monthName} {year} has been paid to your bank account.</p>
                            <ul>
                                <li><strong>Net Amount:</strong> RM {payroll.TotalWages:N2}</li>
                                <li><strong>Period:</strong> {monthName} {year}</li>
                            </ul>
                            <p>You can now view your payslip online.</p>";

                            await _emailSender.SendEmailAsync(payroll.Employee.Email, subject, body);
                            emailSuccessCount++;
                        }
                        catch (Exception emailEx)
                        {
                            // Log email failure but don't fail the entire operation
                            Console.WriteLine($"Failed to send email to {payroll.Employee.Email}: {emailEx.Message}");
                            emailFailureCount++;
                        }
                    }
                }

                // Provide detailed success message
                var message = $"Successfully marked {successCount} payroll(s) as paid.";
                if (emailSuccessCount > 0)
                {
                    message += $" {emailSuccessCount} employees were notified via email.";
                }
                if (emailFailureCount > 0)
                {
                    message += $" Note: {emailFailureCount} email notifications failed to send.";
                }

                TempData["Success"] = message;
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = $"Unable to process batch payment operation: {ex.Message}";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
            catch (Exception ex)
            {
                // Log the detailed error
                Console.WriteLine($"Error in BatchMarkAsPaid: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                TempData["Error"] = "An error occurred while processing the batch payment operation. Please try again.";
                return RedirectToAction(nameof(Summary), new { month = month, year = year });
            }
        }

        // GET: Simplified AJAX endpoint
        [HttpGet]
        public async Task<IActionResult> GetEmployeePayrollStatus(int month, int year)
        {
            var employeePayrollStatuses = await _context.Payrolls
                .Where(p => p.Month == month && p.Year == year)
                .Select(p => new { p.EmployeeID, p.PaymentStatus })
                .ToListAsync();

            return Json(employeePayrollStatuses.ToDictionary(x => x.EmployeeID, x => x.PaymentStatus));
        }

        // Helper method to get current user's employee ID
        private async Task<string> GetCurrentUserEmployeeIdAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && !string.IsNullOrEmpty(currentUser.EmployeeID))
            {
                // Verify the employee ID exists in the employees table
                var employeeExists = await _context.Employees
                    .AnyAsync(e => e.EmployeeID == currentUser.EmployeeID);
                if (employeeExists)
                {
                    return currentUser.EmployeeID;
                }
            }
            
            // Fallback: try to find by username or email
            if (currentUser != null)
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == currentUser.Email || e.Username == currentUser.UserName);
                if (employee != null)
                {
                    return employee.EmployeeID;
                }
            }
            
            // If no valid employee ID found, throw an exception rather than returning invalid data
            throw new InvalidOperationException($"Cannot find valid Employee ID for current user: {currentUser?.UserName ?? "Unknown"}. Please ensure the user is properly linked to an employee record.");
        }

        // Helper methods to get approval-based data
        private List<Payroll> GetApprovedPayrolls(List<Payroll> payrolls)
        {
            return payrolls.Where(p => p.Approvals.Any(a => a.Status == "Approved")).ToList();
        }

        private List<Payroll> GetRejectedPayrolls(List<Payroll> payrolls)
        {
            return payrolls.Where(p => p.Approvals.Any(a => a.Status == "Rejected")).ToList();
        }

        private List<ApprovalHistoryItem> GetAllApprovalHistory(List<Payroll> payrolls)
        {
            return payrolls
                .SelectMany(p => p.Approvals.Where(a => a.Status == "Approved" || a.Status == "Rejected")
                    .Select(a => new ApprovalHistoryItem
                    { 
                        Payroll = p, 
                        Approval = a 
                    }))
                .OrderByDescending(x => x.Approval.ApprovalDate)
                .ToList();
        }

        // Helper method to apply filters (status filter removed)
        private List<Payroll> ApplyFilters(List<Payroll> payrolls, string searchTerm, int? filterMonth, 
            int? filterYear, string filterEmployee, decimal? minAmount, 
            decimal? maxAmount, DateTime? dateFrom, DateTime? dateTo)
        {
            var filtered = payrolls.AsQueryable();

            // Search term filter (searches employee name, ID, and payroll ID)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                filtered = filtered.Where(p => 
                    p.EmployeeID.ToLower().Contains(searchTerm) ||
                    p.PayrollID.ToLower().Contains(searchTerm) ||
                    p.Employee.FirstName.ToLower().Contains(searchTerm) ||
                    p.Employee.LastName.ToLower().Contains(searchTerm) ||
                    (p.Employee.FirstName + " " + p.Employee.LastName).ToLower().Contains(searchTerm));
            }

            // Month filter
            if (filterMonth.HasValue)
            {
                filtered = filtered.Where(p => p.Month == filterMonth.Value);
            }

            // Year filter
            if (filterYear.HasValue)
            {
                filtered = filtered.Where(p => p.Year == filterYear.Value);
            }

            // Employee filter
            if (!string.IsNullOrEmpty(filterEmployee))
            {
                filtered = filtered.Where(p => p.EmployeeID == filterEmployee);
            }

            // Amount range filter
            if (minAmount.HasValue)
            {
                filtered = filtered.Where(p => p.TotalWages >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                filtered = filtered.Where(p => p.TotalWages <= maxAmount.Value);
            }

            // Date range filter (based on creation or last approval date)
            if (dateFrom.HasValue || dateTo.HasValue)
            {
                filtered = filtered.Where(p => p.Approvals.Any(a => 
                    (!dateFrom.HasValue || a.ApprovalDate >= dateFrom.Value) &&
                    (!dateTo.HasValue || a.ApprovalDate <= dateTo.Value.AddDays(1))));
            }

            return filtered.ToList();
        }
    }
}