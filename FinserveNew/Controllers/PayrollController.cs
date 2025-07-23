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

namespace FinserveNew.Controllers
{
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;

        public PayrollController(AppDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // GET: /Payroll - Main entry point (Process page as default)
        public async Task<IActionResult> Index()
        {
            return await Process();
        }

        // GET: /Payroll/Process
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
                .OrderBy(p => p.Employee.FirstName)
                .ThenBy(p => p.Employee.LastName)
                .ToListAsync();

            var viewModel = new PayrollProcessViewModel
            {
                Month = month,
                Year = year,
                Payrolls = entries
            };

            return View("~/Views/HR/Payroll/History.cshtml", viewModel);
        }

        // POST: /Payroll/SendApprovalRequest
        [HttpPost]
        public async Task<IActionResult> SendApprovalRequest(int payrollId)
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.PayrollID == payrollId);
            if (payroll == null) return NotFound();

            payroll.PaymentStatus = "Pending Approval";
            await _context.SaveChangesAsync();

            // TODO: Send email to admin for approval 
            var adminEmail = "admin@yourcompany.com";
            var subject = $"Payroll Approval Needed for {payroll.Employee.FirstName} {payroll.Employee.LastName}";
            var body = $"Payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} ({payroll.EmployeeID}) is pending approval.<br/>" +
                       $"<a href='https://yourdomain.com/Admin/Payroll/Approve?payrollId={payroll.PayrollID}'>Approve Now</a>";

            await _emailSender.SendEmailAsync(adminEmail, subject, body);


            return Json(new { success = true });
        }

        // POST: /Payroll/ApprovePayroll
        [HttpPost]
        public async Task<IActionResult> ApprovePayroll(int payrollId)
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.PayrollID == payrollId);
            if (payroll == null) return NotFound();

            payroll.PaymentStatus = "Approved";
            await _context.SaveChangesAsync();

            // TODO: Send email to HR to proceed with payment
            var hrEmail = "hr@yourcompany.com";
            var subject = $"Payroll Approved for {payroll.Employee.FirstName} {payroll.Employee.LastName}";
            var body = $"Payroll for {payroll.Employee.FirstName} {payroll.Employee.LastName} has been approved. You may now proceed with payment.";

            await _emailSender.SendEmailAsync(hrEmail, subject, body);

            return Json(new { success = true });
        }

        // POST: /Payroll/MarkAsPaid
        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int payrollId)
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.PayrollID == payrollId);
            if (payroll == null) return NotFound();

            payroll.PaymentStatus = "Completed";
            await _context.SaveChangesAsync();

            // TODO: Send email to employee about payment
            var subject = "Your Payroll Has Been Paid";
            var body = $"Dear {payroll.Employee.FirstName},<br/>Your salary for {payroll.Month}/{payroll.Year} has been paid. You can now view your payslip in the employee portal.";
            await _emailSender.SendEmailAsync(payroll.Employee.Email, subject, body);

            return Json(new { success = true });
        }

    }
} 