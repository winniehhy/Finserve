using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.UI.Services;
using FinserveNew.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FinserveNew.Controllers
{
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;

        public PayrollController(
            AppDbContext context,
            IEmailSender emailSender,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        // GET: /Payroll - Main entry point (Process page as default)
        public async Task<IActionResult> Index()
        {
            return await Process();
        }

        // GET: /Payroll/Process
        [Authorize(Roles = "Admin,HR")]
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

                // If found, populate the form with existing values
                if (existingPayroll != null)
                {
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
                }
            }

            return View("~/Views/HR/Payroll/Process.cshtml", viewModel);
        }

        // GET: /Payroll/Summary
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(PayrollProcessViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Employees = await _context.Employees
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToListAsync();

                model.Payrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .Where(p => p.Month == model.Month && p.Year == model.Year)
                    .ToListAsync();

                return View("~/Views/HR/Payroll/Process.cshtml", model);
            }

            // Check if an entry already exists for this employee in this month/year
            var existingEntry = await _context.Payrolls
                .FirstOrDefaultAsync(p => p.EmployeeID == model.EmployeeID &&
                                         p.Month == model.Month &&
                                         p.Year == model.Year);

            if (existingEntry != null)
            {
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
            }
            else
            {
                // Create new entry
                var salary = new Payroll
                {
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
                _context.Payrolls.Add(salary);
            }

            await _context.SaveChangesAsync();
            //TempData["Success"] = "Payroll data processed successfully!";
            //return RedirectToAction(nameof(Process));

            // Redirect to Summary instead of Process
            //return RedirectToAction(nameof(Summary), new { month = model.Month, year = model.Year });

            TempData["Success"] = "Payroll data saved successfully!";
            return RedirectToAction(nameof(Process), new { month = model.Month, year = model.Year, employeeId = model.EmployeeID });

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
        public async Task<IActionResult> HistoryByMonth(int month = 0, int year = 0)
        {
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;

            var entries = await _context.Payrolls
                .Include(p => p.Employee)
                .Where(p => p.Month == month && p.Year == year)
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendApprovalRequest(int payrollId)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == payrollId);

            if (payroll == null)
            {
                return NotFound();
            }

            // Update status to pending approval
            payroll.PaymentStatus = "Pending Approval";
            await _context.SaveChangesAsync();

            // Notify admins of the approval request
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var adminUser in adminUsers)
            {
                if (!string.IsNullOrEmpty(adminUser.Email))
                {
                    var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(payroll.Month);
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
                <p><a href='{Url.Action("PayrollDetails", "Admin", new { id = payroll.PayrollID }, Request.Scheme)}'>Click here to review this payroll</a></p>";

                    await _emailSender.SendEmailAsync(adminUser.Email, subject, body);
                }
            }

            TempData["Success"] = "Payroll has been sent for approval.";
            return RedirectToAction(nameof(Summary), new { month = payroll.Month, year = payroll.Year });
        }

        // POST: Payroll/MarkAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int payrollId)
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

            // Update status to completed
            payroll.PaymentStatus = "Completed";
            await _context.SaveChangesAsync();

            // Notify employee
            if (!string.IsNullOrEmpty(payroll.Employee?.Email))
            {
                var monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(payroll.Month);
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


        // GET: Payroll/ApprovePayrolls
        public async Task<IActionResult> ApprovePayrolls()
        {
            try
            {
                var payrolls = await _context.Payrolls
                    .Include(p => p.Employee)
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .ToListAsync();

                // Specify the exact path to the view
                return View("~/Views/Admins/Payroll/ApprovePayrolls.cshtml", payrolls);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in ApprovePayrolls: {ex.Message}");

                // Return an error view or redirect
                TempData["Error"] = "An error occurred while loading payroll approvals.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Payroll/PayrollDetails/{id}
        public async Task<IActionResult> PayrollDetails(int id)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id);

            if (payroll == null)
            {
                return NotFound();
            }

            // Specify the exact path to the view
            return View("~/Views/Admins/Payroll/PayrollDetails.cshtml", payroll);
        }



        // POST: Payroll/ApprovePayroll/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayroll(int id, string comments)
        {
            var payroll = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id);

            if (payroll == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            string approverName = currentUser != null
                ? $"{currentUser.FirstName} {currentUser.LastName}"
                : User.Identity.Name;

            // Update status to approved
            payroll.PaymentStatus = "Approved";
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

        // POST: Payroll/RejectPayroll/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayroll(int id, string reason)
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

            // Update status to rejected
            payroll.PaymentStatus = "Rejected";
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

        // Helper method
        private string GetMonthName(int month)
        {
            return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
        }



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
                       p.PaymentStatus == "Completed")
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ToListAsync();

            var availableYears = await _context.Payrolls
                .Where(p => p.EmployeeID == user.EmployeeID && p.PaymentStatus == "Completed")
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
        public async Task<IActionResult> ViewPayslip(int id)
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

            // Only allow viewing of completed payrolls
            if (payslip.PaymentStatus != "Completed")
            {
                return RedirectToAction(nameof(Payslips));
            }

            return View("~/Views/Employee/Payslips/ViewPayslip.cshtml", payslip);
        }

        // ========================== Employee Actions ========================== //
        // GET: /Payroll/DownloadPayslip/{id}
        [Authorize]
        public async Task<IActionResult> DownloadPayslip(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.EmployeeID))
            {
                return RedirectToAction("Index", "Home");
            }

            var payslip = await _context.Payrolls
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.PayrollID == id && p.EmployeeID == user.EmployeeID);

            if (payslip == null || payslip.PaymentStatus != "Completed")
            {
                return NotFound();
            }

            // This would generate PDF - here we return the view for now
            // In a real implementation, you would use a PDF library
            return View("~/Views/Employee/Payslips/PayslipPdf.cshtml", payslip);
        }
    }
} 