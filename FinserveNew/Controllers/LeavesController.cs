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

            if (currentUser == null)
            {
                _logger.LogWarning("No authenticated user found");
                return null;
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
            var employeeId = await GetCurrentEmployeeId();

            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/LeaveRecords.cshtml", new List<LeaveModel>());
            }

            var leaves = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.EmployeeID == employeeId)
                .OrderByDescending(l => l.SubmissionDate != DateTime.MinValue ? l.SubmissionDate : l.CreatedDate)
                .ThenByDescending(l => l.LeaveID)
                .ToListAsync();

            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
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

            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/Create.cshtml");
            }

            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            _logger.LogInformation("✅ Create Leave form loaded successfully");
            return View("~/Views/Employee/Leaves/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(LeaveModel leave, IFormFile? MedicalCertificate, string DayType = "full",
    string? JustificationReason = null, bool ConfirmUnpaidLeave = false)
        {
            _logger.LogInformation("🚀 CREATE LEAVE POST started");
            _logger.LogInformation($"📝 Received leave data: Type={leave.LeaveTypeID}, Start={leave.StartDate}, End={leave.EndDate}, DayType={DayType}");

            // ===== CRITICAL DEBUG SECTION =====
            _logger.LogInformation($"🔍 DEBUG CHECKPOINT 1 - Form Parameters:");
            _logger.LogInformation($"   ConfirmUnpaidLeave: {ConfirmUnpaidLeave}");
            _logger.LogInformation($"   JustificationReason: '{JustificationReason ?? "NULL"}'");
            _logger.LogInformation($"   JustificationReason IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(JustificationReason)}");
            _logger.LogInformation($"   JustificationReason Length: {JustificationReason?.Length ?? 0}");

            // ✅ DECLARE employeeId AT METHOD LEVEL
            string employeeId = null;

            try
            {
                _logger.LogInformation("🔍 DEBUG CHECKPOINT 2 - Starting try block");

                // Get current employee ID FIRST
                employeeId = await GetCurrentEmployeeId();
                if (string.IsNullOrEmpty(employeeId))
                {
                    _logger.LogError("❌ Employee ID not found");
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateLeaveTypeDropdownAsync();
                    return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                }

                _logger.LogInformation($"🔍 DEBUG CHECKPOINT 3 - Employee ID found: {employeeId}");

                // Set EmployeeID and clear validation errors
                leave.EmployeeID = employeeId;
                ModelState.Remove("EmployeeID");
                ModelState.Remove("Employee");

                // Handle Emergency Leave - Convert to Annual Leave in database
                bool isEmergencyLeave = false;
                if (leave.LeaveTypeID == 4) // Emergency Leave from UI
                {
                    _logger.LogInformation("🔍 DEBUG CHECKPOINT 4 - Processing Emergency Leave");
                    isEmergencyLeave = true;
                    var annualLeaveType = await _context.LeaveTypes
                        .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

                    if (annualLeaveType != null)
                    {
                        leave.LeaveTypeID = annualLeaveType.LeaveTypeID;
                        _logger.LogInformation($"📝 Emergency Leave converted to Annual Leave (ID: {leave.LeaveTypeID})");
                    }
                    else
                    {
                        _logger.LogError("❌ Annual Leave type not found");
                        ModelState.AddModelError("LeaveTypeID", "Annual Leave type not found. Cannot process Emergency Leave.");
                        await PopulateLeaveTypeDropdownAsync();
                        return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                    }
                }

                _logger.LogInformation($"🔍 DEBUG CHECKPOINT 5 - Emergency Leave Status: {isEmergencyLeave}");

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

                _logger.LogInformation($"🔍 DEBUG CHECKPOINT 6 - Calculated LeaveDays: {leave.LeaveDays}");

                // Clear validation errors for auto-set fields
                ModelState.Remove("Status");
                ModelState.Remove("CreatedDate");
                ModelState.Remove("SubmissionDate");
                ModelState.Remove("ApprovalDate");
                ModelState.Remove("ApprovedBy");
                ModelState.Remove("ApprovalRemarks");
                ModelState.Remove("LeaveDays");

                // Validate leave type exists in database
                if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
                {
                    _logger.LogError($"❌ Invalid leave type: {leave.LeaveTypeID}");
                    ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
                }

                _logger.LogInformation("🔍 DEBUG CHECKPOINT 7 - Leave type validation completed");

                // Validate medical certificate for medical leave
                var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
                if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical"))
                {
                    _logger.LogInformation("🔍 DEBUG CHECKPOINT 8 - Processing Medical Leave");
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

                _logger.LogInformation("🔍 DEBUG CHECKPOINT 9 - Medical certificate validation completed");

                // Check leave balance and determine if unpaid leave is needed
                var currentYear = DateTime.Now.Year;
                var requestedDays = leave.LeaveDays;
                var remainingBalance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear);

                bool willExceedBalance = requestedDays > remainingBalance;
                double excessDays = Math.Max(0, requestedDays - remainingBalance);

                _logger.LogInformation($"🔍 DEBUG CHECKPOINT 10 - Balance Check:");
                _logger.LogInformation($"   Requested: {requestedDays}");
                _logger.LogInformation($"   Available: {remainingBalance}");
                _logger.LogInformation($"   Excess: {excessDays}");
                _logger.LogInformation($"   Will Exceed Balance: {willExceedBalance}");

                // ===== SIMPLIFIED UNPAID LEAVE HANDLING =====
                if (willExceedBalance)
                {
                    _logger.LogInformation("🔍 DEBUG CHECKPOINT 11 - Balance exceeded, checking if this is an unpaid leave request");

                    // Check if this is coming from the frontend with unpaid leave confirmation
                    var isUnpaidLeaveRequest = Request.Form["IsUnpaidLeaveRequest"].ToString() == "true";

                    _logger.LogInformation($"   IsUnpaidLeaveRequest from form: {isUnpaidLeaveRequest}");

                    if (isUnpaidLeaveRequest)
                    {
                        _logger.LogInformation("🔍 DEBUG CHECKPOINT 12 - Processing as unpaid leave request");

                        // Validate justification is provided
                        if (string.IsNullOrWhiteSpace(JustificationReason))
                        {
                            _logger.LogWarning("❌ Justification missing for unpaid leave request");
                            ModelState.AddModelError("JustificationReason", "Justification is required for unpaid leave requests.");
                            await PopulateViewDataForFormRedisplay(employeeId);
                            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                        }

                        // CREATE UNPAID LEAVE REQUEST
                        var unpaidLeaveRequest = new UnpaidLeaveRequestModel
                        {
                            EmployeeID = employeeId,
                            LeaveTypeID = leave.LeaveTypeID,
                            StartDate = leave.StartDate,
                            EndDate = leave.EndDate,
                            RequestedDays = requestedDays,
                            ExcessDays = excessDays,
                            Reason = !string.IsNullOrWhiteSpace(leave.Reason) ? leave.Reason : "Leave request", // ✅ ENSURE REASON IS NOT NULL
                            JustificationReason = JustificationReason,
                            Status = "Pending",
                            SubmissionDate = DateTime.Now,
                            CreatedDate = DateTime.Now
                        };

                        // Add emergency leave remark if applicable
                        if (isEmergencyLeave)
                        {
                            unpaidLeaveRequest.Reason = $"[EMERGENCY LEAVE] {unpaidLeaveRequest.Reason}".Trim();
                        }

                        _logger.LogInformation("🔍 DEBUG CHECKPOINT 13 - Saving unpaid leave request");

                        try
                        {
                            _context.UnpaidLeaveRequests.Add(unpaidLeaveRequest);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation($"✅ Unpaid leave request created with ID: {unpaidLeaveRequest.UnpaidLeaveRequestID}");
                        }
                        catch (Exception saveEx)
                        {
                            _logger.LogError(saveEx, "💥 ERROR saving unpaid leave request");
                            ModelState.AddModelError("", "Error saving unpaid leave request. Please try again.");
                            await PopulateViewDataForFormRedisplay(employeeId);
                            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                        }

                        // Handle medical certificate for unpaid leave request
                        if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                            MedicalCertificate != null && MedicalCertificate.Length > 0)
                        {
                            _logger.LogInformation("🔍 DEBUG CHECKPOINT 14 - Saving medical certificate");
                            await SaveMedicalCertificateAsync(MedicalCertificate, -unpaidLeaveRequest.UnpaidLeaveRequestID, leave.LeaveTypeID, "Medical certificate for unpaid leave request");
                        }

                        // Success message
                        var successMessage = isEmergencyLeave ?
                            $"✅ Emergency Leave Request Submitted!\n\n" +
                            $"📋 Details:\n" +
                            $"• Total Days Requested: {requestedDays:0.#}\n" +
                            $"• From Annual Leave Balance: {Math.Min(remainingBalance, requestedDays):0.#}\n" +
                            $"• Unpaid Days: {excessDays:0.#}\n\n" +
                            $"⏳ Your request has been sent to HR for approval." :
                            $"✅ Leave Request Submitted!\n\n" +
                            $"📋 Details:\n" +
                            $"• Leave Type: {leaveType.TypeName}\n" +
                            $"• Total Days Requested: {requestedDays:0.#}\n" +
                            $"• From Leave Balance: {Math.Min(remainingBalance, requestedDays):0.#}\n" +
                            $"• Unpaid Days: {excessDays:0.#}\n\n" +
                            $"⏳ Your request has been sent to HR for approval.";

                        TempData["Success"] = successMessage;

                        _logger.LogInformation("🔍 DEBUG CHECKPOINT 15 - Redirecting to UnpaidLeaveRequests");
                        return RedirectToAction(nameof(UnpaidLeaveRequests));
                    }
                    else
                    {
                        _logger.LogInformation("🔍 DEBUG CHECKPOINT 16 - Balance exceeded but not unpaid request - letting frontend handle");
                        // Let the frontend JavaScript handle the unpaid leave flow
                        // The form will be redisplayed and JavaScript will show the unpaid leave section
                    }
                }

                _logger.LogInformation("🔍 DEBUG CHECKPOINT 17 - Processing normal leave (within balance or frontend handling)");

                // Validate backdate restrictions (Emergency Leave allows backdating)
                if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
                }

                // ✅ PROCEED WITH NORMAL LEAVE CREATION (within balance)
                if (ModelState.IsValid && !willExceedBalance)
                {
                    _logger.LogInformation("🔍 DEBUG CHECKPOINT 18 - ModelState valid and within balance, creating normal leave");

                    leave.Status = "Pending";
                    leave.CreatedDate = DateTime.Now;
                    leave.SubmissionDate = DateTime.Now;

                    if (isEmergencyLeave)
                    {
                        var originalReason = string.IsNullOrEmpty(leave.Reason) ? "" : leave.Reason;
                        leave.Reason = $"[EMERGENCY LEAVE] {originalReason}".Trim();
                    }

                    // Save the leave
                    _context.Leaves.Add(leave);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Leave saved successfully with ID: {leave.LeaveID}");

                    // Handle medical certificate upload
                    if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                        MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        await SaveMedicalCertificateAsync(MedicalCertificate, leave.LeaveID, leave.LeaveTypeID, "Medical certificate uploaded");
                    }

                    TempData["Success"] = isEmergencyLeave ?
                        $"✅ Emergency leave application submitted successfully! ({requestedDays:0.#} days from Annual Leave balance)" :
                        $"✅ Leave application submitted successfully! ({requestedDays:0.#} days)";

                    return RedirectToAction(nameof(LeaveRecords));
                }
                else
                {
                    _logger.LogWarning("🔍 DEBUG CHECKPOINT 19 - ModelState invalid or balance exceeded");
                    if (ModelState.ErrorCount > 0)
                    {
                        foreach (var modelError in ModelState)
                        {
                            foreach (var error in modelError.Value.Errors)
                            {
                                _logger.LogWarning($"   ModelState Error in {modelError.Key}: {error.ErrorMessage}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 DEBUG CHECKPOINT 20 - Exception in controller");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            // If we got this far, redisplay form
            _logger.LogInformation("🔍 DEBUG CHECKPOINT 21 - Redisplaying form");

            // ✅ NOW employeeId is accessible here since it's declared at method level
            await PopulateViewDataForFormRedisplay(employeeId);
            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
        }

        // Helper method to populate view data for form redisplay
        private async Task PopulateViewDataForFormRedisplay(string employeeId)
        {
            await PopulateLeaveTypeDropdownAsync();

            if (!string.IsNullOrEmpty(employeeId))
            {
                var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                ViewBag.LeaveBalances = leaveBalances;
                PopulateLeaveBalanceViewBag(leaveBalances);
            }
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

            // Check if this is an Emergency Leave
            if (leave.LeaveType.TypeName.ToLower().Contains("annual") &&
                !string.IsNullOrEmpty(leave.Reason) &&
                leave.Reason.Contains("[EMERGENCY LEAVE]"))
            {
                leave.LeaveTypeID = 4; // Display as Emergency Leave
                leave.Reason = leave.Reason.Replace("[EMERGENCY LEAVE]", "").Trim();
            }

            await PopulateLeaveTypeDropdownAsync();
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

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

            if (leaveToUpdate.Status != "Pending")
            {
                TempData["Error"] = "You can only edit leaves that are in Pending status.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            bool isEmergencyLeave = false;
            if (leave.LeaveTypeID == 4) // Emergency Leave from UI
            {
                isEmergencyLeave = true;
                var annualLeaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.TypeName.ToLower().Contains("annual"));

                if (annualLeaveType != null)
                {
                    leave.LeaveTypeID = annualLeaveType.LeaveTypeID;
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

            if (!await IsValidLeaveTypeAsync(leave.LeaveTypeID))
            {
                ModelState.AddModelError("LeaveTypeID", "Please select a valid leave type.");
            }

            // Check medical certificate requirements
            var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
            var existingLeaveDetails = await _context.LeaveDetails.FirstOrDefaultAsync(ld => ld.LeaveID == id);

            if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical"))
            {
                if (existingLeaveDetails == null && (MedicalCertificate == null || MedicalCertificate.Length == 0))
                {
                    ModelState.AddModelError("MedicalCertificate", "Medical certificate is required for medical leave.");
                }
                else if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                {
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
            var hasBalance = await HasSufficientLeaveBalanceAsync(employeeId, leave.LeaveTypeID, requestedDays, currentYear, id);

            if (!hasBalance)
            {
                var balance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear, id);
                var balanceTypeName = isEmergencyLeave ? "Annual Leave (used by Emergency Leave)" : leaveType?.TypeName ?? "Selected Leave Type";

                ModelState.AddModelError("", $"Insufficient {balanceTypeName} balance. You have {balance:0.#} days remaining but requested {requestedDays:0.#} days.");
            }

            // Validate backdate restrictions
            if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    leaveToUpdate.LeaveTypeID = leave.LeaveTypeID;
                    leaveToUpdate.StartDate = leave.StartDate;
                    leaveToUpdate.EndDate = leave.EndDate;
                    leaveToUpdate.LeaveDays = leave.LeaveDays;

                    if (isEmergencyLeave)
                    {
                        var originalReason = string.IsNullOrEmpty(leave.Reason) ? "" : leave.Reason;
                        var cleanReason = originalReason.Replace("[EMERGENCY LEAVE]", "").Trim();
                        leaveToUpdate.Reason = $"[EMERGENCY LEAVE] {cleanReason}".Trim();
                        _logger.LogInformation("📝 Updated Emergency Leave remark in reason");
                    }
                    else
                    {
                        leaveToUpdate.Reason = leave.Reason;
                    }

                    // Handle medical certificate upload if provided
                    if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        await UpdateMedicalCertificateAsync(MedicalCertificate, leave.LeaveID, leave.LeaveTypeID, existingLeaveDetails);
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

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> UnpaidLeaveRequests()
        {
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "Employee record not found.";
                return View("~/Views/Employee/Leaves/UnpaidLeaveRequests.cshtml", new List<UnpaidLeaveRequestModel>());
            }

            var unpaidRequests = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .Where(u => u.EmployeeID == employeeId)
                .OrderByDescending(u => u.SubmissionDate)
                .ToListAsync();

            return View("~/Views/Employee/Leaves/UnpaidLeaveRequests.cshtml", unpaidRequests);
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

            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

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

            if (leave.Status != "Pending")
            {
                TempData["Error"] = "This leave has already been processed.";
                return RedirectToAction(nameof(LeaveIndex));
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

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
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == employeeId);

                if (employee == null)
                {
                    return NotFound();
                }

                var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
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

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> UnpaidLeaveIndex()
        {
            var unpaidRequests = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .OrderByDescending(u => u.SubmissionDate)
                .ToListAsync();

            return View("~/Views/HR/Leaves/UnpaidLeaveIndex.cshtml", unpaidRequests);
        }

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessUnpaidLeave(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var unpaidRequest = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .FirstOrDefaultAsync(m => m.UnpaidLeaveRequestID == id);

            if (unpaidRequest == null)
            {
                return NotFound();
            }

            if (unpaidRequest.Status != "Pending")
            {
                TempData["Error"] = "This unpaid leave request has already been processed.";
                return RedirectToAction(nameof(UnpaidLeaveIndex));
            }

            var leaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == -unpaidRequest.UnpaidLeaveRequestID);

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

            return View("~/Views/HR/Leaves/ProcessUnpaidLeave.cshtml", unpaidRequest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ProcessUnpaidLeave(int id, string action, string? remarks)
        {
            var unpaidRequest = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == id);

            if (unpaidRequest == null)
            {
                return NotFound();
            }

            if (unpaidRequest.Status != "Pending")
            {
                TempData["Error"] = "This unpaid leave request has already been processed.";
                return RedirectToAction(nameof(UnpaidLeaveIndex));
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (action == "approve")
                {
                    _logger.LogInformation($"🟢 APPROVING unpaid leave request ID: {id}");
                    _logger.LogInformation($"📊 Request Details: Employee={unpaidRequest.EmployeeID}, Total={unpaidRequest.RequestedDays}, Excess={unpaidRequest.ExcessDays}");

                    // ✅ STEP 1: Approve the unpaid leave request
                    unpaidRequest.Status = "Approved";
                    unpaidRequest.ApprovalDate = DateTime.Now;
                    unpaidRequest.ApprovedBy = currentUser.Id;
                    unpaidRequest.ApprovalRemarks = remarks;

                    // ✅ STEP 2: Create the actual leave record
                    var leave = new LeaveModel
                    {
                        EmployeeID = unpaidRequest.EmployeeID,
                        LeaveTypeID = unpaidRequest.LeaveTypeID,
                        StartDate = unpaidRequest.StartDate,
                        EndDate = unpaidRequest.EndDate,
                        LeaveDays = unpaidRequest.RequestedDays,
                        Reason = unpaidRequest.Reason,
                        Status = "Approved", // Automatically approved since HR approved
                        CreatedDate = unpaidRequest.CreatedDate,
                        SubmissionDate = unpaidRequest.SubmissionDate,
                        ApprovalDate = DateTime.Now,
                        ApprovedBy = currentUser.Id,
                        ApprovalRemarks = $"✅ APPROVED AS UNPAID LEAVE\n" +
                                         $"📊 Breakdown:\n" +
                                         $"• Total Days: {unpaidRequest.RequestedDays}\n" +
                                         $"• From Balance: {(unpaidRequest.RequestedDays - unpaidRequest.ExcessDays):0.#}\n" +
                                         $"• Unpaid Days: {unpaidRequest.ExcessDays:0.#}\n" +
                                         $"• Employee Balance Reset: 0\n\n" +
                                         $"{(!string.IsNullOrEmpty(remarks) ? $"HR Remarks: {remarks}" : "")}"
                    };

                    _context.Leaves.Add(leave);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Created leave record with ID: {leave.LeaveID}");

                    // ✅ STEP 3: Transfer medical certificate if exists
                    var unpaidLeaveDetails = await _context.LeaveDetails
                        .FirstOrDefaultAsync(ld => ld.LeaveID == -unpaidRequest.UnpaidLeaveRequestID);

                    if (unpaidLeaveDetails != null)
                    {
                        unpaidLeaveDetails.LeaveID = leave.LeaveID; // Change to positive leave ID
                        unpaidLeaveDetails.Comment = "Medical certificate transferred from approved unpaid leave request";
                        _logger.LogInformation($"✅ Transferred medical certificate to leave ID: {leave.LeaveID}");
                    }

                    // ✅ STEP 4: Calculate the balance impact
                    var currentYear = DateTime.Now.Year;
                    var currentBalance = await GetRemainingLeaveBalanceAsync(unpaidRequest.EmployeeID, unpaidRequest.LeaveTypeID, currentYear, leave.LeaveID);
                    var paidDays = unpaidRequest.RequestedDays - unpaidRequest.ExcessDays;
                    var newBalance = Math.Max(0, currentBalance - paidDays);

                    _logger.LogInformation($"📊 BALANCE CALCULATION:");
                    _logger.LogInformation($"   Before: {currentBalance + paidDays} days");
                    _logger.LogInformation($"   Deducting: {paidDays} days (paid portion)");
                    _logger.LogInformation($"   After: {newBalance} days");
                    _logger.LogInformation($"   Unpaid Portion: {unpaidRequest.ExcessDays} days");

                    await _context.SaveChangesAsync();

                    // ✅ SUCCESS MESSAGE with detailed breakdown
                    TempData["Success"] = $"✅ UNPAID LEAVE REQUEST APPROVED!\n\n" +
                                         $"📋 Employee: {unpaidRequest.Employee.Username}\n" +
                                         $"📅 Period: {unpaidRequest.StartDate:dd MMM} - {unpaidRequest.EndDate:dd MMM yyyy}\n" +
                                         $"📊 Breakdown:\n" +
                                         $"• Total Days: {unpaidRequest.RequestedDays}\n" +
                                         $"• Paid (from balance): {paidDays:0.#} days\n" +
                                         $"• Unpaid: {unpaidRequest.ExcessDays:0.#} days\n\n" +
                                         $"✅ Leave record created and employee's leave balance has been adjusted.";
                }
                else if (action == "reject")
                {
                    _logger.LogInformation($"🔴 REJECTING unpaid leave request ID: {id}");

                    unpaidRequest.Status = "Rejected";
                    unpaidRequest.ApprovalDate = DateTime.Now;
                    unpaidRequest.ApprovedBy = currentUser.Id;
                    unpaidRequest.ApprovalRemarks = remarks;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"❌ UNPAID LEAVE REQUEST REJECTED\n\n" +
                                         $"📋 Employee: {unpaidRequest.Employee.Username}\n" +
                                         $"📅 Period: {unpaidRequest.StartDate:dd MMM} - {unpaidRequest.EndDate:dd MMM yyyy}\n" +
                                         $"📊 Days: {unpaidRequest.RequestedDays} ({unpaidRequest.ExcessDays:0.#} would have been unpaid)\n\n" +
                                         $"✅ Employee's leave balance remains unchanged.";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessUnpaidLeave), new { id = id });
                }

                return RedirectToAction(nameof(UnpaidLeaveIndex));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "💥 Database error while processing unpaid leave request");
                TempData["Error"] = "An error occurred while processing the request. Please try again.";
                return RedirectToAction(nameof(ProcessUnpaidLeave), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error while processing unpaid leave request");
                TempData["Error"] = "An unexpected error occurred. Please try again.";
                return RedirectToAction(nameof(ProcessUnpaidLeave), new { id = id });
            }
        }

        // ================== HELPER METHODS ==================

        private async Task SaveMedicalCertificateAsync(IFormFile medicalCertificate, int leaveId, int leaveTypeId, string comment)
        {
            try
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "medical-certificates");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{leaveId}_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(medicalCertificate.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await medicalCertificate.CopyToAsync(stream);
                }

                var leaveDetails = new LeaveDetailsModel
                {
                    LeaveID = leaveId,
                    LeaveTypeID = leaveTypeId,
                    Comment = comment,
                    DocumentPath = $"/uploads/medical-certificates/{fileName}",
                    UploadDate = DateTime.Now
                };

                _context.LeaveDetails.Add(leaveDetails);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Medical certificate saved: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error saving medical certificate");
                throw;
            }
        }

        private async Task UpdateMedicalCertificateAsync(IFormFile medicalCertificate, int leaveId, int leaveTypeId, LeaveDetailsModel? existingLeaveDetails)
        {
            try
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "medical-certificates");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{leaveId}_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(medicalCertificate.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await medicalCertificate.CopyToAsync(stream);
                }

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
                    existingLeaveDetails.LeaveTypeID = leaveTypeId;
                }
                else
                {
                    // Create new record
                    var leaveDetails = new LeaveDetailsModel
                    {
                        LeaveID = leaveId,
                        LeaveTypeID = leaveTypeId,
                        Comment = "Medical certificate uploaded",
                        DocumentPath = $"/uploads/medical-certificates/{fileName}",
                        UploadDate = DateTime.Now
                    };
                    _context.LeaveDetails.Add(leaveDetails);
                }

                _logger.LogInformation($"✅ Medical certificate updated: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error updating medical certificate");
                throw;
            }
        }

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
                    // Create a list that includes Emergency Leave
                    var dropdownItems = new List<dynamic>();

                    // Add existing database leave types
                    foreach (var lt in leaveTypes)
                    {
                        _logger.LogInformation($"📝 Leave Type: ID={lt.LeaveTypeID}, Name={lt.TypeName}");
                        dropdownItems.Add(new { LeaveTypeID = lt.LeaveTypeID, TypeName = lt.TypeName });
                    }

                    // Add Emergency Leave (UI only - will be converted to Annual Leave)
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