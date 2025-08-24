using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinserveNew.Controllers
{
    public class ClaimController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ClaimController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOcrService _ocrService;
        private readonly IEmailSender _emailSender; // Added email sender

        public ClaimController(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<ClaimController> logger,
            UserManager<ApplicationUser> userManager,
            IOcrService ocrService,
            IEmailSender emailSender) // Added email sender to constructor
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _userManager = userManager;
            _ocrService = ocrService;
            _emailSender = emailSender;
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
                .Where(c => c.EmployeeID == employeeId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/Employee/Claim/Index.cshtml", claims);
        }

        // FIXED: Details method with proper document handling
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
                .Include(c => c.ClaimDetails) // Include claim details
                .FirstOrDefaultAsync(m => m.Id == id && m.EmployeeID == employeeId && !m.IsDeleted);

            if (claim == null)
            {
                return NotFound();
            }

            // Get approver information if claim is processed
            if (!string.IsNullOrEmpty(claim.ApprovedBy))
            {
                _logger.LogInformation($"Claim {claim.Id} has ApprovedBy: '{claim.ApprovedBy}'");
                
                // Check if ApprovedBy contains a full name (new format) or user ID (old format)
                if (claim.ApprovedBy.Contains(" "))
                {
                    // New format: ApprovedBy contains the full name directly
                    ViewBag.ApproverName = claim.ApprovedBy;
                    ViewBag.ApproverEmail = ""; // No email available in new format
                    _logger.LogInformation($"Using new format - Approver Name: {ViewBag.ApproverName}");
                }
                else
                {
                    // Old format: ApprovedBy contains user ID, try to get user details
                    _logger.LogInformation($"Using old format - ApprovedBy is user ID: {claim.ApprovedBy}");
                    var approver = await _userManager.FindByIdAsync(claim.ApprovedBy);
                    if (approver != null)
                    {
                        ViewBag.ApproverName = $"{approver.FirstName} {approver.LastName}";
                        ViewBag.ApproverEmail = approver.Email;
                        _logger.LogInformation($"Found approver user: {ViewBag.ApproverName}");
                    }
                    else
                    {
                        ViewBag.ApproverName = "Unknown Approver";
                        ViewBag.ApproverEmail = "";
                        _logger.LogWarning($"Could not find approver user with ID: {claim.ApprovedBy}");
                    }
                }
            }
            else
            {
                _logger.LogInformation($"Claim {claim.Id} has no ApprovedBy value");
            }

            // FIXED: Initialize documents list properly
            var documents = new List<ClaimDetails>();

            // Get documents from ClaimDetails (preferred method)
            if (claim.ClaimDetails != null && claim.ClaimDetails.Any())
            {
                documents = claim.ClaimDetails.ToList();
                _logger.LogInformation($"Found {documents.Count} documents in ClaimDetails for claim {claim.Id}");
            }

            ViewBag.Documents = documents;
            ViewBag.DocumentCount = documents.Count;

            // FIXED: Better document handling logic
            bool hasDocuments = false;
            string primaryDocumentName = "";
            string primaryDocumentPath = "";
            string primaryDocumentSize = "Unknown";
            string primaryDocumentUploadDate = "Unknown";

            // Check ClaimDetails first (preferred)
            if (documents.Count > 0)
            {
                hasDocuments = true;
                var primaryDoc = documents.First();
                primaryDocumentName = primaryDoc.OriginalFileName ?? "Document";
                primaryDocumentPath = primaryDoc.DocumentPath;

                // Get file size and date
                try
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, primaryDoc.DocumentPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        primaryDocumentSize = FormatFileSize(fileInfo.Length);
                    }
                    else
                    {
                        // Use stored file size if file doesn't exist on disk
                        primaryDocumentSize = FormatFileSize(primaryDoc.FileSize ?? 0);
                    }
                    primaryDocumentUploadDate = primaryDoc.UploadDate.ToString("dd/MM/yyyy HH:mm");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not get file info for {primaryDoc.DocumentPath}: {ex.Message}");
                    primaryDocumentSize = FormatFileSize(primaryDoc.FileSize ?? 0);
                    primaryDocumentUploadDate = primaryDoc.UploadDate.ToString("dd/MM/yyyy HH:mm");
                }

                _logger.LogInformation($"Primary document set: {primaryDocumentName}, Size: {primaryDocumentSize}");
            }
            // Fallback to legacy document field
            else if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
            {
                hasDocuments = true;
                primaryDocumentName = claim.SupportingDocumentName ?? Path.GetFileName(claim.SupportingDocumentPath);
                primaryDocumentPath = claim.SupportingDocumentPath;

                try
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, claim.SupportingDocumentPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        primaryDocumentSize = FormatFileSize(fileInfo.Length);
                        primaryDocumentUploadDate = fileInfo.CreationTime.ToString("dd/MM/yyyy HH:mm");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not get file info for {claim.SupportingDocumentPath}: {ex.Message}");
                }

                _logger.LogInformation($"Using legacy document: {primaryDocumentName}");
            }
            else
            {
                _logger.LogWarning($"No documents found for claim {claim.Id}");
            }

            // Set ViewBag properties
            ViewBag.HasSupportingDocument = hasDocuments;
            ViewBag.SupportingDocumentFileName = primaryDocumentName;
            ViewBag.SupportingDocumentUrl = primaryDocumentPath;
            ViewBag.SupportingDocumentSize = primaryDocumentSize;
            ViewBag.SupportingDocumentUploadDate = primaryDocumentUploadDate;

            // Calculate processing time
            if (claim.ApprovalDate.HasValue)
            {
                var processingTime = claim.ApprovalDate.Value - claim.CreatedDate;
                ViewBag.ProcessingTime = $"{processingTime.Days} days, {processingTime.Hours} hours";
            }
            else
            {
                var pendingTime = DateTime.Now - claim.CreatedDate;
                ViewBag.PendingTime = $"{pendingTime.Days} days, {pendingTime.Hours} hours";
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
        public async Task<IActionResult> Create(Claim claim, List<IFormFile> UploadedFiles) // Changed parameter name to match form
        {
            try
            {
                _logger.LogInformation($"Create POST action called with {UploadedFiles?.Count ?? 0} files");

                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateViewBagData();
                    return View("~/Views/Employee/Claim/Create.cshtml", claim);
                }

                // ADDED: Validate claim date is not in the future
                if (claim.ClaimDate > DateTime.Today)
                {
                    ModelState.AddModelError("ClaimDate", "Claim date cannot be in the future.");
                }

                // Validate model
                if (!ModelState.IsValid)
                {
                    await PopulateViewBagData();
                    return View("~/Views/Employee/Claim/Create.cshtml", claim);
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

                // Save the claim first to get the ID
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Claim saved with ID: {claim.Id}");

                // Handle multiple file uploads if provided
                if (UploadedFiles != null && UploadedFiles.Count > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                    Directory.CreateDirectory(uploadsFolder);

                    _logger.LogInformation($"Processing {UploadedFiles.Count} files for claim {claim.Id}");

                    var claimTypeId = await GetClaimTypeId(claim.ClaimType);
                    string firstFilePath = null;
                    string firstFileName = null;
                    int savedFileCount = 0;

                    foreach (var file in UploadedFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            // Validate file size (5MB limit)
                            if (file.Length > 5 * 1024 * 1024)
                            {
                                _logger.LogWarning($"File {file.FileName} exceeds size limit");
                                continue;
                            }

                            // Generate unique filename ONCE
                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            // Save file to disk
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            // Track first file for backward compatibility
                            if (firstFilePath == null)
                            {
                                firstFilePath = $"/uploads/claims/{uniqueFileName}";
                                firstFileName = file.FileName;
                            }

                            // Create ClaimDetails record for EACH file (this saves ALL files)
                            var claimDetail = new ClaimDetails
                            {
                                ClaimID = claim.Id,
                                ClaimTypeID = claimTypeId,
                                Comment = $"Supporting document: {file.FileName}",
                                DocumentPath = $"/uploads/claims/{uniqueFileName}",
                                OriginalFileName = file.FileName,
                                FileSize = file.Length,
                                UploadDate = DateTime.Now
                            };

                            _context.ClaimDetails.Add(claimDetail);
                            savedFileCount++;
                            _logger.LogInformation($"File {savedFileCount} saved: {file.FileName} -> {uniqueFileName}");
                        }
                    }

                    // Update main claim record for backward compatibility (only first file)
                    if (firstFilePath != null)
                    {
                        claim.SupportingDocumentName = firstFileName;
                        claim.SupportingDocumentPath = firstFilePath;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"All {savedFileCount} files processed and saved for claim {claim.Id}");

                    TempData["FileUploadStatus"] = $"Successfully uploaded {savedFileCount} file(s)";
                }

                // ADDED: Send email notification to HR when claim is submitted
                try
                {
                    await SendClaimSubmissionNotificationToHR(claim);
                    _logger.LogInformation($"Email notification sent to HR for claim {claim.Id}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send email notification to HR for claim {claim.Id}");
                    // Don't fail the entire operation if email fails
                }

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
                TempData["Error"] = $"Error: {ex.Message}";
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

            var claim = await _context.Claims.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

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
        public async Task<IActionResult> Edit(int id, List<IFormFile>? UploadedFiles)
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var claimToUpdate = await _context.Claims
                .Include(c => c.ClaimDetails)
                .FirstOrDefaultAsync(c => c.Id == id && c.EmployeeID == employeeId);

            if (claimToUpdate == null)
                return NotFound();

            // Only allow editing if status is Pending
            if (claimToUpdate.Status != "Pending")
            {
                TempData["Error"] = "You can only edit claims that are in Pending status.";
                return RedirectToAction(nameof(Index));
            }

            if (await TryUpdateModelAsync(claimToUpdate, "",
                c => c.ClaimType, c => c.ClaimAmount, c => c.Description, c => c.ClaimDate))
            {
                try
                {
                    // Handle new document uploads
                    if (UploadedFiles != null && UploadedFiles.Count > 0 && UploadedFiles.Any(f => f != null && f.Length > 0))
                    {
                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                        Directory.CreateDirectory(uploadsFolder);

                        var claimTypeId = await GetClaimTypeId(claimToUpdate.ClaimType);

                        // Delete old ClaimDetails files
                        var oldClaimDetails = claimToUpdate.ClaimDetails?.ToList() ?? new List<ClaimDetails>();
                        foreach (var oldDetail in oldClaimDetails)
                        {
                            var oldPath = Path.Combine(_environment.WebRootPath, oldDetail.DocumentPath.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath))
                                System.IO.File.Delete(oldPath);

                            _context.ClaimDetails.Remove(oldDetail);
                        }

                        // Add new files
                        foreach (var file in UploadedFiles.Where(f => f != null && f.Length > 0))
                        {
                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            var claimDetail = new ClaimDetails
                            {
                                ClaimID = claimToUpdate.Id,
                                ClaimTypeID = claimTypeId,
                                Comment = $"Supporting document: {file.FileName}",
                                DocumentPath = $"/uploads/claims/{uniqueFileName}",
                                OriginalFileName = file.FileName,
                                FileSize = file.Length,
                                UploadDate = DateTime.Now
                            };

                            _context.ClaimDetails.Add(claimDetail);
                        }

                        // Update main claim for backward compatibility
                        var firstFile = UploadedFiles.First(f => f != null && f.Length > 0);
                        var firstUniqueFileName = $"{Guid.NewGuid()}_{firstFile.FileName}";

                        claimToUpdate.SupportingDocumentName = firstFile.FileName;
                        claimToUpdate.SupportingDocumentPath = $"/uploads/claims/{firstUniqueFileName}";
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

            var claim = await _context.Claims.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (claim != null && claim.EmployeeID == employeeId)
            {
                // Only allow deletion if status is Pending
                if (claim.Status != "Pending")
                {
                    TempData["Error"] = "You can only delete claims that are in Pending status.";
                    return RedirectToAction(nameof(Index));
                }

                // Soft delete - mark as deleted instead of removing from database
                claim.IsDeleted = true;
                claim.DeletedDate = DateTime.Now;
                
                // Note: We keep the files for audit purposes, but you can uncomment below if you want to delete files
                // if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
                // {
                //     var filePath = Path.Combine(_environment.WebRootPath, claim.SupportingDocumentPath.TrimStart('/'));
                //     if (System.IO.File.Exists(filePath))
                //     {
                //         System.IO.File.Delete(filePath);
                //     }
                // }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Claim deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> OCR()
        {
            try
            {
                _logger.LogInformation("=== OCR GET ACTION CALLED ===");

                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    _logger.LogError("Employee ID not found for OCR page");
                    TempData["Error"] = "Employee record not found.";
                    return RedirectToAction("Create");
                }

                _logger.LogInformation($"Employee ID: {employeeId}");

                // Initialize claim with defaults - DON'T override with hardcoded values
                var claim = new Claim
                {
                    EmployeeID = employeeId,
                    Status = "Draft",
                    ClaimDate = DateTime.Today,
                    Currency = "MYR",
                    ClaimAmount = 0,
                    ClaimType = "", // Leave empty initially
                    Description = "" // Leave empty initially
                };

                // FIXED: Check TempData first (highest priority)
                if (TempData["ClaimData"] != null)
                {
                    try
                    {
                        var claimDataJson = TempData["ClaimData"].ToString();
                        _logger.LogInformation($"Found TempData ClaimData with length: {claimDataJson.Length}");
                        _logger.LogInformation($"TempData content preview: {claimDataJson.Substring(0, Math.Min(200, claimDataJson.Length))}...");

                        using var document = System.Text.Json.JsonDocument.Parse(claimDataJson);
                        var root = document.RootElement;
                        
                        // Log all available properties
                        _logger.LogInformation("Available JSON properties:");
                        foreach (var property in root.EnumerateObject())
                        {
                            _logger.LogInformation($"  - {property.Name}: {property.Value.ValueKind}");
                        }

                        // Parse all fields properly
                        if (root.TryGetProperty("ClaimType", out var claimTypeElement))
                        {
                            var claimType = claimTypeElement.GetString();
                            if (!string.IsNullOrWhiteSpace(claimType))
                            {
                                claim.ClaimType = claimType;
                                _logger.LogInformation($"✅ Set ClaimType from TempData: {claim.ClaimType}");
                            }
                        }

                        if (root.TryGetProperty("ClaimAmount", out var claimAmountElement))
                        {
                            // Handle both string and number formats
                            if (claimAmountElement.ValueKind == JsonValueKind.String)
                            {
                                if (decimal.TryParse(claimAmountElement.GetString(), out var amount))
                                {
                                    claim.ClaimAmount = amount;
                                }
                            }
                            else if (claimAmountElement.ValueKind == JsonValueKind.Number)
                            {
                                claim.ClaimAmount = claimAmountElement.GetDecimal();
                            }
                            _logger.LogInformation($"✅ Set ClaimAmount from TempData: {claim.ClaimAmount}");
                        }

                        if (root.TryGetProperty("ClaimDate", out var claimDateElement))
                        {
                            if (DateTime.TryParse(claimDateElement.GetString(), out var date))
                            {
                                claim.ClaimDate = date;
                                _logger.LogInformation($"✅ Set ClaimDate from TempData: {claim.ClaimDate}");
                            }
                        }

                        if (root.TryGetProperty("Description", out var descriptionElement))
                        {
                            var description = descriptionElement.GetString();
                            claim.Description = description ?? "";
                            _logger.LogInformation($"✅ Set Description from TempData: '{claim.Description}' (Length: {claim.Description?.Length ?? 0})");
                        }

                        // Store file information for client-side access
                        if (root.TryGetProperty("Files", out var filesElement))
                        {
                            ViewBag.UploadedFiles = filesElement.GetRawText();
                            _logger.LogInformation($"✅ Set UploadedFiles data");
                        }

                        _logger.LogInformation("✅ Successfully parsed claim data from TempData");
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON parsing failed for claim data");
                        TempData["Error"] = "Failed to parse claim data. Please try again.";
                        return RedirectToAction("Create");
                    }
                }
                else
                {
                    _logger.LogWarning("No TempData found - checking query parameters");

                    // Fallback to query parameters
                    if (!string.IsNullOrEmpty(Request.Query["claimType"]))
                    {
                        claim.ClaimType = Request.Query["claimType"].ToString();
                        _logger.LogInformation($"✅ Set ClaimType from query: {claim.ClaimType}");
                    }

                    if (decimal.TryParse(Request.Query["claimAmount"], out var queryAmount))
                    {
                        claim.ClaimAmount = queryAmount;
                        _logger.LogInformation($"✅ Set ClaimAmount from query: {claim.ClaimAmount}");
                    }

                    if (DateTime.TryParse(Request.Query["claimDate"], out var queryDate))
                    {
                        claim.ClaimDate = queryDate;
                        _logger.LogInformation($"✅ Set ClaimDate from query: {claim.ClaimDate}");
                    }

                    if (!string.IsNullOrEmpty(Request.Query["description"]))
                    {
                        claim.Description = Request.Query["description"].ToString();
                        _logger.LogInformation($"✅ Set Description from query: '{claim.Description}' (Length: {claim.Description?.Length ?? 0})");
                    }
                }

                // ONLY set fallback if absolutely nothing is set
                if (string.IsNullOrEmpty(claim.ClaimType))
                {
                    claim.ClaimType = "Travel"; // Changed from "Medical" to match user expectation
                    _logger.LogWarning("⚠️ Using fallback ClaimType: Travel");
                }
                else
                {
                    _logger.LogInformation($"✅ Using provided ClaimType: {claim.ClaimType}");
                }

                // Log description status
                if (string.IsNullOrWhiteSpace(claim.Description))
                {
                    _logger.LogWarning("⚠️ Description is empty or whitespace");
                }
                else
                {
                    _logger.LogInformation($"✅ Using provided Description: '{claim.Description}' (Length: {claim.Description.Length})");
                }

                // Add debug information
                ViewBag.DebugInfo = $"Employee: {claim.EmployeeID}, Type: {claim.ClaimType}, Amount: {claim.ClaimAmount}, Description: '{claim.Description}'";

                _logger.LogInformation($"=== OCR PAGE LOADING SUCCESSFUL - Final Values: ClaimType='{claim.ClaimType}', Description='{claim.Description}' ===");
                return View("~/Views/Employee/Claim/OCR.cshtml", claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading OCR page");
                TempData["Error"] = "An error occurred while loading the OCR page.";
                return RedirectToAction("Create");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> OCRWithData(string ClaimDataJson)
        {
            try
            {
                _logger.LogInformation($"OCRWithData called with data length: {ClaimDataJson?.Length ?? 0}");

                if (string.IsNullOrEmpty(ClaimDataJson))
                {
                    _logger.LogWarning("ClaimDataJson is null or empty");
                    return Json(new { success = false, error = "No data provided" });
                }

                // Store the claim data in TempData
                TempData["ClaimData"] = ClaimDataJson;
                TempData.Keep("ClaimData");

                _logger.LogInformation("✅ Successfully stored ClaimData in TempData");

                return Json(new { success = true, message = "Data stored successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing claim data for OCR");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> ProcessOCRAndSubmit(Claim model, string ocrResults)
        {
            try
            {
                _logger.LogInformation($"ProcessOCRAndSubmit called");
                _logger.LogInformation($"Received ClaimType: '{model.ClaimType}' (Length: {model.ClaimType?.Length ?? 0})");
                _logger.LogInformation($"Received Description: '{model.Description}' (Length: {model.Description?.Length ?? 0})");
                _logger.LogInformation($"Received ClaimAmount: {model.ClaimAmount}");
                _logger.LogInformation($"Received ClaimDate: {model.ClaimDate}");
                
                // Log all form data for debugging
                _logger.LogInformation("Form data received:");
                foreach (var key in Request.Form.Keys)
                {
                    _logger.LogInformation($"  {key}: '{Request.Form[key]}'");
                }

                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateViewBagData();
                    return View("~/Views/Employee/Claim/Create.cshtml", model);
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.ClaimType))
                {
                    ModelState.AddModelError("ClaimType", "Please select a valid Claim Type");
                }

                if (model.ClaimAmount <= 0)
                    ModelState.AddModelError("ClaimAmount", "Claim Amount must be greater than 0");

                if (model.ClaimDate == default(DateTime) || model.ClaimDate > DateTime.Today)
                    ModelState.AddModelError("ClaimDate", "Valid Claim Date is required and cannot be in the future");

                if (!ModelState.IsValid)
                {
                    _logger.LogError("Model validation failed");
                    foreach (var error in ModelState)
                    {
                        _logger.LogError($"Field: {error.Key}, Errors: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                    await PopulateViewBagData();
                    return View("~/Views/Employee/Claim/Create.cshtml", model);
                }

                // Set claim properties
                model.EmployeeID = employeeId;
                model.CreatedDate = DateTime.Now;
                model.SubmissionDate = DateTime.Now;
                model.Status = "Pending";
                model.TotalAmount = model.ClaimAmount;
                model.Currency = "MYR";
                model.ApprovalDate = null;
                model.ApprovedBy = null;
                model.ApprovalRemarks = null;

                if (string.IsNullOrWhiteSpace(model.Description))
                {
                    model.Description = "";
                }

                _logger.LogInformation($"About to save claim: Type={model.ClaimType}, Amount={model.ClaimAmount}, Description={model.Description}");

                // Save the claim first to get the ID
                _context.Claims.Add(model);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Claim saved with ID: {model.Id}");

                // FIXED: Handle files from OCR results AND TempData
                var savedFileCount = 0;
                List<object> filesData = new List<object>();

                // First try to get files from OCR results
                if (!string.IsNullOrEmpty(ocrResults))
                {
                    try
                    {
                        using var document = System.Text.Json.JsonDocument.Parse(ocrResults);
                        var root = document.RootElement;

                        if (root.TryGetProperty("filesData", out var filesDataElement))
                        {
                            foreach (var fileElement in filesDataElement.EnumerateArray())
                            {
                                if (fileElement.TryGetProperty("name", out var nameElement) &&
                                    fileElement.TryGetProperty("data", out var dataElement))
                                {
                                    filesData.Add(new
                                    {
                                        name = nameElement.GetString(),
                                        data = dataElement.GetString(),
                                        type = fileElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : "application/octet-stream",
                                        size = fileElement.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing OCR results for files");
                    }
                }

                // If no files in OCR results, try TempData
                if (filesData.Count == 0 && TempData["ClaimData"] != null)
                {
                    try
                    {
                        using var tempDoc = System.Text.Json.JsonDocument.Parse(TempData["ClaimData"].ToString());
                        if (tempDoc.RootElement.TryGetProperty("Files", out var filesElement))
                        {
                            foreach (var fileElement in filesElement.EnumerateArray())
                            {
                                if (fileElement.TryGetProperty("name", out var nameElement) &&
                                    fileElement.TryGetProperty("data", out var dataElement))
                                {
                                    filesData.Add(new
                                    {
                                        name = nameElement.GetString(),
                                        data = dataElement.GetString(),
                                        type = fileElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : "application/octet-stream",
                                        size = fileElement.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing TempData for files");
                    }
                }

                _logger.LogInformation($"Found {filesData.Count} files to process");

                // Process and save files
                if (filesData.Count > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                    Directory.CreateDirectory(uploadsFolder);
                    var claimTypeId = await GetClaimTypeId(model.ClaimType);

                    string firstFilePath = null;
                    string firstFileName = null;

                    foreach (var file in filesData)
                    {
                        try
                        {
                            var fileName = file.GetType().GetProperty("name")?.GetValue(file)?.ToString();
                            var base64Data = file.GetType().GetProperty("data")?.GetValue(file)?.ToString();
                            var fileType = file.GetType().GetProperty("type")?.GetValue(file)?.ToString();
                            var fileSize = Convert.ToInt64(file.GetType().GetProperty("size")?.GetValue(file) ?? 0);

                            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(base64Data))
                            {
                                // Convert base64 to bytes
                                var fileBytes = Convert.FromBase64String(base64Data);

                                // Generate unique filename
                                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                                // Save file to disk
                                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                                // Track first file for backward compatibility
                                if (firstFilePath == null)
                                {
                                    firstFilePath = $"/uploads/claims/{uniqueFileName}";
                                    firstFileName = fileName;
                                }

                                // Create ClaimDetails record
                                var claimDetail = new ClaimDetails
                                {
                                    ClaimID = model.Id,
                                    ClaimTypeID = claimTypeId,
                                    Comment = $"Supporting document: {fileName} (OCR Processed)",
                                    DocumentPath = $"/uploads/claims/{uniqueFileName}",
                                    OriginalFileName = fileName,
                                    FileSize = fileBytes.Length,
                                    UploadDate = DateTime.Now
                                };

                                _context.ClaimDetails.Add(claimDetail);
                                savedFileCount++;
                                _logger.LogInformation($"OCR File {savedFileCount} saved: {fileName} -> {uniqueFileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing individual file");
                        }
                    }

                    // Update main claim record for backward compatibility
                    if (firstFilePath != null)
                    {
                        model.SupportingDocumentName = firstFileName;
                        model.SupportingDocumentPath = firstFilePath;
                        _context.Claims.Update(model);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"All {savedFileCount} OCR files processed and saved for claim {model.Id}");
                }

                if (savedFileCount > 0)
                {
                    TempData["FileUploadStatus"] = $"Successfully uploaded {savedFileCount} file(s) via OCR";
                }

                // ADDED: Send email notification to HR when claim is submitted via OCR
                try
                {
                    await SendClaimSubmissionNotificationToHR(model);
                    _logger.LogInformation($"Email notification sent to HR for OCR claim {model.Id}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send email notification to HR for OCR claim {model.Id}");
                    // Don't fail the entire operation if email fails
                }

                TempData["Success"] = $"Claim submitted successfully! Claim ID: {model.Id}";
                return RedirectToAction("Index", "Claim");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating claim via OCR");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                TempData["Error"] = $"Error: {ex.Message}";
                await PopulateViewBagData();
                return View("~/Views/Employee/Claim/Create.cshtml", model);
            }
        }

        // Helper method to preserve uploaded files (you'll need to implement this)
        private async Task PreserveUploadedFiles(List<IFormFile> files)
        {
            // TODO: Implement logic to preserve files temporarily
            // This could involve saving them to a temp location or session storage
            await Task.CompletedTask;
        }

        // Helper method to cleanup uploaded files on error
        private async Task CleanupUploadedFiles(List<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                try
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                        Console.WriteLine($"🗑️ Cleaned up file: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to cleanup file {path}: {ex.Message}");
                }
            }
            await Task.CompletedTask;
        }

        // HELPER: Separate method for handling file uploads
        private async Task HandleFileUploads(Claim claim, List<IFormFile> uploadedFiles)
        {
            if (uploadedFiles == null || uploadedFiles.Count == 0 || !uploadedFiles.Any(f => f != null && f.Length > 0))
            {
                _logger.LogInformation("No files to upload");
                return;
            }

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                Directory.CreateDirectory(uploadsFolder);

                _logger.LogInformation($"Processing {uploadedFiles.Count} files for claim {claim.Id}");

                var claimTypeId = await GetClaimTypeId(claim.ClaimType);

                foreach (var file in uploadedFiles.Where(f => f != null && f.Length > 0))
                {
                    // Validate file size (5MB limit)
                    if (file.Length > 5 * 1024 * 1024)
                    {
                        _logger.LogWarning($"File {file.FileName} exceeds size limit");
                        continue;
                    }

                    // Generate unique filename
                    var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save file to disk
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    // Create ClaimDetails record for each file
                    var claimDetail = new ClaimDetails
                    {
                        ClaimID = claim.Id,
                        ClaimTypeID = claimTypeId,
                        Comment = $"Supporting document: {file.FileName} (OCR Validated)",
                        DocumentPath = $"/uploads/claims/{uniqueFileName}",
                        OriginalFileName = file.FileName,
                        FileSize = file.Length,
                        UploadDate = DateTime.Now
                    };

                    _context.ClaimDetails.Add(claimDetail);
                    _logger.LogInformation($"File saved: {file.FileName} -> {uniqueFileName}");
                }

                // Update main claim record for backward compatibility (use first file)
                var firstFile = uploadedFiles.First(f => f != null && f.Length > 0);
                var firstUniqueFileName = $"{Guid.NewGuid()}_{firstFile.FileName}";

                claim.SupportingDocumentName = firstFile.FileName;
                claim.SupportingDocumentPath = $"/uploads/claims/{firstUniqueFileName}";

                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ All files processed and saved for claim {claim.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file uploads");
                throw; // Re-throw to be handled by calling method
            }
        }

        // Add this helper method for handling files from OCR page
        [HttpPost]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> UploadFilesForOCR(List<IFormFile> files)
        {
            try
            {
                var results = new List<object>();

                if (files != null && files.Count > 0)
                {
                    var tempFolder = Path.Combine(_environment.WebRootPath, "temp", "ocr");
                    Directory.CreateDirectory(tempFolder);

                    foreach (var file in files)
                    {
                        if (file != null && file.Length > 0)
                        {
                            // Validate file size (5MB limit)
                            if (file.Length > 5 * 1024 * 1024)
                            {
                                results.Add(new
                                {
                                    fileName = file.FileName,
                                    success = false,
                                    error = "File size exceeds 5MB limit"
                                });
                                continue;
                            }

                            // Generate unique filename for temporary storage
                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var filePath = Path.Combine(tempFolder, uniqueFileName);

                            // Save file temporarily
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            // Convert to base64 for client-side OCR processing
                            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                            string base64String = Convert.ToBase64String(fileBytes);

                            results.Add(new
                            {
                                fileName = file.FileName,
                                originalName = file.FileName,
                                fileSize = file.Length,
                                fileType = file.ContentType,
                                base64Data = $"data:{file.ContentType};base64,{base64String}",
                                success = true,
                                tempPath = uniqueFileName
                            });

                            // Clean up temp file after converting to base64
                            System.IO.File.Delete(filePath);
                        }
                    }
                }

                return Json(new { success = true, files = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files for OCR");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> ProcessOCRServer(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return Json(new { success = false, error = "No file provided" });

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return Json(new { success = false, error = "Only JPG, PNG, and PDF files are supported" });

                // Validate file size (5MB limit)
                if (file.Length > 5 * 1024 * 1024)
                    return Json(new { success = false, error = "File size must be less than 5MB" });

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var result = await _ocrService.ProcessImageAsync(imageData);

                return Json(new
                {
                    success = result.Success,
                    text = result.Text,
                    confidence = result.Confidence,
                    wordCount = result.WordCount,
                    error = result.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server OCR processing failed");
                return Json(new { success = false, error = "OCR processing failed. Please try again." });
            }
        }

        // ================== HR ACTIONS ==================

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> HRIndex()
        {
            var claims = await _context.Claims
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/HRIndex.cshtml", claims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ClaimDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.Claims
                .Include(c => c.ClaimDetails)
                .Where(c => !c.IsDeleted)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (claim == null)
            {
                return NotFound();
            }

            // Get employee information
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeID);

            if (employee != null)
            {
                ViewBag.EmployeeName = $"{employee.FirstName} {employee.LastName}";
            }

            // Get supporting documents from ClaimDetails
            var documents = new List<ClaimDetails>();
            if (claim.ClaimDetails != null && claim.ClaimDetails.Any())
            {
                documents = claim.ClaimDetails.ToList();
            }

            ViewBag.Documents = documents;

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
                .Include(c => c.ClaimDetails)
                .Where(c => !c.IsDeleted)
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

            // Get employee information
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeID);

            if (employee != null)
            {
                ViewBag.EmployeeName = $"{employee.FirstName} {employee.LastName}";
            }

            // Get supporting documents from ClaimDetails
            var documents = new List<ClaimDetails>();
            if (claim.ClaimDetails != null && claim.ClaimDetails.Any())
            {
                documents = claim.ClaimDetails.ToList();
            }

            ViewBag.Documents = documents;

            return View("~/Views/HR/Claim/ProcessClaim.cshtml", claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessClaim(int id, string action, string? remarks)
        {
            var claim = await _context.Claims.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
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

                if (currentUser == null)
                {
                    TempData["Error"] = "Unable to identify current user. Please try again.";
                    return RedirectToAction(nameof(ProcessClaim), new { id = id });
                }

                // Update claim based on action
                if (action == "approve")
                {
                    claim.Status = "Approved";
                    claim.ApprovalDate = DateTime.Now;
                    claim.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim(); // HR user name for approval tracking
                    claim.ApprovalRemarks = remarks;
                    TempData["Success"] = "Claim approved successfully!";
                }
                else if (action == "reject")
                {
                    claim.Status = "Rejected";
                    claim.ApprovalDate = DateTime.Now;
                    claim.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim(); // HR user name for approval tracking
                    claim.ApprovalRemarks = remarks;
                    TempData["Success"] = "Claim rejected successfully!";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessClaim), new { id = id });
                }

                await _context.SaveChangesAsync();

                // ADDED: Send email notification to employee about claim status
                try
                {
                    await SendClaimStatusNotificationToEmployee(claim, currentUser);
                    _logger.LogInformation($"Email notification sent to employee for claim {claim.Id} status: {claim.Status}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send email notification to employee for claim {claim.Id}");
                    // Don't fail the entire operation if email fails
                }

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
                .Where(c => c.Status == "Pending" && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/PendingClaim.cshtml", pendingClaims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ApprovedClaims()
        {
            var approvedClaims = await _context.Claims
                .Where(c => c.Status == "Approved" && !c.IsDeleted)
                .OrderByDescending(c => c.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/ApprovedClaim.cshtml", approvedClaims);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> RejectedClaims()
        {
            var rejectedClaims = await _context.Claims
                .Where(c => c.Status == "Rejected" && !c.IsDeleted)
                .OrderByDescending(c => c.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Claim/RejectedClaim.cshtml", rejectedClaims);
        }

        // ================== EMAIL NOTIFICATION METHODS ==================

        /// <summary>
        /// Sends email notification to HR when a new claim is submitted
        /// </summary>
        private async Task SendClaimSubmissionNotificationToHR(Claim claim)
        {
            try
            {
                // Get employee information
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeID);

                // Get all HR users
                var hrUsers = await _userManager.GetUsersInRoleAsync("HR");

                if (!hrUsers.Any())
                {
                    _logger.LogWarning("No HR users found to send claim notification");
                    return;
                }

                // Email subject and body
                string subject = $"Claim Approval Request for {employee.FirstName} {employee.LastName}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2c3e50;'>Claim Approval Request</h2>
                    
                    <div style='background-color: #e8f4fd; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;'>
                        <h3 style='color: #007bff; margin-top: 0;'>New Claim Submitted</h3>
                        <p style='margin-bottom: 0;'>A new claim has been submitted and requires your review.</p>
                    </div>

                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='color: #495057; margin-top: 0;'>Claim Details</h3>
                        <p><strong>Employee:</strong> {employee.FirstName} {employee.LastName} ({employee.EmployeeID})</p>
                        <p><strong>Claim Type:</strong> {claim.ClaimType}</p>
                        <p><strong>Amount:</strong> {claim.Currency} {claim.ClaimAmount:F2}</p>
                        <p><strong>Claim Date:</strong> {claim.ClaimDate:dd/MM/yyyy}</p>
                        <p><strong>Description:</strong> {claim.Description ?? "No description provided"}</p>
                        <p><strong>Submitted On:</strong> {claim.SubmissionDate:dd/MM/yyyy HH:mm}</p>
                    </div>

                    <div style='background-color: #e8f4fd; padding: 15px; border-radius: 5px; border-left: 4px solid #007bff;'>
                        <p style='margin: 0;'><strong>Action Required:</strong> This claim requires your review and approval.</p>
                    </div>

                    <div style='margin-top: 30px;'>
                        <p><a href='{Url.Action("ClaimDetails", "Claim", new { id = claim.Id }, Request.Scheme)}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Click here to review this claim</a></p>
                        <p style='font-size: 12px; color: #6c757d;'>
                            This is an automated notification from the Finserve Claim Management System.
                        </p>
                    </div>
                </body>
                </html>";

                // Send email to all HR users
                foreach (var hrUser in hrUsers)
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(hrUser.Email, subject, body);
                        _logger.LogInformation($"Claim submission notification sent to HR user: {hrUser.Email}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send email to HR user: {hrUser.Email}");
                    }
                }

                // Also send email to the specific HR email address
                try
                {
                    await _emailSender.SendEmailAsync("hr001@cubicsoftware.com.my", subject, body);
                    _logger.LogInformation("Claim submission notification sent to hr001@cubicsoftware.com.my");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to hr001@cubicsoftware.com.my");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending claim submission notification to HR");
                throw;
            }
        }

        /// <summary>
        /// Sends email notification to employee when claim status is updated
        /// </summary>
        private async Task SendClaimStatusNotificationToEmployee(Claim claim, ApplicationUser approver)
        {
            try
            {
                // Get employee information
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == claim.EmployeeID);

                if (employee == null || string.IsNullOrEmpty(employee.Email))
                {
                    _logger.LogWarning($"Employee email not found for employee ID: {claim.EmployeeID}");
                    return;
                }

                // Determine status color and message
                string statusColor = claim.Status == "Approved" ? "#28a745" : "#dc3545";
                string statusMessage = claim.Status == "Approved"
                    ? "Your claim has been approved and will be processed for payment."
                    : "Your claim has been rejected. Please review the remarks below.";

                // Email subject and body
                string subject = $"Claim {claim.Status} - ID: {claim.Id}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2c3e50;'>Claim Status Update</h2>
                    
                    <div style='background-color: {(claim.Status == "Approved" ? "#d4edda" : "#f8d7da")}; 
                                padding: 20px; border-radius: 5px; margin: 20px 0; 
                                border-left: 4px solid {statusColor};'>
                        <h3 style='color: {statusColor}; margin-top: 0;'>
                            Your claim has been {claim.Status.ToUpper()}
                        </h3>
                        <p style='margin-bottom: 0;'>{statusMessage}</p>
                    </div>

                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='color: #495057; margin-top: 0;'>Claim Details</h3>
                        <p><strong>Claim ID:</strong> {claim.Id}</p>
                        <p><strong>Claim Type:</strong> {claim.ClaimType}</p>
                        <p><strong>Amount:</strong> {claim.Currency} {claim.ClaimAmount:F2}</p>
                        <p><strong>Claim Date:</strong> {claim.ClaimDate:dd/MM/yyyy}</p>
                        <p><strong>Submitted On:</strong> {claim.SubmissionDate:dd/MM/yyyy HH:mm}</p>
                        <p><strong>Processed On:</strong> {claim.ApprovalDate:dd/MM/yyyy HH:mm}</p>
                        <p><strong>Processed By:</strong> {approver.FirstName} {approver.LastName}</p>
                        {(string.IsNullOrEmpty(claim.ApprovalRemarks) ? "" : $"<p><strong>Remarks:</strong> {claim.ApprovalRemarks}</p>")}
                    </div>

                    <div style='margin-top: 30px;'>
                        <p>You can view the full details of your claim by logging into the system.</p>
                        <p style='font-size: 12px; color: #6c757d;'>
                            This is an automated notification from the Finserve Claim Management System.
                        </p>
                    </div>
                </body>
                </html>";

                // Send email to employee
                await _emailSender.SendEmailAsync(employee.Email, subject, body);
                _logger.LogInformation($"Claim status notification sent to employee: {employee.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending claim status notification to employee");
                throw;
            }
        }

        // ================== HELPER METHODS ==================

        private async Task<string> GetCurrentEmployeeId()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                _logger.LogWarning("Current user is null - user may not be authenticated properly");
                return null;
            }

            _logger.LogInformation($"Current user: {currentUser.UserName}, Email: {currentUser.Email}");

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Username == currentUser.UserName || e.Email == currentUser.Email);

            if (employee == null)
            {
                _logger.LogWarning($"No employee found for username: {currentUser.UserName} or email: {currentUser.Email}");

                // For testing purposes, return E001 if the user is employee@finserve.com
                if (currentUser.Email == "employee@finserve.com")
                {
                    _logger.LogInformation("Using hardcoded employee ID for test user");
                    return "E001";
                }
            }

            return employee?.EmployeeID;
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 Bytes";

            string[] sizes = { "Bytes", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private async Task PopulateViewBagData()
        {
            ViewBag.ClaimTypes = new List<string>
            {
                "Travel",
                "Medical",
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

        private async Task<int> GetClaimTypeId(string claimTypeName)
        {
            var claimType = await _context.ClaimTypes
                .FirstOrDefaultAsync(ct => ct.Name == claimTypeName);

            if (claimType == null)
            {
                // Create new claim type if it doesn't exist
                claimType = new ClaimType
                {
                    Name = claimTypeName,
                    Description = $"Auto-created claim type for {claimTypeName}",
                    RequiresApproval = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.ClaimTypes.Add(claimType);
                await _context.SaveChangesAsync();
            }

            return claimType.Id;
        }
    }
}