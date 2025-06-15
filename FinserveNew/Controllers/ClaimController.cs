using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;

namespace FinserveNew.Controllers
{
    // [Authorize]
    public class ClaimController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ClaimController> _logger;

        public ClaimController(AppDbContext context, IWebHostEnvironment environment, ILogger<ClaimController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        // GET: Claim
        public async Task<IActionResult> Index()
        {
            var claims = await _context.Claims
                .Include(c => c.Employee)
                .Include(c => c.Approval)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View(claims);
        }

        // GET: Claim/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.Claims
                .Include(c => c.Employee)
                .Include(c => c.Approval)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }

        // GET: Claim/Create
        public async Task<IActionResult> Create()
        {
            // Check if any employees exist
            var employeeExists = await _context.Employees.AnyAsync();
            if (!employeeExists)
            {
                TempData["Error"] = "No employees found. Please create an employee first.";
                return RedirectToAction("Index", "Employee");
            }

            // Populate ViewBag for dropdowns
            ViewBag.Employees = await _context.Employees
                .Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName })
                .ToListAsync();

            ViewBag.ClaimTypes = new List<string>
            {
                "Medical",
                "Travel",
                "Equipment",
                "Training",
                "Other"
            };

            return View();
        }

        // POST: Claim/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, IFormFile? supportingDocument)
        {
            try
            {
                _logger.LogInformation($"[DEBUG] Attempting to create claim for EmployeeId: {claim.EmployeeId}");

                // 1. Validate Employee exists
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeId);

                if (employee == null)
                {
                    ModelState.AddModelError("EmployeeId", $"Employee with ID '{claim.EmployeeId}' does not exist.");
                    await PopulateViewBagData();
                    return View(claim);
                }

                // 2. Handle file upload
                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");

                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Generate unique filename
                    var uniqueFileName = $"{Guid.NewGuid()}_{supportingDocument.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save file
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await supportingDocument.CopyToAsync(fileStream);
                    }

                    claim.SupportingDocumentName = supportingDocument.FileName;
                    claim.SupportingDocumentPath = $"/uploads/claims/{uniqueFileName}";
                }

                // 3. Set claim properties
                claim.CreatedDate = DateTime.Now;
                claim.SubmissionDate = DateTime.Now;
                claim.Status = "Pending";
                claim.TotalAmount = claim.ClaimAmount; // Set total amount same as claim amount initially

                // Don't set ApprovalID or ApprovalDate yet (will be set when approved)
                claim.ApprovalID = null;
                claim.ApprovalDate = null;

                _logger.LogInformation($"[DEBUG] Claim details - EmployeeId: {claim.EmployeeId}, CreatedDate: {claim.CreatedDate}");

                // 4. Add to context and save
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"[DEBUG] Claim created successfully with ID: {claim.Id}");

                TempData["Success"] = "Claim submitted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "[DEBUG] Database error while saving claim");

                // Check for specific foreign key constraint errors
                if (ex.InnerException?.Message.Contains("FK_Claims_Employees_EmployeeId") == true)
                {
                    ModelState.AddModelError("EmployeeId", "The selected employee does not exist in the system.");
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while saving the claim. Please try again.");
                }

                await PopulateViewBagData();
                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEBUG] Unexpected error while creating claim");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                await PopulateViewBagData();
                return View(claim);
            }
        }

        // GET: Claim/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            await PopulateViewBagData();
            return View(claim);
        }

        // POST: Claim/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Claim claim, IFormFile? supportingDocument)
        {
            if (id != claim.Id)
            {
                return NotFound();
            }

            try
            {
                // Validate Employee exists
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeId);

                if (employee == null)
                {
                    ModelState.AddModelError("EmployeeId", $"Employee with ID '{claim.EmployeeId}' does not exist.");
                    await PopulateViewBagData();
                    return View(claim);
                }

                // Handle new file upload
                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}_{supportingDocument.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await supportingDocument.CopyToAsync(fileStream);
                    }

                    claim.SupportingDocumentName = supportingDocument.FileName;
                    claim.SupportingDocumentPath = $"/uploads/claims/{uniqueFileName}";
                }

                _context.Update(claim);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Claim updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClaimExists(claim.Id))
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
                _logger.LogError(ex, "Database error while updating claim");

                if (ex.InnerException?.Message.Contains("FK_Claims_Employees_EmployeeId") == true)
                {
                    ModelState.AddModelError("EmployeeId", "The selected employee does not exist in the system.");
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while updating the claim. Please try again.");
                }

                await PopulateViewBagData();
                return View(claim);
            }
        }

        // GET: Claim/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.Claims
                .Include(c => c.Employee)
                .Include(c => c.Approval)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }

        // POST: Claim/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim != null)
            {
                // Delete associated file if exists
                if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, claim.SupportingDocumentPath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Claim deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // Helper method to populate ViewBag data
        private async Task PopulateViewBagData()
        {
            ViewBag.Employees = await _context.Employees
                .Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName })
                .ToListAsync();

            ViewBag.ClaimTypes = new List<string>
            {
                "Medical",
                "Travel",
                "Equipment",
                "Training",
                "Other"
            };
        }

        private bool ClaimExists(int id)
        {
            return _context.Claims.Any(e => e.Id == id);
        }

        // Additional action to check employees (for debugging)
        public async Task<IActionResult> CheckEmployees()
        {
            var employees = await _context.Employees
                .Select(e => new { e.EmployeeID, e.FirstName, e.LastName })
                .ToListAsync();

            return Json(employees);
        }
    }
}