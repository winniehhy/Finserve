using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using FinserveNew.Data;
using FinserveNew.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FinserveNew.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public AdminController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        //// GET: Admin/ApprovePayrolls
        //public async Task<IActionResult> ApprovePayrolls()
        //{
        //    var payrolls = await _context.Payrolls
        //        .Include(p => p.Employee)
        //        .OrderByDescending(p => p.Year)
        //        .ThenByDescending(p => p.Month)
        //        .ToListAsync();

        //    return View(payrolls);
        //}

        //// GET: Admin/PayrollDetails/{id}
        //public async Task<IActionResult> PayrollDetails(int id)
        //{
        //    var payroll = await _context.Payrolls
        //        .Include(p => p.Employee)
        //        .FirstOrDefaultAsync(p => p.PayrollID == id);

        //    if (payroll == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(payroll);
        //}

        // GET: Admin/ApprovePayrolls
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

        // GET: Admin/PayrollDetails/{id}
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



        // POST: Admin/ApprovePayroll/{id}
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

        // POST: Admin/RejectPayroll/{id}
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
    }
}
