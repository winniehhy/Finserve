using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;
using FinserveNew.Data;

namespace FinserveNew.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public InvoiceController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Invoice/InvoiceRecord (Admin view)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> InvoiceRecord()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Employee)
                .OrderByDescending(i => i.IssueDate)
                .ToListAsync();

            return View("~/Views/Admins/Invoice/InvoiceRecord.cshtml",invoices);
        }

        // GET: Invoice/Index (Employee view - their own invoices)
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.Identity.Name;
            var invoices = await _context.Invoices
                .Include(i => i.Employee)
                .Where(i => i.EmployeeID == currentUserId)
                .OrderByDescending(i => i.IssueDate)
                .ToListAsync();

            return View(invoices);
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Employee)
                .FirstOrDefaultAsync(m => m.InvoiceID == id);

            if (invoice == null)
            {
                return NotFound();
            }

            // Check if user can view this invoice
            if (!User.IsInRole("Admin") && !User.IsInRole("HR") && invoice.EmployeeID != User.Identity.Name)
            {
                return Forbid();
            }

            return View(invoice);
        }

        // GET: Invoice/Create
        public IActionResult Create()
        {
            var invoice = new Invoice
            {
                IssueDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(30),
                Year = DateTime.Now.Year,
                EmployeeID = User.Identity.Name
            };

            return View(invoice);
        }

        // POST: Invoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice, IFormFile invoiceFile)
        {
            if (ModelState.IsValid)
            {
                // Set default values
                invoice.EmployeeID = User.Identity.Name;
                invoice.Year = invoice.IssueDate.Year;
                invoice.Status = "Pending";

                // Generate invoice number
                var lastInvoice = await _context.Invoices
                    .Where(i => i.Year == invoice.Year)
                    .OrderByDescending(i => i.InvoiceID)
                    .FirstOrDefaultAsync();

                var nextNumber = 1;
                if (lastInvoice != null)
                {
                    var lastNumber = lastInvoice.InvoiceNumber.Split('-').LastOrDefault();
                    if (int.TryParse(lastNumber, out int num))
                    {
                        nextNumber = num + 1;
                    }
                }

                invoice.InvoiceNumber = $"INV-{invoice.Year}-{nextNumber:D4}";

                // Handle file upload
                if (invoiceFile != null && invoiceFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "invoices");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{invoice.InvoiceNumber}_{Path.GetFileName(invoiceFile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await invoiceFile.CopyToAsync(fileStream);
                    }

                    invoice.FilePath = $"/uploads/invoices/{uniqueFileName}";
                }

                _context.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Invoice created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
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

            // Check if user can edit this invoice
            if (!User.IsInRole("Admin") && !User.IsInRole("HR") && invoice.EmployeeID != User.Identity.Name)
            {
                return Forbid();
            }

            if (!invoice.CanEdit)
            {
                TempData["Error"] = "This invoice cannot be edited as it's no longer in pending status.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
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

            if (ModelState.IsValid)
            {
                try
                {
                    var existingInvoice = await _context.Invoices.FindAsync(id);
                    if (existingInvoice == null)
                    {
                        return NotFound();
                    }

                    // Check if user can edit this invoice
                    if (!User.IsInRole("Admin") && !User.IsInRole("HR") && existingInvoice.EmployeeID != User.Identity.Name)
                    {
                        return Forbid();
                    }

                    // Update properties
                    existingInvoice.DueDate = invoice.DueDate;
                    existingInvoice.TotalAmount = invoice.TotalAmount;
                    existingInvoice.Remark = invoice.Remark;
                    existingInvoice.Year = invoice.IssueDate.Year;

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

                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "invoices");
                        Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = $"{existingInvoice.InvoiceNumber}_{Path.GetFileName(invoiceFile.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await invoiceFile.CopyToAsync(fileStream);
                        }

                        existingInvoice.FilePath = $"/uploads/invoices/{uniqueFileName}";
                    }

                    _context.Update(existingInvoice);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Invoice updated successfully!";
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
                return RedirectToAction(nameof(Index));
            }
            return View(invoice);
        }

        // GET: Invoice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Employee)
                .FirstOrDefaultAsync(m => m.InvoiceID == id);

            if (invoice == null)
            {
                return NotFound();
            }

            // Check if user can delete this invoice
            if (!User.IsInRole("Admin") && !User.IsInRole("HR") && invoice.EmployeeID != User.Identity.Name)
            {
                return Forbid();
            }

            if (!invoice.CanDelete)
            {
                TempData["Error"] = "This invoice cannot be deleted as it's no longer in pending status.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        // POST: Invoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                // Check if user can delete this invoice
                if (!User.IsInRole("Admin") && !User.IsInRole("HR") && invoice.EmployeeID != User.Identity.Name)
                {
                    return Forbid();
                }

                // Delete associated file
                if (!string.IsNullOrEmpty(invoice.FilePath))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, invoice.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Invoice deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Invoice/UpdateStatus/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            invoice.Status = status;
            _context.Update(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Invoice status updated to {status}!";
            return RedirectToAction(nameof(InvoiceRecord));
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceID == id);
        }
    }
}