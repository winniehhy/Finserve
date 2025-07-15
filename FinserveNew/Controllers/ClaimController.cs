using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FinserveNew.Controllers
{
    public class ClaimController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ClaimController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClaimController(AppDbContext context, IWebHostEnvironment environment, ILogger<ClaimController> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _userManager = userManager;
        }

        // ================== HELPER METHOD TO GET EMPLOYEE ID ==================
        private async Task<string> GetCurrentEmployeeId()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            _logger.LogInformation($"Current user: {currentUser.UserName}, Email: {currentUser.Email}");

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Username == currentUser.UserName || e.Email == currentUser.Email);

            if (employee == null)
            {
                _logger.LogWarning($"No employee found for username: {currentUser.UserName} or email: {currentUser.Email}");

                // Log all employees to see what's in the database
                var allEmployees = await _context.Employees.ToListAsync();
                foreach (var emp in allEmployees)
                {
                    _logger.LogInformation($"Employee: {emp.EmployeeID}, Username: {emp.Username}, Email: {emp.Email}");
                }
            }

            return employee?.EmployeeID;
        }

        // ================== EMPLOYEE ACTIONS ==================

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Index()
        {
            // Get current employee ID
            var employeeId = await GetCurrentEmployeeId();

            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Claim/Index.cshtml", new List<Claim>());
            }

            var claims = await _context.Claims
                .Where(c => c.EmployeeID == employeeId)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/Employee/Claim/Index.cshtml", claims);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                return NotFound();
            }

            var claim = await _context.Claims
                .FirstOrDefaultAsync(m => m.Id == id && m.EmployeeID == employeeId);

            if (claim == null)
            {
                return NotFound();
            }

            return View("~/Views/Employee/Claim/Details.cshtml", claim);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create()
        {
            await PopulateViewBagData();
            return View("~/Views/Employee/Claim/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(Claim claim, IFormFile? supportingDocument)
        {
            try
            {
                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateViewBagData();
                    return View("~/Views/Employee/Claim/Create.cshtml", claim);
                }

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

                // Set default claim values with correct employee ID
                claim.EmployeeID = employeeId; // This will be "E001"
                claim.CreatedDate = DateTime.Now;
                claim.SubmissionDate = DateTime.Now;
                claim.Status = "Pending";
                claim.TotalAmount = claim.ClaimAmount;
                claim.ApprovalDate = null;
                claim.ApprovedBy = null;
                claim.ApprovalRemarks = null;

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
                return View("~/Views/Employee/Claim/Create.cshtml", claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating claim");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                await PopulateViewBagData();
                return View("~/Views/Employee/Claim/Create.cshtml", claim);
            }
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var claim = await _context.Claims.FindAsync(id);

            if (claim == null || claim.EmployeeID != employeeId)
                return NotFound();

            // Only allow editing if status is Pending
            if (claim.Status != "Pending")
            {
                TempData["Error"] = "You can only edit claims that are in Pending status.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateViewBagData();
            return View("~/Views/Employee/Claim/Edit.cshtml", claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int id, IFormFile? supportingDocument)
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var claimToUpdate = await _context.Claims.FindAsync(id);

            if (claimToUpdate == null || claimToUpdate.EmployeeID != employeeId)
                return NotFound();

            // Only allow editing if status is Pending
            if (claimToUpdate.Status != "Pending")
            {
                TempData["Error"] = "You can only edit claims that are in Pending status.";
                return RedirectToAction(nameof(Index));
            }

            if (await TryUpdateModelAsync(claimToUpdate, "",
                c => c.ClaimType, c => c.ClaimAmount, c => c.Description))
            {
                try
                {
                    // Handle new document upload
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

                        // Delete old file if exists
                        if (!string.IsNullOrEmpty(claimToUpdate.SupportingDocumentPath))
                        {
                            var oldPath = Path.Combine(_environment.WebRootPath, claimToUpdate.SupportingDocumentPath.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath))
                                System.IO.File.Delete(oldPath);
                        }

                        claimToUpdate.SupportingDocumentName = supportingDocument.FileName;
                        claimToUpdate.SupportingDocumentPath = $"/uploads/claims/{uniqueFileName}";
                    }

                    claimToUpdate.TotalAmount = claimToUpdate.ClaimAmount;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Claim updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while updating claim");
                    ModelState.AddModelError("", "An error occurred while updating the claim.");
                }
            }

            await PopulateViewBagData();
            return View("~/Views/Employee/Claim/Edit.cshtml", claimToUpdate);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var claim = await _context.Claims
                .FirstOrDefaultAsync(m => m.Id == id && m.EmployeeID == employeeId);

            if (claim == null)
                return NotFound();

            // Only allow deletion if status is Pending
            if (claim.Status != "Pending")
            {
                TempData["Error"] = "You can only delete claims that are in Pending status.";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Employee/Claim/Delete.cshtml", claim);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return RedirectToAction(nameof(Index));

            var claim = await _context.Claims.FindAsync(id);

            if (claim != null && claim.EmployeeID == employeeId)
            {
                // Only allow deletion if status is Pending
                if (claim.Status != "Pending")
                {
                    TempData["Error"] = "You can only delete claims that are in Pending status.";
                    return RedirectToAction(nameof(Index));
                }

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

        // ================== HR ACTIONS ==================

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HRIndex()
        {
            var claims = await _context.Claims
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/HRIndex.cshtml", claims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HRDetails(int? id)
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

            return View("~/Views/HR/Claim/ClaimDetails.cshtml", claim);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessClaim(int? id)
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

            // Only allow processing if status is Pending
            if (claim.Status != "Pending")
            {
                TempData["Error"] = "This claim has already been processed.";
                return RedirectToAction(nameof(HRIndex));
            }

            return View("~/Views/HR/Claim/ProcessClaim.cshtml", claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessClaim(int id, string action, string? remarks)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            // Only allow processing if status is Pending
            if (claim.Status != "Pending")
            {
                TempData["Error"] = "This claim has already been processed.";
                return RedirectToAction(nameof(HRIndex));
            }

            try
            {
                // For HR actions, we can still use the current user's ID for ApprovedBy
                var currentUser = await _userManager.GetUserAsync(User);

                // Update claim based on action
                if (action == "approve")
                {
                    claim.Status = "Approved";
                    claim.ApprovalDate = DateTime.Now;
                    claim.ApprovedBy = currentUser.Id; // HR user ID for approval tracking
                    claim.ApprovalRemarks = remarks;
                    TempData["Success"] = "Claim approved successfully!";
                }
                else if (action == "reject")
                {
                    claim.Status = "Rejected";
                    claim.ApprovalDate = DateTime.Now;
                    claim.ApprovedBy = currentUser.Id; // HR user ID for approval tracking
                    claim.ApprovalRemarks = remarks;
                    TempData["Success"] = "Claim rejected successfully!";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessClaim), new { id = id });
                }

                await _context.SaveChangesAsync();

                // TODO: Send email notification to employee
                // await SendEmailNotification(claim);

                return RedirectToAction(nameof(HRIndex));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while processing claim");
                TempData["Error"] = "An error occurred while processing the claim. Please try again.";
                return RedirectToAction(nameof(ProcessClaim), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing claim");
                TempData["Error"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction(nameof(ProcessClaim), new { id = id });
            }
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> PendingClaims()
        {
            var pendingClaims = await _context.Claims
                .Where(c => c.Status == "Pending")
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/PendingClaim.cshtml", pendingClaims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ApprovedClaims()
        {
            var approvedClaims = await _context.Claims
                .Where(c => c.Status == "Approved")
                .OrderByDescending(c => c.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/ApprovedClaim.cshtml", approvedClaims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> RejectedClaims()
        {
            var rejectedClaims = await _context.Claims
                .Where(c => c.Status == "Rejected")
                .OrderByDescending(c => c.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/RejectedClaim.cshtml", rejectedClaims);
        }

        // ================== HELPER METHODS ==================

        private async Task PopulateViewBagData()
        {
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

        private bool ClaimExists(int id)
        {
            return _context.Claims.Any(e => e.Id == id);
        }

        // TODO: Implement email notification
        private async Task SendEmailNotification(Claim claim)
        {
            // Implementation for sending email notification to employee
            // when claim is approved/rejected
        }
    }
}