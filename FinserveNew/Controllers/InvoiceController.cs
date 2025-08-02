using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using FinserveNew.Models;
using FinserveNew.Data;

namespace FinserveNew.Controllers
{
    [Authorize(Roles = "Admin")] // Only Admins can access
    public class InvoiceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(AppDbContext context, IWebHostEnvironment webHostEnvironment,
                               UserManager<ApplicationUser> userManager, ILogger<InvoiceController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Invoice/InvoiceRecord (Admin view - all invoices)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> InvoiceRecord()
        {
            var invoices = await _context.Invoices
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            return View("~/Views/Admins/Invoice/InvoiceRecord.cshtml", invoices);
        }

        // Keep existing Index method for backward compatibility
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            return View("~/Views/Admins/Invoice/InvoiceRecord.cshtml", invoices);
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(m => m.InvoiceID == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return View("~/Views/Admins/Invoice/Details.cshtml", invoice);
        }

        // GET: Invoice/Create
        public IActionResult Create()
        {
            var invoice = new Invoice
            {
                IssueDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(30),
                Year = DateTime.Now.Year,
                Currency = "MYR",
                Status = "Draft"
            };

            return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
        }

        // POST: Invoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice, IFormFile invoiceFile)
        {
            try
            {
                // Auto-generate InvoiceNumber
                var lastInvoice = await _context.Invoices
                    .OrderByDescending(i => i.InvoiceID)
                    .FirstOrDefaultAsync();

                string newInvoiceNumber = "INV001";
                if (lastInvoice != null && !string.IsNullOrEmpty(lastInvoice.InvoiceNumber))
                {
                    var lastNumber = int.Parse(lastInvoice.InvoiceNumber.Substring(3));
                    newInvoiceNumber = $"INV{(lastNumber + 1):D3}";
                }

                // Set auto-generated fields
                invoice.InvoiceNumber = newInvoiceNumber;
                invoice.CreatedDate = DateTime.Now;
                invoice.CreatedBy = User.Identity.Name;
                invoice.Year = invoice.IssueDate.Year;

                // Remove these fields from model validation
                ModelState.Remove("InvoiceNumber");
                ModelState.Remove("CreatedBy");

                if (!ModelState.IsValid)
                {
                    // Log validation errors for debugging
                    foreach (var error in ModelState)
                    {
                        foreach (var modelError in error.Value.Errors)
                        {
                            _logger.LogWarning($"Validation error for {error.Key}: {modelError.ErrorMessage}");
                        }
                    }
                    return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
                }

                // Handle file upload
                if (invoiceFile != null && invoiceFile.Length > 0)
                {
                    var filePath = await SaveFile(invoiceFile, "invoices");
                    invoice.FilePath = filePath;
                }

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} created successfully!";
                return RedirectToAction(nameof(InvoiceRecord));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while saving invoice");
                ModelState.AddModelError("", "An error occurred while saving the invoice. Please try again.");
                return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating invoice");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
            }
        }

        // GET: Invoice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            if (!invoice.CanEdit)
            {
                TempData["Error"] = "This invoice cannot be edited as it's no longer in draft status.";
                return RedirectToAction(nameof(InvoiceRecord));
            }

