using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;

namespace FinserveNew.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly AppDbContext _context;

        public ReportController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ReviewReports()
        {
            try
            {
                var invoiceSummary = await GetInvoiceSummaryData();

                ViewBag.InvoiceSummary = new
                {
                    TotalInvoices = invoiceSummary.TotalInvoices,
                    PaidAmount = invoiceSummary.PaidAmount,
                    PendingAmount = invoiceSummary.PendingAmount,
                    OverdueAmount = invoiceSummary.OverdueAmount,
                    SentAmount = invoiceSummary.SentAmount
                };
            }
            catch (Exception ex)
            {
                // Log error and provide empty data
                ViewBag.InvoiceSummary = new { TotalInvoices = 0, PaidAmount = 0m, PendingAmount = 0m, OverdueAmount = 0m, SentAmount = 0m };
            }

            return View("~/Views/Admins/Report/ReviewReports.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoiceStats()
        {
            try
            {
                var summary = await GetInvoiceSummaryData();
                var stats = new
                {
                    totalInvoices = summary.TotalInvoices,
                    paidAmount = $"RM {summary.PaidAmount:N2}",
                    pendingAmount = $"RM {summary.PendingAmount:N2}",
                    overdueAmount = $"RM {summary.OverdueAmount:N2}",
                    sentAmount = $"RM {summary.SentAmount:N2}"
                };
                return Json(stats);
            }
            catch (Exception)
            {
                var stats = new
                {
                    totalInvoices = 0,
                    paidAmount = "RM 0.00",
                    pendingAmount = "RM 0.00",
                    overdueAmount = "RM 0.00",
                    sentAmount = "RM 0.00"
                };
                return Json(stats);
            }
        }

        private async Task<InvoiceSummaryDto> GetInvoiceSummaryData()
        {
            var invoices = await _context.Invoices.ToListAsync();

            return new InvoiceSummaryDto
            {
                TotalInvoices = invoices.Count,
                PaidAmount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                PendingAmount = invoices.Where(i => i.Status == "Pending").Sum(i => i.TotalAmount),
                OverdueAmount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount),
                SentAmount = invoices.Where(i => i.Status == "Sent").Sum(i => i.TotalAmount)
            };
        }
    }

    // DTO class for data transfer
    public class InvoiceSummaryDto
    {
        public int TotalInvoices { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal SentAmount { get; set; }
    }
}