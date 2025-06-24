using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;

namespace FinserveNew.Controllers
{
    // [Authorize] // will uncomment for RBAC later
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

        // ================== GET ALL CLAIMS ==================
        // Show the list of all claims, ordered by most recent
        public async Task<IActionResult> Index()
        {
            var claims = await _context.Claims
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View(claims);
        }

        // ================== VIEW CLAIM DETAILS ==================
        // Show detailed info for a specific claim
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.Claims
                .FirstOrDefaultAsync(m => m.Id == id);

            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }

        // ================== SHOW CLAIM FORM ==================
        // Show the form to create a new claim
        public async Task<IActionResult> Create()
        {
            // Removed employee existence check
            await PopulateViewBagData();
            return View();
        }

        // ================== CREATE CLAIM (POST) ==================
        // Handle submission of a new claim with optional document upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, IFormFile? supportingDocument)
        {
            try
            {
                // Removed employee validation - just use the provided EmployeeID as is

                // Upload document if provided
                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}_{supportingDocument.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await supportingDocument.CopyToAsync(fileStream);
                    }

                    claim.SupportingDocumentName = supportingDocument.FileName;
                    claim.SupportingDocumentPath = $"/uploads/claims/{uniqueFileName}";
                }

                // Set default claim values
                claim.CreatedDate = DateTime.Now;
                claim.SubmissionDate = DateTime.Now;
                claim.Status = "Pending";
                claim.TotalAmount = claim.ClaimAmount;
                claim.ApprovalID = null;
                claim.ApprovalDate = null;

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Claim submitted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while saving claim");
                ModelState.AddModelError("", "An error occurred while saving the claim. Please try again.");
                await PopulateViewBagData();
                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating claim");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                await PopulateViewBagData();
                return View(claim);
            }
        }

        // ================== SHOW EDIT FORM ==================
        // Show the form to edit an existing claim
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var claim = await _context.Claims.FindAsync(id);
            if (claim == null)
                return NotFound();

            await PopulateViewBagData();
            return View(claim);
        }

        // ================== UPDATE CLAIM (POST) ==================
        // Handle claim update with optional new document upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Claim claim, IFormFile? supportingDocument)
        {
            if (id != claim.Id)
                return NotFound();

            try
            {
                // Removed employee validation - just use the provided EmployeeID as is

                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                    Directory.CreateDirectory(uploadsFolder);

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
                    return NotFound();
                else
                    throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while updating claim");
                ModelState.AddModelError("", "An error occurred while updating the claim.");
                await PopulateViewBagData();
                return View(claim);
            }
        }

        // ================== SHOW DELETE CONFIRMATION ==================
        // Show confirmation page before deleting a claim
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var claim = await _context.Claims
                .FirstOrDefaultAsync(m => m.Id == id);

            if (claim == null)
                return NotFound();

            return View(claim);
        }

        // ================== DELETE CLAIM (POST) ==================
        // Delete the claim and its associated document (if exists)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim != null)
            {
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

        // ================== HELPER: POPULATE DROPDOWNS ==================
        // Load claim types for dropdowns (removed employee data)
        private async Task PopulateViewBagData()
        {
            // Removed employee dropdown data
            ViewBag.ClaimTypes = new List<string>
            {
                "Medical",
                "Travel",
                "Equipment",
                "Training",
                "Entertainment",
                "Other"
            };
        }

        // ================== HELPER: CHECK IF CLAIM EXISTS ==================
        private bool ClaimExists(int id)
        {
            return _context.Claims.Any(e => e.Id == id);
        }

        // ================== DEBUG: VIEW EMPLOYEE LIST AS JSON ==================
        // Removed CheckEmployees method since we're not using employees
    }
}