            return View("~/Views/Admins/Invoice/Edit.cshtml", invoice);
        }

        // POST: Invoice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Invoice invoice, IFormFile invoiceFile)
        {
            if (id != invoice.InvoiceID)
            {
                return NotFound();
            }

            // Remove fields that shouldn't be validated during edit
            ModelState.Remove("InvoiceNumber");
            ModelState.Remove("CreatedBy");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingInvoice = await _context.Invoices.FindAsync(id);
                    if (existingInvoice == null)
                    {
                        return NotFound();
                    }

                    // Update properties (don't change InvoiceNumber, CreatedBy, or CreatedDate)
                    existingInvoice.ClientName = invoice.ClientName;
                    existingInvoice.ClientCompany = invoice.ClientCompany;
                    existingInvoice.ClientEmail = invoice.ClientEmail;
                    existingInvoice.IssueDate = invoice.IssueDate;
                    existingInvoice.DueDate = invoice.DueDate;
                    existingInvoice.TotalAmount = invoice.TotalAmount;
                    existingInvoice.Currency = invoice.Currency;
                    existingInvoice.Remark = invoice.Remark;
                    existingInvoice.Year = invoice.Year;

                    // Handle file upload
                    if (invoiceFile != null && invoiceFile.Length > 0)
                    {
                        // Delete old file if exists
                        if (!string.IsNullOrEmpty(existingInvoice.FilePath))
                        {
                            var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingInvoice.FilePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        var filePath = await SaveFile(invoiceFile, "invoices");
                        existingInvoice.FilePath = filePath;
                    }

                    _context.Update(existingInvoice);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Invoice updated successfully!";
                    return RedirectToAction(nameof(InvoiceRecord));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.InvoiceID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while updating invoice");
                    ModelState.AddModelError("", "An error occurred while updating the invoice. Please try again.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while updating invoice");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            return View("~/Views/Admins/Invoice/Edit.cshtml", invoice);
        }

        // GET: Invoice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(m => m.InvoiceID == id);

            if (invoice == null)
            {
                return NotFound();
            }

            if (!invoice.CanDelete)
            {
                TempData["Error"] = "This invoice cannot be deleted as it's no longer in draft status.";
                return RedirectToAction(nameof(InvoiceRecord));
            }

            return View("~/Views/Admins/Invoice/Delete.cshtml", invoice);
        }

        // POST: Invoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice != null)
                {
                    // Check if invoice can be deleted
                    if (!invoice.CanDelete)
                    {
                        TempData["Error"] = "This invoice cannot be deleted as it's no longer in draft status.";
                        return RedirectToAction(nameof(InvoiceRecord));
                    }

                    // Delete associated file if exists
                    if (!string.IsNullOrEmpty(invoice.FilePath))
                    {
                        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, invoice.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.IO.File.Delete(filePath);
                                _logger.LogInformation($"Deleted file: {filePath}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Could not delete file: {filePath}");
                                // Continue with invoice deletion even if file deletion fails
                            }
                        }
                    }

                    _context.Invoices.Remove(invoice);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Invoice {invoice.InvoiceNumber} deleted successfully!";
                    _logger.LogInformation($"Invoice {invoice.InvoiceNumber} deleted by {User.Identity.Name}");
                }
                else
                {
                    TempData["Error"] = "Invoice not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting invoice with ID: {id}");
                TempData["Error"] = "An error occurred while deleting the invoice. Please try again.";
            }

            return RedirectToAction(nameof(InvoiceRecord));
        }

        // POST: Invoice/UpdateStatus/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    return NotFound();
                }

                invoice.Status = status;

                // Update overdue status automatically
                if (status == "Sent" && invoice.DueDate < DateTime.Now)
                {
                    invoice.Status = "Overdue";
                }

                _context.Update(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Invoice status updated to {invoice.Status}!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice status");
                TempData["Error"] = "An error occurred while updating the invoice status.";
            }

            return RedirectToAction(nameof(InvoiceRecord));
        }

        // GET: Invoice/GenerateReport
        public async Task<IActionResult> GenerateReport(int? year, string? status)
        {
            var query = _context.Invoices.AsQueryable();

            if (year.HasValue)
            {
                query = query.Where(i => i.Year == year.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            var reportData = new
            {
                TotalInvoices = invoices.Count,
                TotalAmount = invoices.Sum(i => i.TotalAmount),
                PaidAmount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                PendingAmount = invoices.Where(i => i.Status == "Sent").Sum(i => i.TotalAmount),
                OverdueAmount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount),
                Invoices = invoices
            };

            ViewBag.ReportData = reportData;
            ViewBag.SelectedYear = year;
            ViewBag.SelectedStatus = status;

            return View("~/Views/Admins/Invoice/Report.cshtml", invoices);
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceID == id);
        }

        private async Task<string> SaveFile(IFormFile file, string folderName)
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", folderName);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{folderName}/{uniqueFileName}";
        }
    }
}