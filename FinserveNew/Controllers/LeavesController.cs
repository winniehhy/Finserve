using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FinserveNew.Controllers
{
    public class LeavesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LeavesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeavesController(AppDbContext context, ILogger<LeavesController> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // ================== HELPER METHOD TO GET EMPLOYEE ID ==================
        private async Task<string> GetCurrentEmployeeId()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Add null check BEFORE accessing properties
            if (currentUser == null)
            {
                _logger.LogWarning("No authenticated user found");
                return null; // or throw an exception
            }

            _logger.LogInformation($"Current user: {currentUser.UserName}, Email: {currentUser.Email}");

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Username == currentUser.UserName || e.Email == currentUser.Email);

            if (employee == null)
            {
                _logger.LogWarning($"No employee found for username: {currentUser.UserName} or email: {currentUser.Email}");
            }

            return employee?.EmployeeID;
        }

        // ================== EMPLOYEE ACTIONS ==================

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> LeaveRecords()
        {
            // Get current employee ID
            var employeeId = await GetCurrentEmployeeId();

            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", new List<LeaveModel>());
            }

            // Updated query to sort by latest application first (by SubmissionDate, then CreatedDate as fallback)
            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeID == employeeId)
                .OrderByDescending(l => l.SubmissionDate != DateTime.MinValue ? l.SubmissionDate : l.CreatedDate) // Sort by application date, not leave date
                .ThenByDescending(l => l.LeaveID) // Secondary sort by ID for consistency
                .ToListAsync();

            // Calculate dynamic leave balances
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

            ViewBag.LeaveBalances = leaveBalances;

            // Populate individual ViewBag properties for the view
            PopulateLeaveBalanceViewBag(leaveBalances);

            return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", leaves);
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

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave == null)
            {
                return NotFound();
            }

            // ADD THIS: Check if this leave has a medical certificate
            var leaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveDetails != null)
            {
                ViewBag.HasMedicalCertificate = true;
                ViewBag.MedicalCertificateFileName = Path.GetFileName(leaveDetails.DocumentPath);
                ViewBag.MedicalCertificateUrl = leaveDetails.DocumentPath;
                ViewBag.MedicalCertificateUploadDate = leaveDetails.UploadDate.ToString("dd/MM/yyyy HH:mm") ?? "Unknown";
            }
            else
            {
                ViewBag.HasMedicalCertificate = false;
                ViewBag.MedicalCertificateFileName = "";
                ViewBag.MedicalCertificateUrl = "";
                ViewBag.MedicalCertificateUploadDate = "";
            }

            return View("~/Views/Employee/Leaves/Details.cshtml", leave);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("📋 Loading Create Leave form");

            await PopulateLeaveTypeDropdownAsync();

            // Get current employee ID
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/Create.cshtml");
            }

            // Calculate dynamic leave balances
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            _logger.LogInformation("✅ Create Leave form loaded successfully");
            return View("~/Views/Employee/Leaves/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(LeaveModel leave, IFormFile? MedicalCertificate, string DayType = "full")
        {
            _logger.LogInformation("🚀 CREATE LEAVE POST started");
            _logger.LogInformation($"📝 Received leave data: Type={leave.LeaveTypeID}, Start={leave.StartDate}, End={leave.EndDate}, DayType={DayType}");

            try
            {
                // Get current employee ID FIRST
                var employeeId = await GetCurrentEmployeeId();
                if (string.IsNullOrEmpty(employeeId))
                {
                    _logger.LogError("❌ Employee ID not found");
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateLeaveTypeDropdownAsync();
                    return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                }

                // Set EmployeeID and clear any validation error for it
                leave.EmployeeID = employeeId;
                ModelState.Remove("EmployeeID");
                ModelState.Remove("Employee");

                // 🚨 NEW: Handle Emergency Leave - Convert to Annual Leave in database
                bool isEmergencyLeave = false;
                if (leave.LeaveTypeID == 4) // Emergency Leave from UI
                {
                    isEmergencyLeave = true;
                    // Find Annual Leave type ID
                    var annualLeaveType = await _context.LeaveTypes
                        .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

                    if (annualLeaveType != null)
                    {
                        leave.LeaveTypeID = annualLeaveType.LeaveTypeID; // Convert to Annual Leave
                        _logger.LogInformation($"📝 Emergency Leave converted to Annual Leave (ID: {leave.LeaveTypeID})");
                    }
                    else
                    {
                        ModelState.AddModelError("LeaveTypeID", "Annual Leave type not found. Cannot process Emergency Leave.");
                        await PopulateLeaveTypeDropdownAsync();
                        return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                    }
                }

                // Calculate LeaveDays BEFORE validation
                var baseDays = (leave.EndDate.DayNumber - leave.StartDate.DayNumber) + 1;
                if (DayType == "half")
                {
                    if (baseDays == 1)
                    {
                        leave.LeaveDays = 0.5;
                    }
                    else
                    {
                        leave.LeaveDays = baseDays - 0.5;
                    }
                }
                else
                {
                    leave.LeaveDays = baseDays;
                }

                _logger.LogInformation($"📝 Calculated LeaveDays: {leave.LeaveDays}");

                // Clear validation errors for auto-set fields
                ModelState.Remove("Status");
                ModelState.Remove("CreatedDate");
                ModelState.Remove("SubmissionDate");
                ModelState.Remove("ApprovalDate");
                ModelState.Remove("ApprovedBy");
                ModelState.Remove("ApprovalRemarks");
                ModelState.Remove("LeaveDays");

                _logger.LogInformation($"📝 Model State Valid: {ModelState.IsValid}");

                // Validate leave type exists in database (now should be valid since we converted Emergency to Annual)
                if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
                {
                    ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
                    _logger.LogWarning($"❌ Invalid leave type: {leave.LeaveTypeID}");
                }

                // Validate medical certificate for medical leave
                var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
                if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical"))
                {
                    if (MedicalCertificate == null || MedicalCertificate.Length == 0)
                    {
                        ModelState.AddModelError("MedicalCertificate", "Medical certificate is required for medical leave.");
                        _logger.LogWarning("❌ Medical certificate missing for medical leave");
                    }
                    else
                    {
                        // Validate file type and size
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var fileExtension = Path.GetExtension(MedicalCertificate.FileName).ToLower();

                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError("MedicalCertificate", "Invalid file type. Please upload PDF, JPG, PNG, DOC, or DOCX files only.");
                        }

                        if (MedicalCertificate.Length > 5 * 1024 * 1024) // 5MB limit
                        {
                            ModelState.AddModelError("MedicalCertificate", "File size exceeds 5MB limit.");
                        }
                    }
                }

                // 🚨 NEW: Validate leave balance - for Emergency Leave, check Annual Leave balance
                var currentYear = DateTime.Now.Year;
                var requestedDays = leave.LeaveDays;

                var hasBalance = await HasSufficientLeaveBalanceAsync(employeeId, leave.LeaveTypeID, requestedDays, currentYear);

                if (!hasBalance)
                {
                    var balance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear);
                    var balanceTypeName = isEmergencyLeave ? "Annual Leave (used by Emergency Leave)" : leaveType?.TypeName ?? "Selected Leave Type";

                    ModelState.AddModelError("", $"Insufficient {balanceTypeName} balance. You have {balance:0.#} days remaining but requested {requestedDays:0.#} days.");
                }

                // 🚨 NEW: Validate backdate restrictions (Emergency Leave allows backdating)
                if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
                }

                if (ModelState.IsValid)
                {
                    // Set default values
                    leave.Status = "Pending";
                    leave.CreatedDate = DateTime.Now;
                    leave.SubmissionDate = DateTime.Now;

                    // 🚨 NEW: Add emergency leave remark to description
                    if (isEmergencyLeave)
                    {
                        var originalDescription = string.IsNullOrEmpty(leave.Description) ? "" : leave.Description;
                        leave.Description = $"[EMERGENCY LEAVE] {originalDescription}".Trim();
                        _logger.LogInformation("📝 Added Emergency Leave remark to description");
                    }

                    // Save the leave first
                    _context.Leaves.Add(leave);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Leave saved successfully with ID: {leave.LeaveID}");

                    if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                        MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        try
                        {
                            // Create uploads directory if it doesn't exist
                            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "medical-certificates");
                            if (!Directory.Exists(uploadsPath))
                            {
                                Directory.CreateDirectory(uploadsPath);
                            }

                            // Generate unique filename
                            var fileName = $"{leave.LeaveID}_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(MedicalCertificate.FileName)}";
                            var filePath = Path.Combine(uploadsPath, fileName);

                            // Save the file
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await MedicalCertificate.CopyToAsync(stream);
                            }

                            // Create LeaveDetails record
                            var leaveDetails = new LeaveDetailsModel
                            {
                                LeaveID = leave.LeaveID,
                                LeaveTypeID = leave.LeaveTypeID,
                                Comment = "Medical certificate uploaded",
                                DocumentPath = $"/uploads/medical-certificates/{fileName}",
                                UploadDate = DateTime.Now
                            };

                            _context.LeaveDetails.Add(leaveDetails);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation($"✅ Medical certificate saved: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "💥 Error uploading medical certificate during leave creation");
                            // Don't fail the leave creation, but log the error
                            TempData["Warning"] = "Leave created successfully, but there was an issue uploading the medical certificate.";
                        }
                    }

                    TempData["Success"] = isEmergencyLeave ?
                        "Emergency leave application submitted successfully! (Applied against Annual Leave balance)" :
                        "Leave application submitted successfully!";

                    return RedirectToAction(nameof(LeaveRecords));
                }
                else
                {
                    _logger.LogWarning("❌ Model validation failed - displaying form with errors");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error while creating leave");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            _logger.LogInformation("🔄 Redisplaying form due to errors");
            await PopulateLeaveTypeDropdownAsync();

            // Recalculate leave balances for form redisplay
            if (!string.IsNullOrEmpty(leave.EmployeeID))
            {
                var leaveBalances = await CalculateLeaveBalancesAsync(leave.EmployeeID);
                ViewBag.LeaveBalances = leaveBalances;
                PopulateLeaveBalanceViewBag(leaveBalances);
            }

            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int? id)
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

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave == null)
            {
                return NotFound();
            }

            // Only allow editing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "You can only edit leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            // 🚨 NEW: Check if this is an Emergency Leave (stored as Annual Leave with special description)
            if (leave.LeaveType.TypeName.ToLower().Contains("annual") &&
                !string.IsNullOrEmpty(leave.Description) &&
                leave.Description.Contains("[EMERGENCY LEAVE]"))
            {
                // Display as Emergency Leave in the form
                leave.LeaveTypeID = 4;
                // Clean the description for display
                leave.Description = leave.Description.Replace("[EMERGENCY LEAVE]", "").Trim();
            }

            // Populate leave types dropdown
            await PopulateLeaveTypeDropdownAsync();

            // Calculate leave balances for the view
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            // Check if this leave has a medical certificate
            var leaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveDetails != null)
            {
                ViewBag.HasMedicalCertificate = true;
                ViewBag.MedicalCertificateFileName = Path.GetFileName(leaveDetails.DocumentPath);
                ViewBag.MedicalCertificateUrl = leaveDetails.DocumentPath;
            }
            else
            {
                ViewBag.HasMedicalCertificate = false;
                ViewBag.MedicalCertificateFileName = "";
                ViewBag.MedicalCertificateUrl = "";
            }

            return View("~/Views/Employee/Leaves/Edit.cshtml", leave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Edit(int id, LeaveModel leave, IFormFile? MedicalCertificate, string DayType = "full")
        {
            if (id != leave.LeaveID)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var leaveToUpdate = await _context.Leaves.FindAsync(id);
            if (leaveToUpdate == null || leaveToUpdate.EmployeeID != employeeId)
                return NotFound();

            // Only allow editing if status is Pending
            if (leaveToUpdate.Status != "Pending")
            {
                TempData["Error"] = "You can only edit leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            // 🚨 NEW: Handle Emergency Leave - Convert to Annual Leave in database
            bool isEmergencyLeave = false;
            if (leave.LeaveTypeID == 4) // Emergency Leave from UI
            {
                isEmergencyLeave = true;
                // Find Annual Leave type ID
                var annualLeaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

                if (annualLeaveType != null)
                {
                    leave.LeaveTypeID = annualLeaveType.LeaveTypeID; // Convert to Annual Leave
                    _logger.LogInformation($"📝 Emergency Leave converted to Annual Leave (ID: {leave.LeaveTypeID})");
                }
                else
                {
                    ModelState.AddModelError("LeaveTypeID", "Annual Leave type not found. Cannot process Emergency Leave.");
                    await PopulateLeaveTypeDropdownAsync();
                    var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                    ViewBag.LeaveBalances = leaveBalances;
                    PopulateLeaveBalanceViewBag(leaveBalances);
                    return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
                }
            }

            // Calculate LeaveDays with half-day support
            var baseDays = (leave.EndDate.DayNumber - leave.StartDate.DayNumber) + 1;
            if (DayType == "half")
            {
                if (baseDays == 1)
                {
                    leave.LeaveDays = 0.5; // Single day half = 0.5
                }
                else
                {
                    leave.LeaveDays = baseDays - 0.5; // Multi-day with half = total - 0.5
                }
            }
            else
            {
                leave.LeaveDays = baseDays; // Full day
            }

            // Validate leave type exists in database (now should be valid since we converted Emergency to Annual)
            if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
            {
                ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
            }

            // Check if medical leave and validate medical certificate
            var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
            var existingLeaveDetails = await _context.LeaveDetails.FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical"))
            {
                // If no existing medical certificate and no new one uploaded
                if (existingLeaveDetails == null && (MedicalCertificate == null || MedicalCertificate.Length == 0))
                {
                    ModelState.AddModelError("MedicalCertificate", "Medical certificate is required for medical leave.");
                }
                else if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                {
                    // Validate new uploaded file
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                    var fileExtension = Path.GetExtension(MedicalCertificate.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("MedicalCertificate", "Invalid file type. Please upload PDF, JPG, PNG, DOC, or DOCX files only.");
                    }

                    if (MedicalCertificate.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        ModelState.AddModelError("MedicalCertificate", "File size exceeds 5MB limit.");
                    }
                }
            }

            // Validate leave balance for updated leave
            var currentYear = DateTime.Now.Year;
            var requestedDays = leave.LeaveDays;

            // Calculate balance excluding the current leave being edited
            var hasBalance = await HasSufficientLeaveBalanceAsync(employeeId, leave.LeaveTypeID, requestedDays, currentYear, id);

            if (!hasBalance)
            {
                var balance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear, id);
                var balanceTypeName = isEmergencyLeave ? "Annual Leave (used by Emergency Leave)" : leaveType?.TypeName ?? "Selected Leave Type";

                ModelState.AddModelError("", $"Insufficient {balanceTypeName} balance. You have {balance:0.#} days remaining but requested {requestedDays:0.#} days.");
            }

            // NEW: Validate backdate restrictions (Emergency Leave allows backdating)
            if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update the leave properties
                    leaveToUpdate.LeaveTypeID = leave.LeaveTypeID;
                    leaveToUpdate.StartDate = leave.StartDate;
                    leaveToUpdate.EndDate = leave.EndDate;
                    leaveToUpdate.LeaveDays = leave.LeaveDays;

                    // 🚨 NEW: Handle Emergency Leave description
                    if (isEmergencyLeave)
                    {
                        var originalDescription = string.IsNullOrEmpty(leave.Description) ? "" : leave.Description;
                        // Remove existing [EMERGENCY LEAVE] tag if present
                        var cleanDescription = originalDescription.Replace("[EMERGENCY LEAVE]", "").Trim();
                        leaveToUpdate.Description = $"[EMERGENCY LEAVE] {cleanDescription}".Trim();
                        _logger.LogInformation("📝 Updated Emergency Leave remark in description");
                    }
                    else
                    {
                        leaveToUpdate.Description = leave.Description;
                    }

                    // Handle medical certificate upload if provided
                    if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        try
                        {
                            // Create uploads directory if it doesn't exist
                            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "medical-certificates");
                            if (!Directory.Exists(uploadsPath))
                            {
                                Directory.CreateDirectory(uploadsPath);
                            }

                            // Generate unique filename
                            var fileName = $"{leave.LeaveID}_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(MedicalCertificate.FileName)}";
                            var filePath = Path.Combine(uploadsPath, fileName);

                            // Save the file
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await MedicalCertificate.CopyToAsync(stream);
                            }

                            // Update or create LeaveDetails record
                            if (existingLeaveDetails != null)
                            {
                                // Delete old file if it exists
                                var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingLeaveDetails.DocumentPath.TrimStart('/'));
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }

                                // Update existing record
                                existingLeaveDetails.DocumentPath = $"/uploads/medical-certificates/{fileName}";
                                existingLeaveDetails.Comment = "Medical certificate updated";
                                existingLeaveDetails.LeaveTypeID = leave.LeaveTypeID;
                            }
                            else
                            {
                                // Create new record
                                var leaveDetails = new LeaveDetailsModel
                                {
                                    LeaveID = leave.LeaveID,
                                    LeaveTypeID = leave.LeaveTypeID,
                                    Comment = "Medical certificate uploaded",
                                    DocumentPath = $"/uploads/medical-certificates/{fileName}"
                                };
                                _context.LeaveDetails.Add(leaveDetails);
                            }

                            _logger.LogInformation($"✅ Medical certificate updated: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "💥 Error uploading medical certificate");
                            ModelState.AddModelError("", "Error uploading medical certificate. Please try again.");
                            await PopulateLeaveTypeDropdownAsync();

                            // Recalculate leave balances
                            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                            ViewBag.LeaveBalances = leaveBalances;
                            PopulateLeaveBalanceViewBag(leaveBalances);

                            return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = isEmergencyLeave ?
                        "Emergency leave updated successfully! (Applied against Annual Leave balance)" :
                        "Leave updated successfully!";

                    return RedirectToAction(nameof(LeaveRecords));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveExists(leaveToUpdate.LeaveID))
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
                    _logger.LogError(ex, "Database error while updating leave");
                    ModelState.AddModelError("", "An error occurred while updating the leave.");
                }
            }

            await PopulateLeaveTypeDropdownAsync();

            // Recalculate leave balances
            var currentLeaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = currentLeaveBalances;
            PopulateLeaveBalanceViewBag(currentLeaveBalances);

            return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave == null)
                return NotFound();

            // Only allow deletion if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "You can only delete leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            return View("~/Views/Employee/Leaves/Delete.cshtml", leave);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return RedirectToAction(nameof(LeaveRecords));

            var leave = await _context.Leaves.FindAsync(id);

            if (leave != null && leave.EmployeeID == employeeId)
            {
                // Only allow deletion if status is Pending
                if (leave.Status != "Pending")
                {
                    TempData["Error"] = "You can only delete leaves that are in Pending status.";
                    return RedirectToAction(nameof(LeaveRecords));
                }

                _context.Leaves.Remove(leave);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Leave deleted successfully!";
            }

            return RedirectToAction(nameof(LeaveRecords));
        }

        // ================== HR ACTIONS ==================

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveIndex()
        {
            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/LeaveIndex.cshtml", leaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            // ADD THIS: Check if this leave has a medical certificate or other documents
            var leaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveDetails != null)
            {
                ViewBag.HasMedicalCertificate = true;
                ViewBag.MedicalCertificateFileName = Path.GetFileName(leaveDetails.DocumentPath);
                ViewBag.MedicalCertificateUrl = leaveDetails.DocumentPath;
                ViewBag.MedicalCertificateUploadDate = leaveDetails.UploadDate.ToString("dd/MM/yyyy HH:mm") ?? "Unknown";
                ViewBag.DocumentComment = leaveDetails.Comment;
            }
            else
            {
                ViewBag.HasMedicalCertificate = false;
                ViewBag.MedicalCertificateFileName = "";
                ViewBag.MedicalCertificateUrl = "";
                ViewBag.MedicalCertificateUploadDate = "";
                ViewBag.DocumentComment = "";
            }

            return View("~/Views/HR/Leaves/LeaveDetails.cshtml", leave);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> DownloadMedicalCertificate(int leaveId)
        {
            try
            {
                var leaveDetails = await _context.LeaveDetails
                    .FirstOrDefaultAsync(ld => ld.LeaveID == leaveId);

                if (leaveDetails == null)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
                }

                // Construct the full file path
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", leaveDetails.DocumentPath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    TempData["Error"] = "Document file not found on server.";
                    return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
                }

                var fileName = Path.GetFileName(leaveDetails.DocumentPath);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var contentType = GetContentType(fileName);

                _logger.LogInformation($"✅ HR downloading medical certificate: {fileName} for Leave ID: {leaveId}");

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error downloading medical certificate for Leave ID: {leaveId}");
                TempData["Error"] = "Error downloading document. Please try again.";
                return RedirectToAction(nameof(LeaveDetails), new { id = leaveId });
            }
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessLeave(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            // Only allow processing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            // ADD THIS: Check if this leave has a medical certificate
            var leaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveDetails != null)
            {
                ViewBag.HasMedicalCertificate = true;
                ViewBag.MedicalCertificateFileName = Path.GetFileName(leaveDetails.DocumentPath);
                ViewBag.MedicalCertificateUrl = leaveDetails.DocumentPath;
                ViewBag.MedicalCertificateUploadDate = leaveDetails.UploadDate.ToString("dd/MM/yyyy HH:mm") ?? "Unknown";
                ViewBag.DocumentComment = leaveDetails.Comment;
            }
            else
            {
                ViewBag.HasMedicalCertificate = false;
                ViewBag.MedicalCertificateFileName = "";
                ViewBag.MedicalCertificateUrl = "";
                ViewBag.MedicalCertificateUploadDate = "";
                ViewBag.DocumentComment = "";
            }

            return View("~/Views/HR/Leaves/ProcessLeave.cshtml", leave);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessLeave(int id, string action, string? remarks)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.LeaveID == id);

            if (leave == null)
            {
                return NotFound();
            }

            // Only allow processing if status is Pending
            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Update leave based on action
                if (action == "approve")
                {
                    leave.Status = "Approved";
                    leave.ApprovalDate = DateTime.Now;
                    leave.ApprovedBy = currentUser.Id;
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave approved successfully!";
                }
                else if (action == "reject")
                {
                    leave.Status = "Rejected";
                    leave.ApprovalDate = DateTime.Now;
                    leave.ApprovedBy = currentUser.Id;
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave rejected successfully!";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessLeave), new { id = id });
                }

                await _context.SaveChangesAsync();

                // TODO: Send email notification to employee
                // await SendEmailNotification(leave);

                return RedirectToAction(nameof(LeaveIndex));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while processing leave");
                TempData["Error"] = "An error occurred while processing the leave. Please try again.";
                return RedirectToAction(nameof(ProcessLeave), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing leave");
                TempData["Error"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction(nameof(ProcessLeave), new { id = id });
            }
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> PendingLeaves()
        {
            var pendingLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Pending")
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/PendingLeave.cshtml", pendingLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ApprovedLeaves()
        {
            var approvedLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Approved")
                .OrderByDescending(l => l.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/ApprovedLeave.cshtml", approvedLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> RejectedLeaves()
        {
            var rejectedLeaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Rejected")
                .OrderByDescending(l => l.ApprovalDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/RejectedLeave.cshtml", rejectedLeaves);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> EmployeeLeaveBalance(string? employeeId = null)
        {
            if (!string.IsNullOrEmpty(employeeId))
            {
                // Get specific employee details
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == employeeId);

                if (employee == null)
                {
                    return NotFound();
                }

                // Use the same method as Dashboard and LeaveRecords
                var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);

                // Get all leave applications for this employee
                var employeeLeaveApplications = await _context.Leaves
                    .Include(l => l.LeaveType)
                    .Include(l => l.Employee)
                    .Where(l => l.EmployeeID == employeeId)
                    .OrderByDescending(l => l.SubmissionDate)
                    .ToListAsync();

                ViewBag.SelectedEmployee = employee;
                ViewBag.LeaveBalances = leaveBalances;
                ViewBag.EmployeeLeaveApplications = employeeLeaveApplications;

                return View("~/Views/HR/Leaves/EmployeeLeaveBalance.cshtml");
            }
            else
            {
                // Show all employees - use the same method for consistency
                var employees = await _context.Employees.ToListAsync();
                var allEmployeeBalances = new Dictionary<string, Dictionary<string, object>>();

                foreach (var emp in employees)
                {
                    var balances = await CalculateLeaveBalancesAsync(emp.EmployeeID);
                    allEmployeeBalances[emp.EmployeeID] = balances;
                }

                ViewBag.Employees = employees;
                ViewBag.AllEmployeeBalances = allEmployeeBalances;

                return View("~/Views/HR/Leaves/EmployeeLeaveBalance.cshtml");
            }
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> LeaveReports()
        {
            var currentYear = DateTime.Now.Year;

            // Get leave statistics
            var leaveStats = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.StartDate.Year == currentYear)
                .GroupBy(l => l.LeaveType.TypeName)
                .Select(g => new
                {
                    LeaveType = g.Key,
                    TotalApplications = g.Count(),
                    ApprovedApplications = g.Count(l => l.Status == "Approved"),
                    PendingApplications = g.Count(l => l.Status == "Pending"),
                    RejectedApplications = g.Count(l => l.Status == "Rejected"),
                    TotalDaysUsed = g.Where(l => l.Status == "Approved").Sum(l => l.LeaveDays)
                })
                .ToListAsync();

            // Get employees with high leave usage (approaching limits)
            var allEmployees = await _context.Employees.ToListAsync();
            var employeesNearLimit = new List<object>();

            foreach (var employee in allEmployees)
            {
                var balances = await CalculateLeaveBalancesAsync(employee.EmployeeID);
                foreach (var balance in balances)
                {
                    var balanceInfo = balance.Value as dynamic;
                    if (balanceInfo != null && balanceInfo.RemainingDays <= 5)
                    {
                        employeesNearLimit.Add(new
                        {
                            EmployeeID = employee.EmployeeID,
                            EmployeeName = employee.Username,
                            LeaveType = balance.Key,
                            RemainingDays = balanceInfo.RemainingDays
                        });
                    }
                }
            }

            ViewBag.LeaveStats = leaveStats;
            ViewBag.EmployeesNearLimit = employeesNearLimit;
            ViewBag.CurrentYear = currentYear;

            return View("~/Views/HR/Leaves/LeaveReports.cshtml");
        }

        // ================== HELPER METHODS (UPDATED FOR DOUBLE LEAVE DAYS) ==================

        private void PopulateLeaveBalanceViewBag(Dictionary<string, object> leaveBalances)
        {
            _logger.LogInformation("🔧 Populating individual ViewBag properties for leave balances");

            try
            {
                if (leaveBalances.ContainsKey("Annual Leave"))
                {
                    var annualLeave = leaveBalances["Annual Leave"] as dynamic;
                    ViewBag.AnnualLeaveBalance = annualLeave?.RemainingDays ?? 14.0;
                }
                else
                {
                    ViewBag.AnnualLeaveBalance = 14.0;
                }

                if (leaveBalances.ContainsKey("Medical Leave"))
                {
                    var medicalLeave = leaveBalances["Medical Leave"] as dynamic;
                    ViewBag.MedicalLeaveBalance = medicalLeave?.RemainingDays ?? 10.0;
                }
                else
                {
                    ViewBag.MedicalLeaveBalance = 10.0;
                }

                if (leaveBalances.ContainsKey("Hospitalization Leave"))
                {
                    var hospitalizationLeave = leaveBalances["Hospitalization Leave"] as dynamic;
                    ViewBag.HospitalizationLeaveBalance = hospitalizationLeave?.RemainingDays ?? 16.0;
                }
                else
                {
                    ViewBag.HospitalizationLeaveBalance = 16.0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error populating individual ViewBag properties");
                ViewBag.AnnualLeaveBalance = 14.0;
                ViewBag.MedicalLeaveBalance = 10.0;
                ViewBag.HospitalizationLeaveBalance = 16.0;
            }
        }

        private async Task<Dictionary<string, object>> CalculateLeaveBalancesAsync(string employeeId, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            _logger.LogInformation($"🧮 Calculating leave balances for employee {employeeId} for year {year}");

            var leaveBalances = new Dictionary<string, object>();

            try
            {
                var leaveTypes = await _context.LeaveTypes.ToListAsync();

                // ✅ FIRST: Get Annual Leave balance for Emergency Leave reference
                var annualLeaveType = leaveTypes.FirstOrDefault(lt => lt.TypeName.ToLower().Contains("annual"));
                double annualLeaveUsed = 0;
                double annualLeaveRemaining = 0;

                if (annualLeaveType != null)
                {
                    // Get all leaves that use Annual Leave balance (Annual + Emergency)
                    var annualLeaveApplications = await _context.Leaves
                        .Where(l => l.EmployeeID == employeeId
                                && (l.LeaveTypeID == annualLeaveType.LeaveTypeID ||
                                    _context.LeaveTypes.Any(lt => lt.LeaveTypeID == l.LeaveTypeID && lt.TypeName.ToLower().Contains("emergency")))
                                && l.StartDate.Year == year
                                && l.Status == "Approved")
                        .ToListAsync();

                    foreach (var leave in annualLeaveApplications)
                    {
                        annualLeaveUsed += leave.LeaveDays;
                        _logger.LogInformation($"📊 Annual/Emergency leave used: {leave.LeaveDays} days (Total: {annualLeaveUsed})");
                    }

                    annualLeaveRemaining = annualLeaveType.DefaultDaysPerYear - annualLeaveUsed;

                    _logger.LogInformation($"📋 Annual Leave Summary: Default={annualLeaveType.DefaultDaysPerYear}, Used={annualLeaveUsed}, Remaining={annualLeaveRemaining}");
                }

                foreach (var leaveType in leaveTypes)
                {
                    _logger.LogInformation($"🔍 Processing leave type: {leaveType.TypeName} (ID: {leaveType.LeaveTypeID})");

                    double usedDays = 0;
                    double remainingDays = 0;

                    // ✅ SPECIAL HANDLING FOR EMERGENCY LEAVE
                    if (leaveType.TypeName.ToLower().Contains("emergency"))
                    {
                        // Emergency Leave uses Annual Leave balance
                        var emergencyLeaves = await _context.Leaves
                            .Where(l => l.EmployeeID == employeeId
                                    && l.LeaveTypeID == leaveType.LeaveTypeID
                                    && l.StartDate.Year == year
                                    && l.Status == "Approved")
                            .ToListAsync();

                        foreach (var leave in emergencyLeaves)
                        {
                            usedDays += leave.LeaveDays;
                        }

                        // Use Annual Leave remaining balance for Emergency Leave
                        remainingDays = Math.Max(0, annualLeaveRemaining);

                        _logger.LogInformation($"📋 EMERGENCY LEAVE CALCULATION:");
                        _logger.LogInformation($"   - Emergency Leave Used: {usedDays}");
                        _logger.LogInformation($"   - Available (from Annual): {remainingDays}");

                        leaveBalances[leaveType.TypeName] = new
                        {
                            LeaveTypeID = leaveType.LeaveTypeID,
                            TypeName = leaveType.TypeName,
                            DefaultDays = annualLeaveType?.DefaultDaysPerYear ?? 14, // Show Annual Leave default
                            UsedDays = usedDays,
                            PendingDays = 0, // You can calculate pending if needed
                            RemainingDays = remainingDays
                        };
                    }
                    else
                    {
                        // ✅ NORMAL PROCESSING FOR OTHER LEAVE TYPES
                        var approvedLeaves = await _context.Leaves
                            .Where(l => l.EmployeeID == employeeId
                                    && l.LeaveTypeID == leaveType.LeaveTypeID
                                    && l.StartDate.Year == year
                                    && l.Status == "Approved")
                            .ToListAsync();

                        var pendingLeaves = await _context.Leaves
                            .Where(l => l.EmployeeID == employeeId
                                    && l.LeaveTypeID == leaveType.LeaveTypeID
                                    && l.StartDate.Year == year
                                    && l.Status == "Pending")
                            .ToListAsync();

                        foreach (var leave in approvedLeaves)
                        {
                            usedDays += leave.LeaveDays;
                        }

                        double pendingDays = 0;
                        foreach (var leave in pendingLeaves)
                        {
                            pendingDays += leave.LeaveDays;
                        }

                        // ✅ FOR ANNUAL LEAVE: Use the pre-calculated remaining balance
                        if (leaveType.TypeName.ToLower().Contains("annual"))
                        {
                            remainingDays = Math.Max(0, annualLeaveRemaining);
                        }
                        else
                        {
                            remainingDays = Math.Max(0, leaveType.DefaultDaysPerYear - usedDays);
                        }

                        _logger.LogInformation($"📋 CALCULATION for {leaveType.TypeName}:");
                        _logger.LogInformation($"   - Default Days: {leaveType.DefaultDaysPerYear}");
                        _logger.LogInformation($"   - Used Days: {usedDays}");
                        _logger.LogInformation($"   - Pending Days: {pendingDays}");
                        _logger.LogInformation($"   - Remaining Days: {remainingDays}");

                        leaveBalances[leaveType.TypeName] = new
                        {
                            LeaveTypeID = leaveType.LeaveTypeID,
                            TypeName = leaveType.TypeName,
                            DefaultDays = leaveType.DefaultDaysPerYear,
                            UsedDays = usedDays,
                            PendingDays = pendingDays,
                            RemainingDays = remainingDays
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating leave balances");
            }

            return leaveBalances;
        }

        private async Task<bool> HasSufficientLeaveBalanceForEmergencyAsync(string employeeId, double requestedDays, int year = 0, int? excludeLeaveId = null)
        {
            if (year == 0) year = DateTime.Now.Year;

            // Find Annual Leave type
            var annualLeaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

            if (annualLeaveType == null) return false;

            // Get all leaves that use Annual Leave balance (both Annual and Emergency)
            var query = _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending")
                        && (l.LeaveTypeID == annualLeaveType.LeaveTypeID ||
                            _context.LeaveTypes.Any(lt => lt.LeaveTypeID == l.LeaveTypeID && lt.TypeName.ToLower().Contains("emergency"))));

            // Exclude current leave being edited
            if (excludeLeaveId.HasValue)
            {
                query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
            }

            var leaves = await query.ToListAsync();

            double usedDays = 0;
            foreach (var leave in leaves)
            {
                usedDays += leave.LeaveDays;
            }

            var remainingDays = annualLeaveType.DefaultDaysPerYear - usedDays;
            return remainingDays >= requestedDays;
        }

        private async Task<bool> HasSufficientLeaveBalanceAsync(string employeeId, int leaveTypeId, double requestedDays, int year = 0, int? excludeLeaveId = null)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return false;

            // ✅ SPECIAL HANDLING FOR EMERGENCY LEAVE
            if (leaveType.TypeName.ToLower().Contains("emergency"))
            {
                return await HasSufficientLeaveBalanceForEmergencyAsync(employeeId, requestedDays, year, excludeLeaveId);
            }

            // ✅ SPECIAL HANDLING FOR ANNUAL LEAVE (also consider Emergency Leave usage)
            if (leaveType.TypeName.ToLower().Contains("annual"))
            {
                return await HasSufficientLeaveBalanceForEmergencyAsync(employeeId, requestedDays, year, excludeLeaveId);
            }

            // ✅ NORMAL PROCESSING FOR OTHER LEAVE TYPES
            var query = _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"));

            // Exclude current leave being edited
            if (excludeLeaveId.HasValue)
            {
                query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
            }

            var leaves = await query.ToListAsync();

            double usedDays = 0;
            foreach (var leave in leaves)
            {
                usedDays += leave.LeaveDays;
            }

            var remainingDays = leaveType.DefaultDaysPerYear - usedDays;
            return remainingDays >= requestedDays;
        }

        private async Task<double> GetRemainingLeaveBalanceAsync(string employeeId, int leaveTypeId, int year = 0, int? excludeLeaveId = null)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null) return 0;

            // ✅ SPECIAL HANDLING FOR EMERGENCY LEAVE
            if (leaveType.TypeName.ToLower().Contains("emergency"))
            {
                // Find Annual Leave type
                var annualLeaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

                if (annualLeaveType == null) return 0;

                // Get all leaves that use Annual Leave balance (both Annual and Emergency)
                var query = _context.Leaves
                    .Where(l => l.EmployeeID == employeeId
                            && l.StartDate.Year == year
                            && (l.Status == "Approved" || l.Status == "Pending")
                            && (l.LeaveTypeID == annualLeaveType.LeaveTypeID ||
                                _context.LeaveTypes.Any(lt => lt.LeaveTypeID == l.LeaveTypeID && lt.TypeName.ToLower().Contains("emergency"))));

                // Exclude current leave being edited
                if (excludeLeaveId.HasValue)
                {
                    query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
                }

                var leaves = await query.ToListAsync();
                double usedDays = 0;
                foreach (var leave in leaves)
                {
                    usedDays += leave.LeaveDays;
                }

                var remainingDays = annualLeaveType.DefaultDaysPerYear - usedDays;
                return Math.Max(0, remainingDays);
            }

            // ✅ SPECIAL HANDLING FOR ANNUAL LEAVE
            if (leaveType.TypeName.ToLower().Contains("annual"))
            {
                // Get all leaves that use Annual Leave balance (both Annual and Emergency)
                var query = _context.Leaves
                    .Where(l => l.EmployeeID == employeeId
                            && l.StartDate.Year == year
                            && (l.Status == "Approved" || l.Status == "Pending")
                            && (l.LeaveTypeID == leaveTypeId ||
                                _context.LeaveTypes.Any(lt => lt.LeaveTypeID == l.LeaveTypeID && lt.TypeName.ToLower().Contains("emergency"))));

                // Exclude current leave being edited
                if (excludeLeaveId.HasValue)
                {
                    query = query.Where(l => l.LeaveID != excludeLeaveId.Value);
                }

                var leaves = await query.ToListAsync();
                double usedDays = 0;
                foreach (var leave in leaves)
                {
                    usedDays += leave.LeaveDays;
                }

                var remainingDays = leaveType.DefaultDaysPerYear - usedDays;
                return Math.Max(0, remainingDays);
            }

            // ✅ NORMAL PROCESSING FOR OTHER LEAVE TYPES
            var normalQuery = _context.Leaves
                .Where(l => l.EmployeeID == employeeId
                        && l.LeaveTypeID == leaveTypeId
                        && l.StartDate.Year == year
                        && (l.Status == "Approved" || l.Status == "Pending"));

            // Exclude current leave being edited
            if (excludeLeaveId.HasValue)
            {
                normalQuery = normalQuery.Where(l => l.LeaveID != excludeLeaveId.Value);
            }

            var normalLeaves = await normalQuery.ToListAsync();
            double normalUsedDays = 0;
            foreach (var leave in normalLeaves)
            {
                normalUsedDays += leave.LeaveDays;
            }

            var normalRemainingDays = leaveType.DefaultDaysPerYear - normalUsedDays;
            return Math.Max(0, normalRemainingDays);
        }

        private async Task<Dictionary<string, object>> GetEmployeeLeaveBalance(string employeeId)
        {
            var leaveTypes = await _context.LeaveTypes.ToListAsync();
            var leaveBalances = new Dictionary<string, object>();

            foreach (var leaveType in leaveTypes)
            {
                // Get default days for this leave type
                var defaultDays = leaveType.DefaultDaysPerYear;

                // Calculate used days - ONLY APPROVED leaves - Now returns double
                var usedDays = await _context.Leaves
                    .Where(l => l.EmployeeID == employeeId &&
                               l.LeaveTypeID == leaveType.LeaveTypeID &&
                               l.Status == "Approved" &&
                               l.StartDate.Year == DateTime.Now.Year) // Add year filter
                    .SumAsync(l => l.LeaveDays); // This will now return double

                var remainingDays = defaultDays - usedDays;

                leaveBalances[leaveType.TypeName] = new
                {
                    DefaultDays = defaultDays,
                    UsedDays = usedDays, // Now double
                    RemainingDays = Math.Max(0, remainingDays) // Now double
                };
            }

            return leaveBalances;
        }

        private double GetDefaultLeaveDays(string leaveType)
        {
            switch (leaveType)
            {
                case "Annual Leave":
                    return 14.0;
                case "Medical Leave":
                    return 10.0;
                case "Hospitalization Leave":
                    return 16.0;
                default:
                    return 0.0;
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }

        private async Task<bool> IsValidLeaveTypeAsync(int leaveTypeId)
        {
            return await _context.LeaveTypes.AnyAsync(lt => lt.LeaveTypeID == leaveTypeId);
        }

        private async Task PopulateLeaveTypeDropdownAsync()
        {
            try
            {
                var leaveTypes = await _context.LeaveTypes
                    .OrderBy(lt => lt.TypeName)
                    .ToListAsync();

                _logger.LogInformation($"📝 Found {leaveTypes.Count} leave types in database");

                if (!leaveTypes.Any())
                {
                    _logger.LogWarning("❌ No leave types found in database");
                    // Create fallback list
                    ViewBag.LeaveTypes = new SelectList(new[]
                    {
                new { LeaveTypeID = 1, TypeName = "Annual Leave" },
                new { LeaveTypeID = 2, TypeName = "Medical Leave" },
                new { LeaveTypeID = 3, TypeName = "Hospitalization Leave" },
                new { LeaveTypeID = 4, TypeName = "Emergency Leave" }
            }, "LeaveTypeID", "TypeName");
                }
                else
                {
                    // 🚨 NEW: Create a list that includes Emergency Leave
                    var dropdownItems = new List<dynamic>();

                    // Add existing database leave types
                    foreach (var lt in leaveTypes)
                    {
                        _logger.LogInformation($"📝 Leave Type: ID={lt.LeaveTypeID}, Name={lt.TypeName}");
                        dropdownItems.Add(new { LeaveTypeID = lt.LeaveTypeID, TypeName = lt.TypeName });
                    }

                    // 🚨 NEW: Add Emergency Leave (UI only - will be converted to Annual Leave)
                    dropdownItems.Add(new { LeaveTypeID = 4, TypeName = "Emergency Leave" });

                    // Sort by TypeName for better UX
                    var sortedItems = dropdownItems.OrderBy(x => x.TypeName).ToList();

                    ViewBag.LeaveTypes = new SelectList(sortedItems, "LeaveTypeID", "TypeName");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error populating leave types dropdown");
                // Fallback dropdown in case of error
                ViewBag.LeaveTypes = new SelectList(new[]
                {
            new { LeaveTypeID = 1, TypeName = "Annual Leave" },
            new { LeaveTypeID = 2, TypeName = "Medical Leave" },
            new { LeaveTypeID = 3, TypeName = "Hospitalization Leave" },
            new { LeaveTypeID = 4, TypeName = "Emergency Leave" }
        }, "LeaveTypeID", "TypeName");
            }
        }

        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.LeaveID == id);
        }

        
        private async Task<bool> IsBackdateAllowedAsync(int leaveTypeId)
        {
            try
            {
                var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
                if (leaveType == null) return false;

                var leaveTypeName = leaveType.TypeName.ToLower();

                
                return leaveTypeName.Contains("medical") ||
                       leaveTypeName.Contains("hospitalization") ||
                       leaveTypeName.Contains("emergency") ||
                       leaveTypeName.Contains("annual"); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking backdate permission for leave type {leaveTypeId}");
                return false; // Default to not allowing backdate on error
            }
        }

        
    }
}