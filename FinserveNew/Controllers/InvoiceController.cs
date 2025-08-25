using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using FinserveNew.Models;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;
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
        private readonly IConfiguration _config;

        public InvoiceController(AppDbContext context, IWebHostEnvironment webHostEnvironment,
                               UserManager<ApplicationUser> userManager, ILogger<InvoiceController> logger,
                               IConfiguration config)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
            _config = config;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadInvoicePdf(int id, string pdfBase64)
        {
            try
            {
                if (id <= 0 || string.IsNullOrWhiteSpace(pdfBase64))
                    return Json(new { success = false, error = "Invalid parameters" });

                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == id && !i.IsDeleted);
                if (invoice == null)
                    return Json(new { success = false, error = "Invoice not found" });

                // Strip data URI prefix and ensure PDF header begins with %PDF
                var commaIdx = pdfBase64.IndexOf(",");
                var base64 = commaIdx >= 0 ? pdfBase64.Substring(commaIdx + 1) : pdfBase64;

                byte[] bytes;
                try { bytes = Convert.FromBase64String(base64); }
                catch { return Json(new { success = false, error = "Invalid PDF data" }); }

                // Basic validation: PDF header
                if (bytes.Length < 4 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46)
                {
                    // Try to repair by re-encoding via jsPDF output artifacts (fallback)
                    // If still invalid, return error to avoid sending unreadable file
                    return Json(new { success = false, error = "Uploaded content is not a valid PDF" });
                }

                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "invoices");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{invoice.InvoiceNumber}.pdf";
                var fullPath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

                invoice.FilePath = $"/uploads/invoices/{fileName}";
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                return Json(new { success = true, filePath = invoice.FilePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload invoice PDF");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Simple Invoice Dashboard
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Dashboard()
        {
            var invoices = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .Where(i => !i.IsDeleted)
                .ToListAsync();

            ViewBag.TotalInvoices = invoices.Count;
            ViewBag.Pending = invoices.Count(i => i.Status == "Pending");
            ViewBag.Sent = invoices.Count(i => i.Status == "Sent");
            ViewBag.Paid = invoices.Count(i => i.Status == "Paid");
            ViewBag.Overdue = invoices.Count(i => i.Status == "Overdue");

            var thisMonth = DateTime.Now.Month;
            var thisYear = DateTime.Now.Year;
            ViewBag.ThisMonthTotal = invoices
                .Where(i => i.IssueDate.Month == thisMonth && i.IssueDate.Year == thisYear)
                .Sum(i => i.TotalAmount);
            ViewBag.Outstanding = invoices
                .Where(i => i.Status == "Pending" || i.Status == "Sent" || i.Status == "Overdue")
                .Sum(i => i.TotalAmount);

            var recent = invoices
                .OrderByDescending(i => i.CreatedDate)
                .Take(5)
                .ToList();
            ViewBag.RecentInvoices = recent;

            return View("~/Views/Admins/Invoice/Dashboard.cshtml");
        }

        // GET: Invoice/InvoiceRecord (Admin view - all non-deleted invoices)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> InvoiceRecord()
        {
            // Include InvoiceItems when loading invoices
            var invoices = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            // Update overdue status for invoices that are sent and past due date
            foreach (var invoice in invoices.Where(i => i.Status == "Sent" && i.DueDate < DateTime.Now))
            {
                invoice.Status = "Overdue";
            }

            // Save any status updates
            if (invoices.Any(i => i.Status == "Overdue"))
            {
                await _context.SaveChangesAsync();
            }

            return View("~/Views/Admins/Invoice/InvoiceRecord.cshtml", invoices);
        }

        // Keep existing Index method for backward compatibility
        public async Task<IActionResult> Index()
        {
            return await InvoiceRecord();
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems) // Include invoice items for details view
                .Where(i => i.InvoiceID == id && !i.IsDeleted)
                .FirstOrDefaultAsync();

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
                Status = "Pending" // Start with Pending status
            };

            // Add one empty invoice item for the form
            invoice.InvoiceItems.Add(new InvoiceItem());

            return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
        }

        // POST: Invoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice, IFormFile invoiceFile)
        {
            try
            {
                // Get current user
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    ModelState.AddModelError("", "Unable to identify current user.");
                    return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
                }

                // Auto-generate InvoiceNumber based on all invoices (including soft-deleted ones)
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
                invoice.Status = "Pending"; // Always start with Pending
                invoice.IsDeleted = false;

                // Process invoice items - remove empty items
                if (invoice.InvoiceItems != null)
                {
                    invoice.InvoiceItems = invoice.InvoiceItems
                        .Where(item => !string.IsNullOrWhiteSpace(item.Description) &&
                                     item.Quantity > 0 && item.UnitPrice > 0)
                        .ToList();

                    // Calculate line totals and set invoice ID
                    foreach (var item in invoice.InvoiceItems)
                    {
                        item.CalculateLineTotal();
                        item.CreatedDate = DateTime.Now;
                        item.InvoiceID = 0; // Will be set by EF Core
                    }

                    // Calculate total amount from items
                    invoice.CalculateTotalFromItems();
                }

                // Remove these fields from model validation - they are auto-generated
                ModelState.Remove("InvoiceNumber");
                ModelState.Remove("CreatedBy");
                ModelState.Remove("FilePath");
                ModelState.Remove("invoiceFile");

                // Additional validation fixes
                if (invoice.IssueDate == default(DateTime))
                {
                    invoice.IssueDate = DateTime.Now;
                }

                if (invoice.DueDate == default(DateTime))
                {
                    invoice.DueDate = DateTime.Now.AddDays(30);
                }

                // Validate that we have at least one invoice item
                if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any())
                {
                    ModelState.AddModelError("InvoiceItems", "At least one invoice item is required.");
                }

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

                    // Add empty item if no items exist for form re-display
                    if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any())
                    {
                        invoice.InvoiceItems = new List<InvoiceItem> { new InvoiceItem() };
                    }

                    return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
                }

                // Handle file upload - this is optional
                if (invoiceFile != null && invoiceFile.Length > 0)
                {
                    var filePath = await SaveFile(invoiceFile, "invoices");
                    invoice.FilePath = filePath;
                }

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} created successfully with Pending status!";
                return RedirectToAction(nameof(InvoiceRecord));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while saving invoice");
                ModelState.AddModelError("", "An error occurred while saving the invoice. Please try again.");

                // Ensure we have items for form re-display
                if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any())
                {
                    invoice.InvoiceItems = new List<InvoiceItem> { new InvoiceItem() };
                }

                return View("~/Views/Admins/Invoice/Create.cshtml", invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating invoice");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");

                // Ensure we have items for form re-display
                if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any())
                {
                    invoice.InvoiceItems = new List<InvoiceItem> { new InvoiceItem() };
                }

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

            // IMPORTANT: Include InvoiceItems when retrieving the invoice
            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .Where(i => i.InvoiceID == id && !i.IsDeleted)
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                return NotFound();
            }

            if (!invoice.CanEdit)
            {
                TempData["Error"] = "This invoice cannot be edited. Only pending invoices can be modified.";
                return RedirectToAction(nameof(InvoiceRecord));
            }

            // Add an empty item if no items exist (for adding new items in edit mode)
            if (!invoice.InvoiceItems.Any())
            {
                invoice.InvoiceItems.Add(new InvoiceItem());
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
            ModelState.Remove("FilePath");
            ModelState.Remove("InvoiceFile");

            // Validate that we have at least one invoice item
            if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any(i => !string.IsNullOrWhiteSpace(i.Description)))
            {
                ModelState.AddModelError("InvoiceItems", "At least one invoice item is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get existing invoice with items from database
                    var existingInvoice = await _context.Invoices
                        .Include(i => i.InvoiceItems)
                        .Where(i => i.InvoiceID == id && !i.IsDeleted)
                        .FirstOrDefaultAsync();

                    if (existingInvoice == null)
                    {
                        return NotFound();
                    }

                    if (!existingInvoice.CanEdit)
                    {
                        TempData["Error"] = "This invoice cannot be edited. Only pending invoices can be modified.";
                        return RedirectToAction(nameof(InvoiceRecord));
                    }

                    // Update invoice properties (don't change InvoiceNumber, CreatedBy, or CreatedDate)
                    existingInvoice.ClientName = invoice.ClientName;
                    existingInvoice.ClientCompany = invoice.ClientCompany;
                    existingInvoice.ClientEmail = invoice.ClientEmail;
                    existingInvoice.IssueDate = invoice.IssueDate;
                    existingInvoice.DueDate = invoice.DueDate;
                    existingInvoice.Currency = invoice.Currency;
                    existingInvoice.Remark = invoice.Remark;
                    existingInvoice.Year = invoice.IssueDate.Year;

                    // Remove existing invoice items
                    _context.InvoiceItems.RemoveRange(existingInvoice.InvoiceItems);

                    // Add updated invoice items
                    if (invoice.InvoiceItems != null)
                    {
                        var validItems = invoice.InvoiceItems
                            .Where(item => !string.IsNullOrWhiteSpace(item.Description) &&
                                         item.Quantity > 0 && item.UnitPrice > 0)
                            .ToList();

                        foreach (var item in validItems)
                        {
                            item.InvoiceID = existingInvoice.InvoiceID;
                            item.CalculateLineTotal();
                            item.CreatedDate = DateTime.Now;
                            item.InvoiceItemID = 0; // Reset ID for new items
                            existingInvoice.InvoiceItems.Add(item);
                        }

                        // Calculate total from items
                        existingInvoice.CalculateTotalFromItems();
                    }

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

            // If we reach here, there was an error - ensure we have items for form re-display
            if (invoice.InvoiceItems == null || !invoice.InvoiceItems.Any())
            {
                invoice.InvoiceItems = new List<InvoiceItem> { new InvoiceItem() };
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
                .Include(i => i.InvoiceItems) // Include items for delete confirmation view
                .Where(i => i.InvoiceID == id && !i.IsDeleted)
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                return NotFound();
            }

            if (!invoice.CanDelete)
            {
                TempData["Error"] = "This invoice cannot be deleted. Only pending invoices can be removed.";
                return RedirectToAction(nameof(InvoiceRecord));
            }

            return View("~/Views/Admins/Invoice/Delete.cshtml", invoice);
        }

        // POST: Invoice/Delete/5 - Soft Delete Implementation
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.InvoiceItems) // Include items for deletion
                    .Where(i => i.InvoiceID == id && !i.IsDeleted)
                    .FirstOrDefaultAsync();

                if (invoice != null)
                {
                    // Check if invoice can be deleted
                    if (!invoice.CanDelete)
                    {
                        TempData["Error"] = "This invoice cannot be deleted. Only pending invoices can be removed.";
                        return RedirectToAction(nameof(InvoiceRecord));
                    }

                    // Soft delete - mark as deleted but don't remove from database
                    invoice.IsDeleted = true;
                    invoice.DeletedDate = DateTime.Now;
                    invoice.DeletedBy = User.Identity.Name;

                    // Note: Invoice items will remain in database with the invoice
                    // They're linked by InvoiceID foreign key

                    _context.Update(invoice);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Invoice {invoice.InvoiceNumber} has been removed from the list!";
                    _logger.LogInformation($"Invoice {invoice.InvoiceNumber} soft deleted by {User.Identity.Name}");
                }
                else
                {
                    TempData["Error"] = "Invoice not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error soft deleting invoice with ID: {id}");
                TempData["Error"] = "An error occurred while removing the invoice. Please try again.";
            }

            return RedirectToAction(nameof(InvoiceRecord));
        }

        // POST: Invoice/SendToClient/5 - Change status from Pending to Sent
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendToClient(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Where(i => i.InvoiceID == id && !i.IsDeleted)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                {
                    return NotFound();
                }

                if (!invoice.CanSend)
                {
                    TempData["Error"] = "This invoice cannot be sent. Only pending invoices can be sent to clients.";
                    return RedirectToAction(nameof(InvoiceRecord));
                }

                // Ensure PDF exists before sending
                var hasPdf = !string.IsNullOrEmpty(invoice.FilePath) && System.IO.File.Exists(Path.Combine(_webHostEnvironment.WebRootPath, invoice.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                if (!hasPdf)
                {
                    TempData["Error"] = "Invoice PDF not found. Please open 'Preview' to generate the PDF first, then send to client.";
                    return RedirectToAction(nameof(InvoiceRecord));
                }

                invoice.Status = "Sent";
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                // Send email with PDF attachment to client
                try
                {
                    await SendInvoiceEmailAsync(invoice);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Failed to send invoice email, but status updated");
                }

                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} has been sent to client!";
                _logger.LogInformation($"Invoice {invoice.InvoiceNumber} sent to client by {User.Identity.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invoice to client");
                TempData["Error"] = "An error occurred while sending the invoice to client.";
            }

            return RedirectToAction(nameof(InvoiceRecord));
        }

        private async Task SendInvoiceEmailAsync(Invoice invoice)
        {
            if (string.IsNullOrWhiteSpace(invoice.ClientEmail)) return;

            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
            var smtpUser = _config["Smtp:User"];
            var smtpPass = _config["Smtp:Pass"];
            var fromEmail = _config["Smtp:From"] ?? smtpUser;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Finserve Invoices", fromEmail));
            message.To.Add(MailboxAddress.Parse(invoice.ClientEmail));
            message.Subject = $"Invoice {invoice.InvoiceNumber} from Finserve";

            var builder = new BodyBuilder();
            builder.HtmlBody = $@"
                <p>Dear {invoice.ClientName},</p>
                <p>Please find attached your invoice <strong>{invoice.InvoiceNumber}</strong> dated {invoice.IssueDate:dd MMM yyyy} with total <strong>{invoice.Currency} {invoice.TotalAmount:F2}</strong>.</p>
                <p>Due Date: {invoice.DueDate:dd MMM yyyy}</p>
                <p>Thank you.</p>
            ";

            // Attach PDF if available
            if (!string.IsNullOrEmpty(invoice.FilePath))
            {
                var relative = invoice.FilePath.TrimStart('/');
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                    var mime = new MimePart("application", "pdf")
                    {
                        Content = new MimeContent(new MemoryStream(bytes)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(fullPath)
                    };
                    builder.Attachments.Add(mime);
                }
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private async Task GenerateAndStoreInvoicePdfAsync(Invoice invoice)
        {
            try
            {
                // Very minimal PDF generated server-side to ensure an attachment exists
                // If a richer client-side PDF is later uploaded, it will overwrite this file
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "invoices");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{invoice.InvoiceNumber}.pdf";
                var fullPath = Path.Combine(uploadsFolder, fileName);

                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream))
                {
                    // Simple PDF header using %PDF minimal (placeholder). In practice, you should use a PDF lib.
                    // Here we store a text-like placeholder if jsPDF upload hasn't happened yet.
                    await writer.WriteAsync($"Invoice {invoice.InvoiceNumber}\nClient: {invoice.ClientName}\nDate: {invoice.IssueDate:dd MMM yyyy}\nTotal: {invoice.Currency} {invoice.TotalAmount:F2}");
                    await writer.FlushAsync();
                }

                await System.IO.File.WriteAllBytesAsync(fullPath, stream.ToArray());
                invoice.FilePath = $"/uploads/invoices/{fileName}";
                _context.Update(invoice);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate minimal invoice PDF");
            }
        }

        // POST: Invoice/MarkAsPaid/5 - Change status to Paid
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Where(i => i.InvoiceID == id && !i.IsDeleted)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                {
                    return NotFound();
                }

                if (!invoice.CanMarkPaid)
                {
                    TempData["Error"] = "This invoice cannot be marked as paid. Only sent or overdue invoices can be marked as paid.";
                    return RedirectToAction(nameof(InvoiceRecord));
                }

                invoice.Status = "Paid";
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} has been marked as paid!";
                _logger.LogInformation($"Invoice {invoice.InvoiceNumber} marked as paid by {User.Identity.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking invoice as paid");
                TempData["Error"] = "An error occurred while marking the invoice as paid.";
            }

            return RedirectToAction(nameof(InvoiceRecord));
        }

        // POST: Invoice/UpdateStatus/5 - Generic status update (kept for compatibility)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Where(i => i.InvoiceID == id && !i.IsDeleted)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                {
                    return NotFound();
                }

                // Validate status transitions
                bool canUpdate = status switch
                {
                    "Sent" => invoice.Status == "Pending",
                    "Paid" => invoice.Status == "Sent" || invoice.Status == "Overdue",
                    "Cancelled" => invoice.Status == "Pending" || invoice.Status == "Sent",
                    _ => false
                };

                if (!canUpdate)
                {
                    TempData["Error"] = $"Cannot change status from {invoice.Status} to {status}.";
                    return RedirectToAction(nameof(InvoiceRecord));
                }

                invoice.Status = status;

                // Auto-update overdue status if needed
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
        // GET: Invoice/GenerateReport
        public async Task<IActionResult> GenerateReport(string period = "yearly", int? year = null,
            int? month = null, string? status = null, string chartType = "pie")
        {
            try
            {
                // Set default year if not provided
                if (!year.HasValue)
                {
                    year = DateTime.Now.Year;
                }

                var query = _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Where(i => !i.IsDeleted);

                // Apply filters based on period
                switch (period.ToLower())
                {
                    case "monthly":
                        if (month.HasValue)
                        {
                            query = query.Where(i => i.IssueDate.Year == year.Value && i.IssueDate.Month == month.Value);
                        }
                        else
                        {
                            // Default to current month if month not specified
                            var currentMonth = DateTime.Now.Month;
                            query = query.Where(i => i.IssueDate.Year == year.Value && i.IssueDate.Month == currentMonth);
                            month = currentMonth;
                        }
                        break;
                    case "yearly":
                        query = query.Where(i => i.IssueDate.Year == year.Value);
                        break;
                }

                // Apply status filter if specified
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(i => i.Status.ToLower() == status.ToLower());
                }

                var invoices = await query
                    .OrderByDescending(i => i.CreatedDate)
                    .ToListAsync();

                // Get available years for filter dropdown - MOVED HERE BEFORE USAGE
                var availableYears = await _context.Invoices
                    .Where(i => !i.IsDeleted)
                    .Select(i => i.IssueDate.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();

                // Calculate status distribution for pie chart
                var statusDistribution = new Dictionary<string, object>
                {
                    ["Pending"] = new
                    {
                        count = invoices.Count(i => i.Status == "Pending"),
                        amount = invoices.Where(i => i.Status == "Pending").Sum(i => i.TotalAmount)
                    },
                    ["Sent"] = new
                    {
                        count = invoices.Count(i => i.Status == "Sent"),
                        amount = invoices.Where(i => i.Status == "Sent").Sum(i => i.TotalAmount)
                    },
                    ["Paid"] = new
                    {
                        count = invoices.Count(i => i.Status == "Paid"),
                        amount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount)
                    },
                    ["Overdue"] = new
                    {
                        count = invoices.Count(i => i.Status == "Overdue"),
                        amount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount)
                    }
                };

                // Generate time series data for line chart
                List<object> timeSeriesData = new List<object>();

                if (period == "yearly")
                {
                    // Monthly data for the selected year
                    for (int m = 1; m <= 12; m++)
                    {
                        var monthlyInvoices = invoices.Where(i => i.IssueDate.Month == m).ToList();
                        timeSeriesData.Add(new
                        {
                            period = new DateTime(year.Value, m, 1).ToString("MMM yyyy"),
                            month = m,
                            pending = monthlyInvoices.Count(i => i.Status == "Pending"),
                            sent = monthlyInvoices.Count(i => i.Status == "Sent"),
                            paid = monthlyInvoices.Count(i => i.Status == "Paid"),
                            overdue = monthlyInvoices.Count(i => i.Status == "Overdue"),
                            totalAmount = monthlyInvoices.Sum(i => i.TotalAmount)
                        });
                    }
                }
                else if (period == "monthly")
                {
                    // Daily data for the selected month
                    var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var dailyInvoices = invoices.Where(i => i.IssueDate.Day == d).ToList();
                        timeSeriesData.Add(new
                        {
                            period = $"{d:D2}",
                            day = d,
                            pending = dailyInvoices.Count(i => i.Status == "Pending"),
                            sent = dailyInvoices.Count(i => i.Status == "Sent"),
                            paid = dailyInvoices.Count(i => i.Status == "Paid"),
                            overdue = dailyInvoices.Count(i => i.Status == "Overdue"),
                            totalAmount = dailyInvoices.Sum(i => i.TotalAmount)
                        });
                    }
                }

                // Create status counts for pie chart
                var statusCounts = new[]
                {
    new { status = "Pending", count = invoices.Count(i => i.Status == "Pending") },
    new { status = "Sent", count = invoices.Count(i => i.Status == "Sent") },
    new { status = "Paid", count = invoices.Count(i => i.Status == "Paid") },
    new { status = "Overdue", count = invoices.Count(i => i.Status == "Overdue") }
}.Where(x => x.count > 0).ToArray();

                // Create trend data for line chart
                List<object> trendData = new List<object>();

                if (period == "yearly")
                {
                    // Monthly data for the selected year
                    for (int m = 1; m <= 12; m++)
                    {
                        var monthlyInvoices = invoices.Where(i => i.IssueDate.Month == m).ToList();
                        trendData.Add(new
                        {
                            periodName = new DateTime(year.Value, m, 1).ToString("MMM yyyy"),
                            month = m,
                            count = monthlyInvoices.Count,
                            amount = monthlyInvoices.Sum(i => i.TotalAmount)
                        });
                    }
                }
                else if (period == "monthly")
                {
                    // Daily data for the selected month
                    var daysInMonth = DateTime.DaysInMonth(year.Value, month ?? DateTime.Now.Month);
                    var selectedMonth = month ?? DateTime.Now.Month;
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var dailyInvoices = invoices.Where(i => i.IssueDate.Day == d).ToList();
                        trendData.Add(new
                        {
                            periodName = $"Day {d}",
                            day = d,
                            count = dailyInvoices.Count,
                            amount = dailyInvoices.Sum(i => i.TotalAmount)
                        });
                    }
                }

                var reportData = new
                {
                    Period = period,
                    Year = year.Value,
                    Month = month,
                    Status = status,
                    ChartType = chartType,
                    TotalInvoices = invoices.Count,
                    TotalAmount = invoices.Sum(i => i.TotalAmount),
                    PaidAmount = invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                    PendingAmount = invoices.Where(i => i.Status == "Pending").Sum(i => i.TotalAmount),
                    SentAmount = invoices.Where(i => i.Status == "Sent").Sum(i => i.TotalAmount),
                    OverdueAmount = invoices.Where(i => i.Status == "Overdue").Sum(i => i.TotalAmount),
                    StatusDistribution = statusDistribution,
                    TimeSeriesData = timeSeriesData,
                    StatusCounts = statusCounts,
                    TrendData = trendData,
                    AvailableYears = availableYears,
                    Invoices = invoices
                };

                ViewBag.ReportData = reportData;
                ViewBag.SelectedPeriod = period;
                ViewBag.SelectedYear = year;
                ViewBag.SelectedMonth = month;
                ViewBag.SelectedStatus = status;
                ViewBag.SelectedChartType = chartType;

                // REMOVED DUPLICATE availableYears QUERY - Now using the one defined above
                ViewBag.AvailableYears = availableYears;

                return View("~/Views/Admins/Invoice/Report.cshtml", invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice report");
                TempData["Error"] = "An error occurred while generating the report.";
                return RedirectToAction(nameof(InvoiceRecord));
            }
        }

        // API endpoint to get chart data for AJAX requests
        [HttpGet]
        public async Task<IActionResult> GetChartData(string period = "yearly", int? year = null,
            int? month = null, string? status = null)
        {
            try
            {
                if (!year.HasValue) year = DateTime.Now.Year;

                var query = _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Where(i => !i.IsDeleted);

                // Apply period filter
                switch (period.ToLower())
                {
                    case "monthly":
                        if (month.HasValue)
                        {
                            query = query.Where(i => i.IssueDate.Year == year.Value && i.IssueDate.Month == month.Value);
                        }
                        else
                        {
                            var currentMonth = DateTime.Now.Month;
                            query = query.Where(i => i.IssueDate.Year == year.Value && i.IssueDate.Month == currentMonth);
                            month = currentMonth;
                        }
                        break;
                    case "yearly":
                        query = query.Where(i => i.IssueDate.Year == year.Value);
                        break;
                }

                // Apply status filter
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(i => i.Status.ToLower() == status.ToLower());
                }

                var invoices = await query.ToListAsync();

                // Pie chart data
                var pieData = new[]
                {
            new { label = "Pending", value = invoices.Count(i => i.Status == "Pending"), color = "#fbbf24" },
            new { label = "Sent", value = invoices.Count(i => i.Status == "Sent"), color = "#60a5fa" },
            new { label = "Paid", value = invoices.Count(i => i.Status == "Paid"), color = "#34d399" },
            new { label = "Overdue", value = invoices.Count(i => i.Status == "Overdue"), color = "#f87171" }
        }.Where(x => x.value > 0).ToArray();

                // Line chart data
                List<object> lineData = new List<object>();

                if (period == "yearly")
                {
                    for (int m = 1; m <= 12; m++)
                    {
                        var monthlyInvoices = invoices.Where(i => i.IssueDate.Month == m).ToList();
                        lineData.Add(new
                        {
                            period = new DateTime(year.Value, m, 1).ToString("MMM"),
                            pending = monthlyInvoices.Count(i => i.Status == "Pending"),
                            sent = monthlyInvoices.Count(i => i.Status == "Sent"),
                            paid = monthlyInvoices.Count(i => i.Status == "Paid"),
                            overdue = monthlyInvoices.Count(i => i.Status == "Overdue")
                        });
                    }
                }
                else
                {
                    var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var dailyInvoices = invoices.Where(i => i.IssueDate.Day == d).ToList();
                        lineData.Add(new
                        {
                            period = d.ToString(),
                            pending = dailyInvoices.Count(i => i.Status == "Pending"),
                            sent = dailyInvoices.Count(i => i.Status == "Sent"),
                            paid = dailyInvoices.Count(i => i.Status == "Paid"),
                            overdue = dailyInvoices.Count(i => i.Status == "Overdue")
                        });
                    }
                }

                return Json(new { pieData, lineData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chart data");
                return Json(new { error = "Failed to load chart data" });
            }
        }
        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceID == id && !e.IsDeleted);
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