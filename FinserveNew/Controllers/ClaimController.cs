using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinserveNew.Controllers
{
    public class ClaimController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ClaimController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOcrService _ocrService;

        public ClaimController(AppDbContext context, IWebHostEnvironment environment, ILogger<ClaimController> logger, UserManager<ApplicationUser> userManager, IOcrService ocrService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _userManager = userManager;
            _ocrService = ocrService;
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
                .Include(c => c.ClaimDetails) // Include claim details
                .FirstOrDefaultAsync(m => m.Id == id && m.EmployeeID == employeeId);

            if (claim == null)
            {
                return NotFound();
            }

            // Get approver information if claim is processed
            if (!string.IsNullOrEmpty(claim.ApprovedBy))
            {
                var approver = await _userManager.FindByIdAsync(claim.ApprovedBy);
                if (approver != null)
                {
                    ViewBag.ApproverName = $"{approver.FirstName} {approver.LastName}";
                    ViewBag.ApproverEmail = approver.Email;
                }
                else
                {
                    ViewBag.ApproverName = "Unknown Approver";
                    ViewBag.ApproverEmail = "";
                }
            }

            // Check for documents in ClaimDetails first, then fallback to main claim
            var documents = claim.ClaimDetails?.ToList() ?? new List<ClaimDetails>();
            ViewBag.Documents = documents;
            ViewBag.DocumentCount = documents.Count;

            // Backward compatibility - check main claim document
            if (documents.Count == 0 && !string.IsNullOrEmpty(claim.SupportingDocumentPath))
            {
                ViewBag.HasSupportingDocument = true;
                ViewBag.SupportingDocumentFileName = claim.SupportingDocumentName ?? Path.GetFileName(claim.SupportingDocumentPath);
                ViewBag.SupportingDocumentUrl = claim.SupportingDocumentPath;

                // Get file size if possible
                try
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, claim.SupportingDocumentPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        ViewBag.SupportingDocumentSize = FormatFileSize(fileInfo.Length);
                        ViewBag.SupportingDocumentUploadDate = fileInfo.CreationTime.ToString("dd/MM/yyyy HH:mm");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not get file info for {claim.SupportingDocumentPath}: {ex.Message}");
                    ViewBag.SupportingDocumentSize = "Unknown";
                    ViewBag.SupportingDocumentUploadDate = "Unknown";
                }
            }
            else
            {
                ViewBag.HasSupportingDocument = documents.Count > 0;
            }

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

                    // Get the first ClaimType ID (you might need to adjust this based on your requirements)
                    var claimTypeId = await GetClaimTypeId(claim.ClaimType);

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
                                Comment = $"Supporting document: {file.FileName}",
                                DocumentPath = $"/uploads/claims/{uniqueFileName}",
                                OriginalFileName = file.FileName,
                                FileSize = file.Length,
                                UploadDate = DateTime.Now
                            };

                            _context.ClaimDetails.Add(claimDetail);

                            _logger.LogInformation($"File saved: {file.FileName} -> {uniqueFileName}");
                        }
                    }

                    // If we have files, also update the main claim record for backward compatibility
                    if (UploadedFiles.Any(f => f != null && f.Length > 0))
                    {
                        var firstFile = UploadedFiles.First(f => f != null && f.Length > 0);
                        var firstUniqueFileName = $"{Guid.NewGuid()}_{firstFile.FileName}";

                        claim.SupportingDocumentName = firstFile.FileName;
                        claim.SupportingDocumentPath = $"/uploads/claims/{firstUniqueFileName}";
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"All files processed and saved for claim {claim.Id}");

                    TempData["FileUploadStatus"] = $"Successfully uploaded {UploadedFiles.Count(f => f != null && f.Length > 0)} file(s)";
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

        // Add this OCR action method to your existing ClaimController class

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> OCR()
        {
            try
            {
                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    TempData["Error"] = "Employee record not found.";
                    return RedirectToAction("Create");
                }

                // Create a new claim model with default values for the OCR page
                var claim = new Claim
                {
                    EmployeeID = employeeId,
                    ClaimDate = DateTime.Now,
                    Status = "Draft",
                    ClaimAmount = 0, // Default value
                    ClaimType = "Not specified" // Default value
                };

                // Try to get claim data from TempData first
                if (TempData["ClaimData"] != null)
                {
                    try
                    {
                        var claimDataJson = TempData["ClaimData"].ToString();
                        _logger.LogInformation($"Found TempData ClaimData with length: {claimDataJson.Length}");
                        _logger.LogInformation($"ClaimData content: {claimDataJson}");

                        // Use JsonDocument for better parsing with more detailed error handling
                        using var document = System.Text.Json.JsonDocument.Parse(claimDataJson);
                        var root = document.RootElement;

                        // Populate claim from TempData with proper type conversion and logging
                        if (root.TryGetProperty("ClaimType", out var claimTypeElement))
                        {
                            claim.ClaimType = claimTypeElement.GetString() ?? "Not specified";
                            _logger.LogInformation($"✅ Set ClaimType: {claim.ClaimType}");
                        }
                        else
                        {
                            _logger.LogWarning("❌ ClaimType property not found in JSON");
                        }

                        if (root.TryGetProperty("ClaimAmount", out var claimAmountElement))
                        {
                            var amountStr = claimAmountElement.GetString();
                            _logger.LogInformation($"ClaimAmount string from JSON: '{amountStr}'");

                            if (decimal.TryParse(amountStr, out var amount))
                            {
                                claim.ClaimAmount = amount;
                                _logger.LogInformation($"✅ Set ClaimAmount: {claim.ClaimAmount}");
                            }
                            else
                            {
                                _logger.LogWarning($"❌ Failed to parse ClaimAmount: '{amountStr}'");
                                claim.ClaimAmount = 0;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("❌ ClaimAmount property not found in JSON");
                        }

                        if (root.TryGetProperty("ClaimDate", out var claimDateElement))
                        {
                            var dateStr = claimDateElement.GetString();
                            _logger.LogInformation($"ClaimDate string from JSON: '{dateStr}'");

                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                claim.ClaimDate = date;
                                _logger.LogInformation($"✅ Set ClaimDate: {claim.ClaimDate}");
                            }
                            else
                            {
                                _logger.LogWarning($"❌ Failed to parse ClaimDate: '{dateStr}'");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("❌ ClaimDate property not found in JSON");
                        }

                        if (root.TryGetProperty("Description", out var descriptionElement))
                        {
                            claim.Description = descriptionElement.GetString() ?? "";
                            _logger.LogInformation($"✅ Set Description: {claim.Description}");
                        }

                        // Store currency information in ViewBag
                        if (root.TryGetProperty("OriginalCurrency", out var originalCurrencyElement))
                        {
                            ViewBag.OriginalCurrency = originalCurrencyElement.GetString();
                            _logger.LogInformation($"✅ Set OriginalCurrency: {ViewBag.OriginalCurrency}");
                        }

                        if (root.TryGetProperty("OriginalAmount", out var originalAmountElement))
                        {
                            var origAmountStr = originalAmountElement.GetString();
                            if (decimal.TryParse(origAmountStr, out var origAmount))
                            {
                                ViewBag.OriginalAmount = origAmount;
                                _logger.LogInformation($"✅ Set OriginalAmount: {ViewBag.OriginalAmount}");
                            }
                        }

                        // Store file information in ViewBag for JavaScript access
                        if (root.TryGetProperty("Files", out var filesElement))
                        {
                            ViewBag.UploadedFiles = filesElement.GetRawText();
                            var fileCount = filesElement.GetArrayLength();
                            _logger.LogInformation($"✅ Set UploadedFiles count: {fileCount}");
                        }

                        _logger.LogInformation($"✅ Successfully parsed claim data - Type: '{claim.ClaimType}', Amount: {claim.ClaimAmount}, Date: {claim.ClaimDate}");
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "❌ JSON parsing failed for claim data");
                        _logger.LogError($"❌ Raw JSON content: {TempData["ClaimData"]}");
                        TempData["Error"] = "Failed to parse claim data (JSON error). Please try again.";
                        return RedirectToAction("Create");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ General error parsing claim data from TempData");
                        _logger.LogError($"❌ Raw TempData content: {TempData["ClaimData"]}");
                        TempData["Error"] = "Failed to load claim data. Please try again.";
                        return RedirectToAction("Create");
                    }
                }
                else
                {
                    _logger.LogWarning("❌ No TempData found for ClaimData, checking alternative sources");

                    // Check if there's data in session (client-side fallback)
                    if (Request.Headers.ContainsKey("Referer"))
                    {
                        _logger.LogInformation("📱 OCR accessed via redirect, data should be in client sessionStorage");
                        // Let client-side handle it - the JavaScript will load from sessionStorage
                    }
                    else
                    {
                        _logger.LogError("❌ No claim data found anywhere");
                        TempData["Error"] = "No claim data found. Please start from the Create form.";
                        return RedirectToAction("Create");
                    }
                }

                // Add debug information to help troubleshoot
                ViewBag.DebugInfo = $"ClaimType: {claim.ClaimType}, Amount: {claim.ClaimAmount}, EmployeeID: {claim.EmployeeID}";

                return View("~/Views/Employee/Claim/OCR.cshtml", claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading OCR page");
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
                    _logger.LogWarning("❌ ClaimDataJson is null or empty");
                    return Json(new { success = false, error = "No data provided" });
                }

                // Log the received JSON for debugging
                _logger.LogInformation($"📝 Received JSON: {ClaimDataJson}");

                // Validate JSON structure before storing
                try
                {
                    using var document = System.Text.Json.JsonDocument.Parse(ClaimDataJson);
                    var root = document.RootElement;

                    // Log key properties for debugging
                    if (root.TryGetProperty("ClaimType", out var claimTypeElement))
                    {
                        _logger.LogInformation($"✅ JSON contains ClaimType: {claimTypeElement.GetString()}");
                    }
                    else
                    {
                        _logger.LogWarning("❌ JSON missing ClaimType property");
                    }

                    if (root.TryGetProperty("ClaimAmount", out var claimAmountElement))
                    {
                        _logger.LogInformation($"✅ JSON contains ClaimAmount: {claimAmountElement.GetString()}");
                    }
                    else
                    {
                        _logger.LogWarning("❌ JSON missing ClaimAmount property");
                    }
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "❌ Invalid JSON structure received");
                    return Json(new { success = false, error = "Invalid JSON format" });
                }

                // Store the claim data in TempData so OCR action can access it
                TempData["ClaimData"] = ClaimDataJson;
                _logger.LogInformation("✅ Successfully stored ClaimData in TempData");

                // IMPORTANT: Keep TempData for the next request
                TempData.Keep("ClaimData");

                return Json(new
                {
                    success = true,
                    message = "Data stored successfully",
                    dataLength = ClaimDataJson.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error storing claim data for OCR");
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    details = ex.ToString()
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> ProcessOCRAndSubmit(Claim claim, List<IFormFile> UploadedFiles, string ocrResults = "")
        {
            try
            {
                _logger.LogInformation($"ProcessOCRAndSubmit called with {UploadedFiles?.Count ?? 0} files");

                // Get current employee ID
                var employeeId = await GetCurrentEmployeeId();

                if (string.IsNullOrEmpty(employeeId))
                {
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    return View("~/Views/Employee/Claim/OCR.cshtml", claim);
                }

                // Set claim values
                claim.EmployeeID = employeeId;
                claim.CreatedDate = DateTime.Now;
                claim.SubmissionDate = DateTime.Now;
                claim.Status = "Pending";
                claim.TotalAmount = claim.ClaimAmount;
                claim.ApprovalDate = null;
                claim.ApprovedBy = null;
                claim.ApprovalRemarks = null;

                // Enhanced description with OCR validation status
                if (!string.IsNullOrEmpty(ocrResults))
                {
                    var ocrSection = "\n\n=== OCR VALIDATION RESULTS ===\n" + ocrResults;

                    if (!string.IsNullOrEmpty(claim.Description))
                    {
                        claim.Description += ocrSection;
                    }
                    else
                    {
                        claim.Description = "OCR processed claim" + ocrSection;
                    }
                }

                // Save the claim first to get the ID
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Claim saved with ID: {claim.Id}");

                // Handle file uploads (existing code)
                if (UploadedFiles != null && UploadedFiles.Count > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "claims");
                    Directory.CreateDirectory(uploadsFolder);

                    var claimTypeId = await GetClaimTypeId(claim.ClaimType);

                    foreach (var file in UploadedFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            if (file.Length > 5 * 1024 * 1024)
                            {
                                _logger.LogWarning($"File {file.FileName} exceeds size limit");
                                continue;
                            }

                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

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
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Claim submitted successfully with OCR validation!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing OCR claim");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                return View("~/Views/Employee/Claim/OCR.cshtml", claim);
            }
        }

        // Add this helper method for handling file uploads from OCR page
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