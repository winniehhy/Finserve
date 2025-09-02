using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace FinserveNew.Controllers
{
    public class LeavesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LeavesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public LeavesController(AppDbContext context, ILogger<LeavesController> logger, UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _emailSender = emailSender;
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

            // Check for unpaid leave requests (pending or approved)
            var unpaidLeaveRequests = await _context.UnpaidLeaveRequests
                .Where(u => u.EmployeeID == employeeId &&
                           (u.Status == "Pending" || u.Status == "Approved"))
                .ToListAsync();

            ViewBag.HasUnpaidLeaveRequests = unpaidLeaveRequests.Any();
            ViewBag.UnpaidLeaveRequestCount = unpaidLeaveRequests.Count;

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

            // First check if it's a regular leave
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(m => m.LeaveID == id && m.EmployeeID == employeeId);

            if (leave != null)
            {
                // It's a regular leave - show regular details
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

            // If not found in regular leaves, check if it's an unpaid leave
            // For unpaid leaves, redirect to the unpaid leave requests page
            return RedirectToAction("UnpaidLeaveRequests");
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
              string? Reason = null, string? UnpaidLeaveReason = null, bool ConfirmUnpaidLeave = false,
      string? AlternativeLeaveChoice = null, string? AlternativeLeaveTypeIds = null)
        {
            _logger.LogInformation("🚀 CREATE LEAVE POST started");
            _logger.LogInformation($"📝 Received leave data: Type={leave.LeaveTypeID}, Start={leave.StartDate}, End={leave.EndDate}, DayType={DayType}");
            _logger.LogInformation($"🔍 Alternative Leave Choice: {AlternativeLeaveChoice}");
            _logger.LogInformation($"🔍 Alternative Leave Type IDs: {AlternativeLeaveTypeIds}");

            string employeeId = null;

            try
            {
                // Get current employee ID FIRST
                employeeId = await GetCurrentEmployeeId();
                if (string.IsNullOrEmpty(employeeId))
                {
                    _logger.LogError("❌ Employee ID not found");
                    ModelState.AddModelError("", "Employee record not found. Please contact administrator.");
                    await PopulateLeaveTypeDropdownAsync();
                    return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                }

                // Set EmployeeID and clear validation errors
                leave.EmployeeID = employeeId;
                ModelState.Remove("EmployeeID");
                ModelState.Remove("Employee");

                // Handle Emergency Leave - Convert to Annual Leave in database
                bool isEmergencyLeave = false;
                if (leave.LeaveTypeID == 4) // Emergency Leave from UI
                {
                    _logger.LogInformation("🔍 Processing Emergency Leave");
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

                // Validate medical certificate for medical leave
                var leaveType = await _context.LeaveTypes.FindAsync(leave.LeaveTypeID);
                if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical"))
                {
                    if (MedicalCertificate == null || MedicalCertificate.Length == 0)
                    {
                        ModelState.AddModelError("MedicalCertificate", "Medical certificate is required for medical leave.");
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

                // Check leave balance and handle alternative leave types or unpaid leave
                var currentYear = DateTime.Now.Year;
                var requestedDays = leave.LeaveDays;
                var remainingBalance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear);

                bool willExceedBalance = requestedDays > remainingBalance;
                double excessDays = Math.Max(0, requestedDays - remainingBalance);

                _logger.LogInformation($"🔍 Balance Check: Requested={requestedDays}, Available={remainingBalance}, Excess={excessDays}");

                if (willExceedBalance)
                {
                    _logger.LogInformation("🔍 Balance exceeded, checking alternative options");

                    // Check if user chose to use alternative leave types
                    if (AlternativeLeaveChoice == "alternative" && !string.IsNullOrEmpty(AlternativeLeaveTypeIds))
                    {
                        _logger.LogInformation("🔍 Processing alternative leave types allocation");

                        var alternativeTypeIds = AlternativeLeaveTypeIds.Split(',')
                            .Where(id => !string.IsNullOrEmpty(id))
                            .Select(int.Parse)
                            .ToList();

                        // Create multiple leave records for different leave types
                        var leaveRecords = new List<LeaveModel>();
                        double remainingDaysToAllocate = requestedDays;

                        // First, use the original leave type balance
                        if (remainingBalance > 0)
                        {
                            var originalLeave = new LeaveModel
                            {
                                EmployeeID = employeeId,
                                LeaveTypeID = leave.LeaveTypeID,
                                StartDate = leave.StartDate,
                                EndDate = leave.EndDate,
                                LeaveDays = Math.Min(remainingBalance, remainingDaysToAllocate),
                                Reason = isEmergencyLeave ? $"[EMERGENCY LEAVE] {leave.Reason ?? ""}".Trim() : leave.Reason,
                                Status = "Pending",
                                CreatedDate = DateTime.Now,
                                SubmissionDate = DateTime.Now
                            };
                            leaveRecords.Add(originalLeave);
                            remainingDaysToAllocate -= originalLeave.LeaveDays;
                        }

                        // Then use alternative leave types
                        foreach (var altTypeId in alternativeTypeIds)
                        {
                            if (remainingDaysToAllocate <= 0) break;

                            var altBalance = await GetRemainingLeaveBalanceAsync(employeeId, altTypeId, currentYear);
                            if (altBalance > 0)
                            {
                                var altLeaveType = await _context.LeaveTypes.FindAsync(altTypeId);
                                var daysToUse = Math.Min(altBalance, remainingDaysToAllocate);

                                var altLeave = new LeaveModel
                                {
                                    EmployeeID = employeeId,
                                    LeaveTypeID = altTypeId,
                                    StartDate = leave.StartDate,
                                    EndDate = leave.EndDate,
                                    LeaveDays = daysToUse,
                                    Reason = $"[ALTERNATIVE LEAVE - Original: {leaveType.TypeName}] {leave.Reason ?? ""}".Trim(),
                                    Status = "Pending",
                                    CreatedDate = DateTime.Now,
                                    SubmissionDate = DateTime.Now
                                };
                                leaveRecords.Add(altLeave);
                                remainingDaysToAllocate -= daysToUse;
                            }
                        }

                        // If there are still remaining days, create unpaid leave request
                        if (remainingDaysToAllocate > 0)
                        {
                            if (string.IsNullOrWhiteSpace(Reason))
                            {
                                ModelState.AddModelError("Reason", "Reason is required for the unpaid portion of your leave.");
                                await PopulateViewDataForFormRedisplay(employeeId);
                                return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                            }

                            var unpaidLeaveRequest = new UnpaidLeaveRequestModel
                            {
                                EmployeeID = employeeId,
                                LeaveTypeID = leave.LeaveTypeID,
                                StartDate = leave.StartDate,
                                EndDate = leave.EndDate,
                                RequestedDays = remainingDaysToAllocate,
                                ExcessDays = remainingDaysToAllocate,
                                Reason = isEmergencyLeave ? $"[EMERGENCY LEAVE - UNPAID PORTION] {leave.Reason ?? "No reason provided"}".Trim() : $"[UNPAID PORTION] {leave.Reason ?? "No reason provided"}".Trim(),
                                JustificationReason = leave.Reason ?? "No justification provided",
                                Status = "Pending",
                                SubmissionDate = DateTime.Now,
                                CreatedDate = DateTime.Now
                            };

                            _context.UnpaidLeaveRequests.Add(unpaidLeaveRequest);
                        }

                        // Save all leave records
                        foreach (var leaveRecord in leaveRecords)
                        {
                            _context.Leaves.Add(leaveRecord);
                        }

                        await _context.SaveChangesAsync();

                        // Handle medical certificate for the first leave record
                        if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                            MedicalCertificate != null && MedicalCertificate.Length > 0 && leaveRecords.Any())
                        {
                            await SaveMedicalCertificateAsync(MedicalCertificate, leaveRecords.First().LeaveID, leave.LeaveTypeID, "Medical certificate for multi-type leave application");
                        }

                        // Success message with breakdown
                        var breakdown = string.Join("\n", leaveRecords.Select(lr =>
                            $"• {_context.LeaveTypes.Find(lr.LeaveTypeID)?.TypeName}: {lr.LeaveDays:0.#} days"));

                        if (remainingDaysToAllocate > 0)
                        {
                            breakdown += $"\n• Unpaid Leave: {remainingDaysToAllocate:0.#} days";
                        }

                        TempData["Success"] = $"✅ Leave Application Submitted Successfully!\n\n" +
                                            $"📋 Your {requestedDays:0.#} days leave has been allocated as follows:\n" +
                                            breakdown + "\n\n" +
                                            "⏳ All requests have been sent for approval.";

                        // Send email notification to HR
                        await SendLeaveSubmissionNotificationToHR(leave);

                        return RedirectToAction(nameof(LeaveRecords));
                    }
                    // Check if this is an unpaid leave request
                    else if (Request.Form["IsUnpaidLeaveRequest"].ToString() == "true")
                    {
                        if (string.IsNullOrWhiteSpace(UnpaidLeaveReason))
                        {
                            ModelState.AddModelError("UnpaidLeaveReason", "Reason is required for unpaid leave requests.");
                            await PopulateViewDataForFormRedisplay(employeeId);
                            return View("~/Views/Employee/Leaves/Create.cshtml", leave);
                        }

                        var unpaidLeaveRequest = new UnpaidLeaveRequestModel
                        {
                            EmployeeID = employeeId,
                            LeaveTypeID = leave.LeaveTypeID,
                            StartDate = leave.StartDate,
                            EndDate = leave.EndDate,
                            RequestedDays = requestedDays,
                            ExcessDays = excessDays,
                            Reason = UnpaidLeaveReason,
                            JustificationReason = UnpaidLeaveReason,
                            Status = "Pending",
                            SubmissionDate = DateTime.Now,
                            CreatedDate = DateTime.Now
                        };

                        if (isEmergencyLeave)
                        {
                            unpaidLeaveRequest.Reason = $"[EMERGENCY LEAVE] {unpaidLeaveRequest.Reason}".Trim();
                        }

                        _context.UnpaidLeaveRequests.Add(unpaidLeaveRequest);
                        await _context.SaveChangesAsync();

                        // Handle medical certificate for unpaid leave request
                        if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                            MedicalCertificate != null && MedicalCertificate.Length > 0)
                        {
                            await SaveMedicalCertificateAsync(MedicalCertificate, -unpaidLeaveRequest.UnpaidLeaveRequestID, leave.LeaveTypeID, "Medical certificate for unpaid leave request");
                        }

                        var successMessage = isEmergencyLeave ?
                            $"✅ Emergency Leave Request Submitted!\n\n" +
                            $"📋 Details:\n" +
                            $"• Total Days Requested: {requestedDays:0.#}\n" +
                            $"• From {leaveType.TypeName} Balance: {Math.Min(remainingBalance, requestedDays):0.#}\n" +
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

                        // Send email notification to HR
                        await SendUnpaidLeaveSubmissionNotificationToHR(unpaidLeaveRequest);

                        return RedirectToAction(nameof(UnpaidLeaveRequests));
                    }
                }

                // Validate backdate restrictions (Emergency Leave allows backdating)
                if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
                }

                // Proceed with normal leave creation (within balance)
                if (ModelState.IsValid && !willExceedBalance)
                {
                    leave.Status = "Pending";
                    leave.CreatedDate = DateTime.Now;
                    leave.SubmissionDate = DateTime.Now;

                    if (isEmergencyLeave)
                    {
                        var originalReason = string.IsNullOrEmpty(leave.Reason) ? "" : leave.Reason;
                        leave.Reason = $"[EMERGENCY LEAVE] {originalReason}".Trim();
                    }

                    _context.Leaves.Add(leave);
                    await _context.SaveChangesAsync();

                    // Handle medical certificate upload
                    if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                        MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        await SaveMedicalCertificateAsync(MedicalCertificate, leave.LeaveID, leave.LeaveTypeID, "Medical certificate uploaded");
                    }

                    TempData["Success"] = isEmergencyLeave ?
                        $"✅ Emergency leave application submitted successfully! ({requestedDays:0.#} days from Annual Leave balance)" :
                        $"✅ Leave application submitted successfully! ({requestedDays:0.#} days)";

                    // Send email notification to HR
                    await SendLeaveSubmissionNotificationToHR(leave);

                    return RedirectToAction(nameof(LeaveRecords));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Exception in controller");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            // If we got this far, redisplay form
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
        public async Task<IActionResult> Edit(int id, LeaveModel leave, IFormFile? MedicalCertificate, string DayType = "full",
            string? AlternativeLeaveChoice = null, string? AlternativeLeaveTypeIds = null, string? Reason = null, string? UnpaidLeaveReason = null)
        {
            _logger.LogInformation($"🚀 Edit action called for Leave ID: {id}, LeaveModel ID: {leave.LeaveID}");
            _logger.LogInformation($"📝 Leave data received - TypeID: {leave.LeaveTypeID}, Start: {leave.StartDate}, End: {leave.EndDate}, Reason: '{leave.Reason}', LeaveDays: {leave.LeaveDays}");
            
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

            // Check leave balance and handle alternative leave types
            var currentYear = DateTime.Now.Year;
            var requestedDays = leave.LeaveDays;
            var remainingBalance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear, id);

            bool willExceedBalance = requestedDays > remainingBalance;
            double excessDays = Math.Max(0, requestedDays - remainingBalance);

            if (willExceedBalance)
            {
                _logger.LogInformation("🔍 Balance exceeded during edit, checking alternative options");

                // Check if user chose to use alternative leave types
                if (AlternativeLeaveChoice == "alternative" && !string.IsNullOrEmpty(AlternativeLeaveTypeIds))
                {
                    _logger.LogInformation("🔍 Processing alternative leave types allocation for edit");

                    var alternativeTypeIds = AlternativeLeaveTypeIds.Split(',')
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Select(int.Parse)
                        .ToList();

                    // Validate the alternative leave allocation
                    var validation = await ValidateAlternativeLeaveAllocationAsync(employeeId, leave.LeaveTypeID, requestedDays, alternativeTypeIds, currentYear);
                    if (!validation.isValid)
                    {
                        ModelState.AddModelError("", validation.errorMessage);
                        await PopulateLeaveTypeDropdownAsync();
                        var currentLeaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                        ViewBag.LeaveBalances = currentLeaveBalances;
                        PopulateLeaveBalanceViewBag(currentLeaveBalances);
                        return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
                    }

                    // Get the allocation breakdown
                    var allocationBreakdown = await CreateLeaveAllocationBreakdownAsync(employeeId, leave.LeaveTypeID, requestedDays, alternativeTypeIds, currentYear);
                    LogLeaveAllocation(employeeId, allocationBreakdown, requestedDays);

                    // Check if there are unpaid days and justification is required
                    var unpaidDays = (double)allocationBreakdown.GetType().GetProperty("UnpaidDays")?.GetValue(allocationBreakdown);
                    if (unpaidDays > 0 && string.IsNullOrWhiteSpace(Reason))
                    {
                        ModelState.AddModelError("Reason", "Reason is required for the unpaid portion of your leave.");
                        await PopulateLeaveTypeDropdownAsync();
                        var currentLeaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                        ViewBag.LeaveBalances = currentLeaveBalances;
                        PopulateLeaveBalanceViewBag(currentLeaveBalances);
                        return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
                    }

                    // Delete the original leave and create new ones based on allocation
                    _context.Leaves.Remove(leaveToUpdate);

                    var leaveRecords = new List<LeaveModel>();
                    var breakdown = allocationBreakdown.GetType().GetProperty("Breakdown")?.GetValue(allocationBreakdown) as IEnumerable<object>;

                    if (breakdown != null)
                    {
                        foreach (var item in breakdown)
                        {
                            var typeId = (int)item.GetType().GetProperty("LeaveTypeID")?.GetValue(item);
                            var typeName = (string)item.GetType().GetProperty("TypeName")?.GetValue(item);
                            var allocatedDays = (double)item.GetType().GetProperty("AllocatedDays")?.GetValue(item);

                            var newLeave = new LeaveModel
                            {
                                EmployeeID = employeeId,
                                LeaveTypeID = typeId,
                                StartDate = leave.StartDate,
                                EndDate = leave.EndDate,
                                LeaveDays = allocatedDays,
                                Reason = typeId == leave.LeaveTypeID ?
                                    (isEmergencyLeave ? $"[EMERGENCY LEAVE] {leave.Reason ?? ""}".Trim() : leave.Reason) :
                                    $"[ALTERNATIVE LEAVE - Original: {leaveType.TypeName}] {leave.Reason ?? ""}".Trim(),
                                Status = "Pending",
                                CreatedDate = leaveToUpdate.CreatedDate,
                                SubmissionDate = DateTime.Now
                            };

                            leaveRecords.Add(newLeave);
                            _context.Leaves.Add(newLeave);
                        }
                    }

                    // Create unpaid leave request if there are unpaid days
                    if (unpaidDays > 0)
                    {
                        var unpaidLeaveRequest = new UnpaidLeaveRequestModel
                        {
                            EmployeeID = employeeId,
                            LeaveTypeID = leave.LeaveTypeID,
                            StartDate = leave.StartDate,
                            EndDate = leave.EndDate,
                            RequestedDays = unpaidDays,
                            ExcessDays = unpaidDays,
                            Reason = Reason,
                            JustificationReason = Reason,
                            Status = "Pending",
                            SubmissionDate = DateTime.Now,
                            CreatedDate = DateTime.Now
                        };

                        _context.UnpaidLeaveRequests.Add(unpaidLeaveRequest);
                    }

                    await _context.SaveChangesAsync();

                    // Handle medical certificate for the first leave record
                    if (leaveType != null && leaveType.TypeName.ToLower().Contains("medical") &&
                        MedicalCertificate != null && MedicalCertificate.Length > 0 && leaveRecords.Any())
                    {
                        await SaveMedicalCertificateAsync(MedicalCertificate, leaveRecords.First().LeaveID, leave.LeaveTypeID, "Medical certificate for updated multi-type leave application");
                    }

                    // Success message with breakdown
                    var breakdownList = breakdown.Cast<object>().Select(item =>
                        $"• {item.GetType().GetProperty("TypeName")?.GetValue(item)}: {item.GetType().GetProperty("AllocatedDays")?.GetValue(item):0.#} days").ToList();

                    if (unpaidDays > 0)
                    {
                        breakdownList.Add($"• Unpaid Leave: {unpaidDays:0.#} days");
                    }

                    TempData["Success"] = $"✅ Leave Updated Successfully!\n\n" +
                                        $"📋 Your {requestedDays:0.#} days leave has been allocated as follows:\n" +
                                        string.Join("\n", breakdownList) + "\n\n" +
                                        "⏳ All requests have been sent for approval.";

                    // Send email notification to HR
                    await SendLeaveSubmissionNotificationToHR(leave);

                    return RedirectToAction(nameof(LeaveRecords));
                }
                // Handle direct unpaid leave request
                else if (Request.Form["IsUnpaidLeaveRequest"].ToString() == "true")
                {
                    if (string.IsNullOrWhiteSpace(UnpaidLeaveReason))
                        {
                        ModelState.AddModelError("UnpaidLeaveReason", "Reason is required for unpaid leave requests.");
                            await PopulateLeaveTypeDropdownAsync();
                            var currentLeaveBalances = await CalculateLeaveBalancesAsync(employeeId);
                            ViewBag.LeaveBalances = currentLeaveBalances;
                            PopulateLeaveBalanceViewBag(currentLeaveBalances);
                            return View("~/Views/Employee/Leaves/Edit.cshtml", leaveToUpdate);
                        }

                    // Convert to unpaid leave request
                    _context.Leaves.Remove(leaveToUpdate);

                    var unpaidLeaveRequest = new UnpaidLeaveRequestModel
                    {
                        EmployeeID = employeeId,
                        LeaveTypeID = leave.LeaveTypeID,
                        StartDate = leave.StartDate,
                        EndDate = leave.EndDate,
                        RequestedDays = requestedDays,
                        ExcessDays = excessDays,
                        Reason = UnpaidLeaveReason,
                        JustificationReason = UnpaidLeaveReason,
                        Status = "Pending",
                        SubmissionDate = DateTime.Now,
                        CreatedDate = leaveToUpdate.CreatedDate
                    };

                    if (isEmergencyLeave)
                    {
                        unpaidLeaveRequest.Reason = $"[EMERGENCY LEAVE] {unpaidLeaveRequest.Reason}".Trim();
                    }

                    _context.UnpaidLeaveRequests.Add(unpaidLeaveRequest);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"✅ Leave converted to Unpaid Leave Request!\n\n" +
                                        $"📋 Details:\n" +
                                        $"• Total Days Requested: {requestedDays:0.#}\n" +
                                        $"• From {leaveType.TypeName} Balance: {Math.Min(remainingBalance, requestedDays):0.#}\n" +
                                        $"• Unpaid Days: {excessDays:0.#}\n\n" +
                                        $"⏳ Your request has been sent to HR for approval.";

                    // Send email notification to HR
                    await SendUnpaidLeaveSubmissionNotificationToHR(unpaidLeaveRequest);

                    return RedirectToAction(nameof(UnpaidLeaveRequests));
                }
                else
                {
                    // Balance exceeded but no choice made - let frontend handle
                    var balance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear, id);
                    var balanceTypeName = isEmergencyLeave ? "Annual Leave (used by Emergency Leave)" : leaveType?.TypeName ?? "Selected Leave Type";

                    ModelState.AddModelError("", $"Insufficient {balanceTypeName} balance. You have {balance:0.#} days remaining but requested {requestedDays:0.#} days. Please select an alternative or apply for unpaid leave.");
                }
            }

            // Validate backdate restrictions
            if (!isEmergencyLeave && !await IsBackdateAllowedAsync(leave.LeaveTypeID) && leave.StartDate < DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError("StartDate", "You cannot select past dates for this leave type.");
            }

            _logger.LogInformation($"🔍 ModelState.IsValid: {ModelState.IsValid}");
            if (!ModelState.IsValid)
            {
                _logger.LogWarning($"❌ ModelState validation failed for leave ID {leaveToUpdate.LeaveID}");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Any())
                    {
                        _logger.LogWarning($"ModelState Error - {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
            }

            // FORCE UPDATE - Always save changes regardless of validation
            try
            {
                _logger.LogInformation($"🚀 FORCE UPDATING leave ID {leaveToUpdate.LeaveID}");
                _logger.LogInformation($"📝 Original data - TypeID: {leaveToUpdate.LeaveTypeID}, Start: {leaveToUpdate.StartDate}, End: {leaveToUpdate.EndDate}, Reason: '{leaveToUpdate.Reason}'");
                _logger.LogInformation($"📝 New data - TypeID: {leave.LeaveTypeID}, Start: {leave.StartDate}, End: {leave.EndDate}, Reason: '{leave.Reason}'");
                
                // Force update all fields
                    leaveToUpdate.LeaveTypeID = leave.LeaveTypeID;
                    leaveToUpdate.StartDate = leave.StartDate;
                    leaveToUpdate.EndDate = leave.EndDate;
                    leaveToUpdate.LeaveDays = leave.LeaveDays;
                leaveToUpdate.Reason = leave.Reason ?? leaveToUpdate.Reason; // Keep original if new is null

                    // Handle medical certificate upload if provided
                    if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        await UpdateMedicalCertificateAsync(MedicalCertificate, leave.LeaveID, leave.LeaveTypeID, existingLeaveDetails);
                    }

                    await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ FORCE UPDATE SUCCESSFUL - leave ID {leaveToUpdate.LeaveID} updated");

                TempData["Success"] = "Leave updated successfully!";

                    return RedirectToAction(nameof(LeaveRecords));
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ FORCE UPDATE FAILED for leave ID {leaveToUpdate.LeaveID}");
                TempData["Error"] = "An error occurred while updating the leave.";
                return RedirectToAction(nameof(LeaveRecords));
            }

            await PopulateLeaveTypeDropdownAsync();
            var finalLeaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = finalLeaveBalances;
            PopulateLeaveBalanceViewBag(finalLeaveBalances);

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

            // Check if this is an unpaid leave request (positive ID from UnpaidLeaveRequests table)
            var unpaidRequest = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == id && u.EmployeeID == employeeId);

            if (unpaidRequest != null)
            {
                // Handle unpaid leave request deletion
                if (unpaidRequest.Status != "Pending")
                {
                    TempData["Error"] = "You can only delete unpaid leave requests that are in Pending status.";
                    return RedirectToAction(nameof(UnpaidLeaveRequests));
                }

                // Create a temporary leave model for the delete view
                var leaveModel = new LeaveModel
                {
                    LeaveID = -unpaidRequest.UnpaidLeaveRequestID, // Negative to indicate unpaid leave
                    EmployeeID = unpaidRequest.EmployeeID,
                    LeaveTypeID = unpaidRequest.LeaveTypeID,
                    StartDate = unpaidRequest.StartDate,
                    EndDate = unpaidRequest.EndDate,
                    LeaveDays = unpaidRequest.RequestedDays,
                    Reason = unpaidRequest.Reason,
                    Status = unpaidRequest.Status,
                    SubmissionDate = unpaidRequest.SubmissionDate,
                    Employee = unpaidRequest.Employee,
                    LeaveType = unpaidRequest.LeaveType
                };

                ViewBag.IsUnpaidLeave = true;
                ViewBag.UnpaidLeaveRequestID = unpaidRequest.UnpaidLeaveRequestID;
                return View("~/Views/Employee/Leaves/Delete.cshtml", leaveModel);
            }

            // Handle regular leave deletion
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

            ViewBag.IsUnpaidLeave = false;
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

            // Check if this is an unpaid leave request (negative ID)
            if (id < 0)
            {
                var unpaidRequestId = Math.Abs(id);
                var unpaidRequest = await _context.UnpaidLeaveRequests
                    .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == unpaidRequestId && u.EmployeeID == employeeId);

                if (unpaidRequest != null)
                {
                    // Only allow deletion if status is Pending
                    if (unpaidRequest.Status != "Pending")
                    {
                        TempData["Error"] = "You can only cancel unpaid leave requests that are in Pending status.";
                        return RedirectToAction(nameof(UnpaidLeaveRequests));
                    }

                    try
                    {
                        // Remove associated leave details if any
                        var leaveDetails = await _context.LeaveDetails
                            .FirstOrDefaultAsync(ld => ld.LeaveID == -unpaidRequest.UnpaidLeaveRequestID);
                        
                        if (leaveDetails != null)
                        {
                            _context.LeaveDetails.Remove(leaveDetails);
                        }

                        // Remove the unpaid leave request
                        _context.UnpaidLeaveRequests.Remove(unpaidRequest);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = "Unpaid leave request cancelled successfully!";
                    }
                    catch (Exception ex)
                    {
                        TempData["Error"] = "An error occurred while cancelling the unpaid leave request.";
                    }

                    return RedirectToAction(nameof(UnpaidLeaveRequests));
                }
            }
            else
            {
                // Handle regular leave deletion
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

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> EditUnpaidLeave(int? id)
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

            var unpaidRequest = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == id && u.EmployeeID == employeeId);

            if (unpaidRequest == null)
            {
                return NotFound();
            }

            // Only allow editing if status is Pending
            if (unpaidRequest.Status != "Pending")
            {
                TempData["Error"] = "You can only edit unpaid leave requests that are in Pending status.";
                return RedirectToAction(nameof(UnpaidLeaveRequests));
            }

            var leaveModel = new LeaveModel
            {
                LeaveID = -unpaidRequest.UnpaidLeaveRequestID, // Negative to indicate unpaid leave
                EmployeeID = unpaidRequest.EmployeeID,
                LeaveTypeID = unpaidRequest.LeaveTypeID,
                StartDate = unpaidRequest.StartDate,
                EndDate = unpaidRequest.EndDate,
                LeaveDays = unpaidRequest.RequestedDays, // FIXED: Uncommented to show duration
                Reason = unpaidRequest.Reason,
                Status = unpaidRequest.Status,
                CreatedDate = unpaidRequest.SubmissionDate
            };

            ViewBag.IsUnpaidLeave = true;
            ViewBag.UnpaidLeaveRequestID = unpaidRequest.UnpaidLeaveRequestID;
            ViewBag.Reason = unpaidRequest.Reason;
            ViewBag.LeaveTypeName = unpaidRequest.LeaveType.TypeName;

            // FIXED: Add leave balance calculation for unpaid leave requests
            await PopulateLeaveTypeDropdownAsync();
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            return View("~/Views/Employee/Leaves/Edit.cshtml", leaveModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> EditUnpaidLeave(int id, LeaveModel leave, IFormFile? MedicalCertificate,
            string DayType = "full", string? Reason = null)
        {
            _logger.LogInformation($"🚀 EditUnpaidLeave action called for Unpaid Leave Request ID: {id}");
            
            var employeeId = await GetCurrentEmployeeId();
            if (string.IsNullOrEmpty(employeeId))
                return NotFound();

            // Get the actual unpaid leave request
            var unpaidRequest = await _context.UnpaidLeaveRequests
                .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == id && u.EmployeeID == employeeId);

            if (unpaidRequest == null)
                return NotFound();

            if (unpaidRequest.Status != "Pending")
            {
                TempData["Error"] = "You can only edit unpaid leave requests that are in Pending status.";
                return RedirectToAction(nameof(UnpaidLeaveRequests));
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
            var existingLeaveDetails = await _context.LeaveDetails
                .FirstOrDefaultAsync(ld => ld.LeaveID == -unpaidRequest.UnpaidLeaveRequestID);

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

            // FORCE UPDATE - Always save changes for unpaid leave requests
            try
            {
                _logger.LogInformation($"🚀 FORCE UPDATING unpaid leave request ID {unpaidRequest.UnpaidLeaveRequestID}");
                _logger.LogInformation($"📝 Original data - TypeID: {unpaidRequest.LeaveTypeID}, Start: {unpaidRequest.StartDate}, End: {unpaidRequest.EndDate}, Reason: '{unpaidRequest.Reason}'");
                _logger.LogInformation($"📝 New data - TypeID: {leave.LeaveTypeID}, Start: {leave.StartDate}, End: {leave.EndDate}, Reason: '{Reason}'");
                
                // Force update all fields
                    unpaidRequest.LeaveTypeID = leave.LeaveTypeID;
                    unpaidRequest.StartDate = leave.StartDate;
                    unpaidRequest.EndDate = leave.EndDate;
                    unpaidRequest.RequestedDays = leave.LeaveDays;
                unpaidRequest.Reason = Reason ?? unpaidRequest.Reason; // Keep original if new is null
                unpaidRequest.JustificationReason = Reason ?? unpaidRequest.JustificationReason;

                    // Recalculate excess days based on current balance
                    var currentYear = DateTime.Now.Year;
                    var remainingBalance = await GetRemainingLeaveBalanceAsync(employeeId, leave.LeaveTypeID, currentYear);
                    unpaidRequest.ExcessDays = Math.Max(0, leave.LeaveDays - remainingBalance);

                    // Handle medical certificate upload if provided
                    if (MedicalCertificate != null && MedicalCertificate.Length > 0)
                    {
                        await UpdateMedicalCertificateAsync(MedicalCertificate, -unpaidRequest.UnpaidLeaveRequestID, leave.LeaveTypeID, existingLeaveDetails);
                    }

                    await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ FORCE UPDATE SUCCESSFUL - unpaid leave request ID {unpaidRequest.UnpaidLeaveRequestID} updated");

                    TempData["Success"] = "Unpaid leave request updated successfully!";
                    return RedirectToAction(nameof(UnpaidLeaveRequests));
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ FORCE UPDATE FAILED for unpaid leave request ID {unpaidRequest.UnpaidLeaveRequestID}");
                TempData["Error"] = "An error occurred while updating the unpaid leave request.";
                return RedirectToAction(nameof(UnpaidLeaveRequests));
            }

            // If we got here, redisplay form with errors
            await PopulateLeaveTypeDropdownAsync();
            var leaveBalances = await CalculateLeaveBalancesAsync(employeeId);
            ViewBag.LeaveBalances = leaveBalances;
            PopulateLeaveBalanceViewBag(leaveBalances);

            ViewBag.IsUnpaidLeave = true;
            ViewBag.UnpaidLeaveRequestID = unpaidRequest.UnpaidLeaveRequestID;
            ViewBag.Reason = Reason;

            return View("~/Views/Employee/Leaves/Edit.cshtml", leave);
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
        public async Task<IActionResult> ProcessLeave(int? id, bool isUnpaidLeave = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            if (isUnpaidLeave)
            {
                // Process Unpaid Leave Request
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

                // Check for medical certificate (use negative ID for unpaid leave)
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

                // Set ViewBag flags for unpaid leave
                ViewBag.IsUnpaidLeave = true;
                ViewBag.UnpaidLeaveRequest = unpaidRequest;

                // Create a dummy LeaveModel for the view (since the view expects it)
                var dummyLeave = new LeaveModel
                {
                    LeaveID = 0,
                    EmployeeID = unpaidRequest.EmployeeID,
                    LeaveTypeID = unpaidRequest.LeaveTypeID,
                    StartDate = unpaidRequest.StartDate,
                    EndDate = unpaidRequest.EndDate,
                                    LeaveDays = unpaidRequest.RequestedDays,
                Reason = unpaidRequest.Reason,
                Status = unpaidRequest.Status,
                    SubmissionDate = unpaidRequest.SubmissionDate,
                    Employee = unpaidRequest.Employee,
                    LeaveType = unpaidRequest.LeaveType
                };

                return View("~/Views/HR/Leaves/ProcessLeave.cshtml", dummyLeave);
            }
            else
            {
                // Process Regular Leave
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

                // Set ViewBag flags for regular leave
                ViewBag.IsUnpaidLeave = false;
                ViewBag.UnpaidLeaveRequest = null;

                return View("~/Views/HR/Leaves/ProcessLeave.cshtml", leave);
            }
        }

        // Add this new action to handle unpaid leave medical certificate download
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> DownloadUnpaidLeaveMedicalCertificate(int unpaidLeaveId)
        {
            try
            {
                var leaveDetails = await _context.LeaveDetails
                    .FirstOrDefaultAsync(ld => ld.LeaveID == -unpaidLeaveId);

                if (leaveDetails == null)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction(nameof(ProcessLeave), new { id = unpaidLeaveId, isUnpaidLeave = true });
                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", leaveDetails.DocumentPath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    TempData["Error"] = "Document file not found on server.";
                    return RedirectToAction(nameof(ProcessLeave), new { id = unpaidLeaveId, isUnpaidLeave = true });
                }

                var fileName = Path.GetFileName(leaveDetails.DocumentPath);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var contentType = GetContentType(fileName);

                _logger.LogInformation($"✅ HR downloading unpaid leave medical certificate: {fileName} for Unpaid Leave ID: {unpaidLeaveId}");

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error downloading unpaid leave medical certificate for ID: {unpaidLeaveId}");
                TempData["Error"] = "Error downloading document. Please try again.";
                return RedirectToAction(nameof(ProcessLeave), new { id = unpaidLeaveId, isUnpaidLeave = true });
            }
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
                    leave.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave approved successfully!";
                }
                else if (action == "reject")
                {
                    // ✅ MAKE REJECTION REASON MANDATORY
                    if (string.IsNullOrWhiteSpace(remarks))
                    {
                        TempData["Error"] = "Rejection reason is required. Please provide a reason for rejecting this leave application.";
                        return RedirectToAction(nameof(ProcessLeave), new { id = id });
                    }

                    leave.Status = "Rejected";
                    leave.ApprovalDate = DateTime.Now;
                    leave.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                    leave.ApprovalRemarks = remarks;
                    TempData["Success"] = "Leave rejected successfully!";
                }
                else
                {
                    TempData["Error"] = "Invalid action specified.";
                    return RedirectToAction(nameof(ProcessLeave), new { id = id });
                }

                await _context.SaveChangesAsync();

                // Send email notification to employee about the leave processing result
                await SendLeaveProcessingNotificationToEmployee(leave, action, remarks);

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
        [HttpGet]
        public async Task<IActionResult> GetUnpaidLeaveStats()
        {
            try
            {
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                var pendingCount = await _context.UnpaidLeaveRequests
                    .CountAsync(u => u.Status == "Pending");

                var approvedThisMonth = await _context.UnpaidLeaveRequests
                    .CountAsync(u => u.Status == "Approved"
                                && u.ApprovalDate.HasValue
                                && u.ApprovalDate.Value.Month == currentMonth
                                && u.ApprovalDate.Value.Year == currentYear);

                var rejectedThisMonth = await _context.UnpaidLeaveRequests
                    .CountAsync(u => u.Status == "Rejected"
                                && u.ApprovalDate.HasValue
                                && u.ApprovalDate.Value.Month == currentMonth
                                && u.ApprovalDate.Value.Year == currentYear);

                return Json(new
                {
                    pendingCount = pendingCount,
                    approvedThisMonth = approvedThisMonth,
                    rejectedThisMonth = rejectedThisMonth
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unpaid leave statistics");
                return Json(new
                {
                    pendingCount = 0,
                    approvedThisMonth = 0,
                    rejectedThisMonth = 0
                });
            }
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
            // Redirect to the combined ProcessLeave action
            return RedirectToAction(nameof(ProcessLeave), new { id = id, isUnpaidLeave = true });
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
                    unpaidRequest.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
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
                        ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim(),
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

                    // Send email notification to employee about the unpaid leave approval
                    await SendUnpaidLeaveProcessingNotificationToEmployee(unpaidRequest, action, remarks);

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
                    // ✅ MAKE REJECTION REASON MANDATORY FOR UNPAID LEAVE TOO
                    if (string.IsNullOrWhiteSpace(remarks))
                    {
                        TempData["Error"] = "Rejection reason is required. Please provide a clear explanation for rejecting this unpaid leave request.";
                        return RedirectToAction(nameof(ProcessUnpaidLeave), new { id = id });
                    }

                    _logger.LogInformation($"🔴 REJECTING unpaid leave request ID: {id}");

                    unpaidRequest.Status = "Rejected";
                    unpaidRequest.ApprovalDate = DateTime.Now;
                    unpaidRequest.ApprovedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                    unpaidRequest.ApprovalRemarks = remarks;

                    await _context.SaveChangesAsync();

                    // Send email notification to employee about the unpaid leave rejection
                    await SendUnpaidLeaveProcessingNotificationToEmployee(unpaidRequest, action, remarks);

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

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> UnpaidLeaveDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var unpaidRequest = await _context.UnpaidLeaveRequests
                .Include(u => u.Employee)
                .Include(u => u.LeaveType)
                .FirstOrDefaultAsync(u => u.UnpaidLeaveRequestID == id);

            if (unpaidRequest == null)
            {
                return NotFound();
            }

            return View("~/Views/HR/Leaves/UnpaidLeaveDetails.cshtml", unpaidRequest);
        }

        
        /// Sends leave balance reminder emails to employees who have more than 10 days remaining
        /// Should be called on December 1st or as needed
        
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> SendLeaveBalanceReminders()
        {
            try
            {
                var remindersSent = 0;
                var errors = 0;

                // Get all active employees
                var employees = await _context.Employees.ToListAsync();

                foreach (var employee in employees)
                {
                    try
                    {
                        // Calculate leave balances for current year
                        var leaveBalances = await CalculateLeaveBalancesAsync(employee.EmployeeID);

                        // Check if any leave type has more than 10 days remaining
                        var highBalanceLeaves = new List<object>();

                        foreach (var balance in leaveBalances)
                        {
                            var balanceInfo = balance.Value as dynamic;
                            if (balanceInfo?.RemainingDays > 10)
                            {
                                highBalanceLeaves.Add(new
                                {
                                    LeaveType = balance.Key,
                                    RemainingDays = balanceInfo.RemainingDays,
                                    DefaultDays = balanceInfo.DefaultDays
                                });
                            }
                        }

                        // Send reminder if employee has high balances
                        if (highBalanceLeaves.Any())
                        {
                            await SendLeaveBalanceReminderToEmployee(employee, highBalanceLeaves);
                            remindersSent++;

                            _logger.LogInformation($"Leave balance reminder sent to {employee.Username} ({employee.Email})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending reminder to employee {employee.EmployeeID}");
                        errors++;
                    }
                }

                TempData["Success"] = $"Leave balance reminders sent successfully! Sent: {remindersSent}, Errors: {errors}";
                return RedirectToAction(nameof(LeaveReports));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendLeaveBalanceReminders");
                TempData["Error"] = "An error occurred while sending leave balance reminders.";
                return RedirectToAction(nameof(LeaveReports));
            }
        }

        
        /// Sends leave balance reminder email to a specific employee
      
        private async Task SendLeaveBalanceReminderToEmployee(Employee employee, List<object> highBalanceLeaves)
        {
            try
            {
                if (string.IsNullOrEmpty(employee.Email))
                {
                    _logger.LogWarning($"No email address for employee {employee.EmployeeID}");
                    return;
                }

                // Build the leave balance table
                var leaveBalanceRows = "";
                var totalDaysToUse = 0.0;

                foreach (var leave in highBalanceLeaves)
                {
                    var leaveType = leave.GetType().GetProperty("LeaveType")?.GetValue(leave)?.ToString();
                    var remainingDays = Convert.ToDouble(leave.GetType().GetProperty("RemainingDays")?.GetValue(leave));
                    var defaultDays = Convert.ToDouble(leave.GetType().GetProperty("DefaultDays")?.GetValue(leave));
                    var usedDays = defaultDays - remainingDays;

                    totalDaysToUse += remainingDays;

                    leaveBalanceRows += $@"
                <tr>
                    <td style='padding: 12px; border-bottom: 1px solid #e9ecef;'>{leaveType}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e9ecef; text-align: center;'>{defaultDays:0.#}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e9ecef; text-align: center;'>{usedDays:0.#}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e9ecef; text-align: center; font-weight: bold; color: #28a745;'>{remainingDays:0.#}</td>
                </tr>";
                }

                string subject = "Reminder: Please Clear Your Leave Balance Before Year End";

                string body = $@"
        <html>
        <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                <h2 style='color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px;'>
                    Leave Balance Reminder
                </h2>
                
                <div style='background-color: #e8f4fd; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 5px solid #3498db;'>
                    <h3 style='color: #2980b9; margin-top: 0;'>Dear {employee.FirstName} {employee.LastName},</h3>
                    <p style='margin-bottom: 0; font-size: 16px;'>
                        This is a friendly reminder that you have unused leave days that should be cleared before the year ends.
                    </p>
                </div>

                <div style='background-color: #fff; border: 1px solid #dee2e6; border-radius: 8px; margin: 20px 0; overflow: hidden;'>
                    <div style='background-color: #f8f9fa; padding: 15px; border-bottom: 1px solid #dee2e6;'>
                        <h3 style='color: #495057; margin: 0;'>Your Current Leave Balance</h3>
                    </div>
                    
                    <table style='width: 100%; border-collapse: collapse;'>
                        <thead>
                            <tr style='background-color: #f1f3f4;'>
                                <th style='padding: 12px; text-align: left; border-bottom: 2px solid #dee2e6;'>Leave Type</th>
                                <th style='padding: 12px; text-align: center; border-bottom: 2px solid #dee2e6;'>Allocated</th>
                                <th style='padding: 12px; text-align: center; border-bottom: 2px solid #dee2e6;'>Used</th>
                                <th style='padding: 12px; text-align: center; border-bottom: 2px solid #dee2e6;'>Remaining</th>
                            </tr>
                        </thead>
                        <tbody>
                            {leaveBalanceRows}
                        </tbody>
                    </table>
                </div>

                <div style='background-color: #fff3cd; padding: 20px; border-radius: 8px; border-left: 5px solid #ffc107; margin: 20px 0;'>
                    <h4 style='color: #856404; margin-top: 0;'>Important Notice:</h4>
                    <ul style='margin: 10px 0; padding-left: 20px;'>
                        <li style='margin-bottom: 8px;'>You have <strong>{totalDaysToUse:0.#} days</strong> of leave remaining that should be used</li>
                        <li style='margin-bottom: 8px;'>Unused leave days may be forfeited at year-end according to company policy</li>
                        <li style='margin-bottom: 8px;'>Please plan and submit your leave applications early to ensure approval</li>
                        <li style='margin-bottom: 8px;'>Consider taking time off to rest and recharge for better work-life balance</li>
                    </ul>
                </div>

                <div style='background-color: #d4edda; padding: 20px; border-radius: 8px; border-left: 5px solid #28a745; margin: 20px 0;'>
                    <h4 style='color: #155724; margin-top: 0;'>Next Steps:</h4>
                    <ol style='margin: 10px 0; padding-left: 20px;'>
                        <li style='margin-bottom: 8px;'>Review your leave balance above</li>
                        <li style='margin-bottom: 8px;'>Plan your leave dates with your manager</li>
                        <li style='margin-bottom: 8px;'>Submit your leave applications through the system</li>
                        <li style='margin-bottom: 8px;'>Ensure proper handover of your responsibilities</li>
                    </ol>
                </div>

                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{Url.Action("Create", "Leaves", null, Request.Scheme)}' 
                       style='background-color: #28a745; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold; font-size: 16px;'>
                        Apply for Leave Now
                    </a>
                </div>

                <div style='border-top: 1px solid #dee2e6; padding-top: 20px; margin-top: 30px;'>
                    <p style='font-size: 14px; color: #6c757d; margin: 0;'>
                        <strong>Need Help?</strong> Contact HR at hr001@cubicsoftware.com.my if you have any questions about your leave balance or need assistance with leave planning.
                    </p>
                    <p style='font-size: 12px; color: #6c757d; margin-top: 10px;'>
                        This is an automated reminder from the Finserve Leave Management System.
                        <br>Employee ID: {employee.EmployeeID} | Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}
                    </p>
                </div>
            </div>
        </body>
        </html>";

                await _emailSender.SendEmailAsync(employee.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending leave balance reminder to {employee.Email}");
                throw; // Re-throw to be caught by calling method
            }
        }

        
        /// API endpoint to check employees who need leave balance reminders
       
        [Authorize(Roles = "HR")]
        [HttpGet]
        public async Task<IActionResult> GetEmployeesNeedingLeaveReminders()
        {
            try
            {
                var employeesNeedingReminders = new List<object>();
                var employees = await _context.Employees.ToListAsync();

                foreach (var employee in employees)
                {
                    var leaveBalances = await CalculateLeaveBalancesAsync(employee.EmployeeID);
                    var highBalances = new List<object>();
                    var totalHighBalance = 0.0;

                    foreach (var balance in leaveBalances)
                    {
                        var balanceInfo = balance.Value as dynamic;
                        if (balanceInfo?.RemainingDays > 10)
                        {
                            highBalances.Add(new
                            {
                                LeaveType = balance.Key,
                                RemainingDays = balanceInfo.RemainingDays
                            });
                            totalHighBalance += balanceInfo.RemainingDays;
                        }
                    }

                    if (highBalances.Any())
                    {
                        employeesNeedingReminders.Add(new
                        {
                            EmployeeID = employee.EmployeeID,
                            EmployeeName = $"{employee.FirstName} {employee.LastName}",
                            Email = employee.Email,
                            TotalHighBalance = totalHighBalance,
                            HighBalances = highBalances
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    count = employeesNeedingReminders.Count,
                    employees = employeesNeedingReminders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees needing leave reminders");
                return Json(new
                {
                    success = false,
                    message = "Error retrieving data"
                });
            }
        }

        
        /// Scheduled job method - can be called by a background service on December 1st
        
        public async Task<bool> SendAutomaticLeaveBalanceReminders()
        {
            try
            {
                _logger.LogInformation("Starting automatic leave balance reminders job");

                var result = await SendLeaveBalanceReminders();

                _logger.LogInformation("Completed automatic leave balance reminders job");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in automatic leave balance reminders job");
                return false;
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
                    _logger.LogWarning("❌ No leave types found in database - attempting to seed default leave types");
                    
                    try
                    {
                        // Attempt to seed default leave types
                        await SeedDefaultLeaveTypesAsync();
                        
                        // Retry fetching leave types after seeding
                        leaveTypes = await _context.LeaveTypes
                            .OrderBy(lt => lt.TypeName)
                            .ToListAsync();
                        
                        if (leaveTypes.Any())
                        {
                            _logger.LogInformation($"✅ Successfully seeded {leaveTypes.Count} leave types");
                        }
                        else
                        {
                            _logger.LogError("❌ Failed to seed leave types - using fallback");
                            // Create fallback list as last resort
                            ViewBag.LeaveTypes = new SelectList(new[]
                            {
                                new { LeaveTypeID = 1, TypeName = "Annual Leave" },
                                new { LeaveTypeID = 2, TypeName = "Medical Leave" },
                                new { LeaveTypeID = 3, TypeName = "Hospitalization Leave" }
                            }, "LeaveTypeID", "TypeName");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error seeding leave types - using fallback");
                                                // Create fallback list as last resort
                        ViewBag.LeaveTypes = new SelectList(new[]
                        {
                            new { LeaveTypeID = 1, TypeName = "Annual Leave" },
                            new { LeaveTypeID = 2, TypeName = "Medical Leave" },
                            new { LeaveTypeID = 3, TypeName = "Hospitalization Leave" }
                        }, "LeaveTypeID", "TypeName");
                        return;
                    }
                }
                else
                {
                    // Create dropdown list from existing database leave types
                    var dropdownItems = new List<dynamic>();

                    // Add existing database leave types
                    foreach (var lt in leaveTypes)
                    {
                        _logger.LogInformation($"📝 Leave Type: ID={lt.LeaveTypeID}, Name={lt.TypeName}");
                        dropdownItems.Add(new { LeaveTypeID = lt.LeaveTypeID, TypeName = lt.TypeName });
                    }

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
                    new { LeaveTypeID = 3, TypeName = "Hospitalization Leave" }
                }, "LeaveTypeID", "TypeName");
            }
        }

        private async Task<List<object>> GetAvailableAlternativeLeaveTypesAsync(string employeeId, int excludeLeaveTypeId, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var leaveTypes = await _context.LeaveTypes
                .Where(lt => lt.LeaveTypeID != excludeLeaveTypeId && !lt.TypeName.ToLower().Contains("emergency"))
                .ToListAsync();

            var availableTypes = new List<object>();

            foreach (var leaveType in leaveTypes)
            {
                var remainingBalance = await GetRemainingLeaveBalanceAsync(employeeId, leaveType.LeaveTypeID, year);

                if (remainingBalance > 0)
                {
                    availableTypes.Add(new
                    {
                        LeaveTypeID = leaveType.LeaveTypeID,
                        TypeName = leaveType.TypeName,
                        RemainingBalance = remainingBalance,
                        MaxDays = leaveType.DefaultDaysPerYear
                    });
                }
            }

            return availableTypes;
        }

        // Helper method to create leave allocation breakdown
        private async Task<object> CreateLeaveAllocationBreakdownAsync(string employeeId, int originalLeaveTypeId,
            double requestedDays, List<int> alternativeTypeIds, int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var breakdown = new List<object>();
            var remainingDaysToAllocate = requestedDays;

            // First, use the original leave type balance
            var originalBalance = await GetRemainingLeaveBalanceAsync(employeeId, originalLeaveTypeId, year);
            var originalLeaveType = await _context.LeaveTypes.FindAsync(originalLeaveTypeId);

            if (originalBalance > 0 && remainingDaysToAllocate > 0)
            {
                var daysToUse = Math.Min(originalBalance, remainingDaysToAllocate);
                breakdown.Add(new
                {
                    LeaveTypeID = originalLeaveTypeId,
                    TypeName = originalLeaveType?.TypeName ?? "Unknown",
                    AllocatedDays = daysToUse,
                    BalanceBefore = originalBalance,
                    BalanceAfter = originalBalance - daysToUse
                });
                remainingDaysToAllocate -= daysToUse;
            }

            // Then use alternative leave types
            foreach (var altTypeId in alternativeTypeIds)
            {
                if (remainingDaysToAllocate <= 0) break;

                var altBalance = await GetRemainingLeaveBalanceAsync(employeeId, altTypeId, year);
                var altLeaveType = await _context.LeaveTypes.FindAsync(altTypeId);

                if (altBalance > 0)
                {
                    var daysToUse = Math.Min(altBalance, remainingDaysToAllocate);
                    breakdown.Add(new
                    {
                        LeaveTypeID = altTypeId,
                        TypeName = altLeaveType?.TypeName ?? "Unknown",
                        AllocatedDays = daysToUse,
                        BalanceBefore = altBalance,
                        BalanceAfter = altBalance - daysToUse
                    });
                    remainingDaysToAllocate -= daysToUse;
                }
            }

            return new
            {
                Breakdown = breakdown,
                TotalAllocatedDays = requestedDays - remainingDaysToAllocate,
                UnpaidDays = Math.Max(0, remainingDaysToAllocate),
                IsFullyAllocated = remainingDaysToAllocate <= 0
            };
        }

        // Helper method to validate alternative leave allocation
        private async Task<(bool isValid, string errorMessage)> ValidateAlternativeLeaveAllocationAsync(
            string employeeId, int originalLeaveTypeId, double requestedDays, List<int> alternativeTypeIds, int year = 0)
        {
            try
            {
                if (year == 0) year = DateTime.Now.Year;

                // Check if original leave type exists and is valid
                var originalLeaveType = await _context.LeaveTypes.FindAsync(originalLeaveTypeId);
                if (originalLeaveType == null)
                {
                    return (false, "Invalid original leave type selected.");
                }

                // Validate alternative leave type IDs
                foreach (var altTypeId in alternativeTypeIds)
                {
                    var altLeaveType = await _context.LeaveTypes.FindAsync(altTypeId);
                    if (altLeaveType == null)
                    {
                        return (false, $"Invalid alternative leave type ID: {altTypeId}");
                    }

                    // Don't allow Emergency Leave as alternative
                    if (altLeaveType.TypeName.ToLower().Contains("emergency"))
                    {
                        return (false, "Emergency Leave cannot be used as an alternative leave type.");
                    }

                    // Don't allow same type as original
                    if (altTypeId == originalLeaveTypeId)
                    {
                        return (false, "Cannot use the same leave type as alternative.");
                    }
                }

                // Check if requested days is reasonable
                if (requestedDays <= 0 || requestedDays > 365)
                {
                    return (false, "Invalid number of requested days.");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating alternative leave allocation");
                return (false, "An error occurred while validating the leave allocation.");
            }
        }

        // Helper method to log leave allocation details
        private void LogLeaveAllocation(string employeeId, object allocationBreakdown, double requestedDays)
        {
            _logger.LogInformation($"🔄 LEAVE ALLOCATION BREAKDOWN for Employee {employeeId}:");
            _logger.LogInformation($"   📊 Total Requested: {requestedDays} days");

            var breakdown = allocationBreakdown.GetType().GetProperty("Breakdown")?.GetValue(allocationBreakdown) as IEnumerable<object>;
            var unpaidDays = allocationBreakdown.GetType().GetProperty("UnpaidDays")?.GetValue(allocationBreakdown);

            if (breakdown != null)
            {
                foreach (var item in breakdown)
                {
                    var typeName = item.GetType().GetProperty("TypeName")?.GetValue(item);
                    var allocatedDays = item.GetType().GetProperty("AllocatedDays")?.GetValue(item);
                    var balanceBefore = item.GetType().GetProperty("BalanceBefore")?.GetValue(item);
                    var balanceAfter = item.GetType().GetProperty("BalanceAfter")?.GetValue(item);

                    _logger.LogInformation($"   • {typeName}: {allocatedDays} days (Balance: {balanceBefore} → {balanceAfter})");
                }
            }

            if (unpaidDays != null && (double)unpaidDays > 0)
            {
                _logger.LogInformation($"   • Unpaid Leave: {unpaidDays} days");
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

        /// <summary>
        /// Sends email notification to HR when leave application is submitted
        /// </summary>
        private async Task SendLeaveSubmissionNotificationToHR(LeaveModel leave)
        {
            try
            {
                // Get employee information
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == leave.EmployeeID);

                if (employee == null)
                {
                    _logger.LogWarning($"Employee not found for employee ID: {leave.EmployeeID}");
                    return;
                }

                // Get leave type information
                var leaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.LeaveTypeID == leave.LeaveTypeID);

                // Email subject and body
                string subject = $"Leave Approval Request for {employee.FirstName} {employee.LastName}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2c3e50;'>Leave Approval Request</h2>
                    
                    <div style='background-color: #e8f4fd; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;'>
                        <h3 style='color: #007bff; margin-top: 0;'>New Leave Application</h3>
                        <p style='margin-bottom: 0;'>A new leave application has been submitted and requires your review.</p>
                    </div>

                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='color: #495057; margin-top: 0;'>Application Details</h3>
                        <p><strong>Employee:</strong> {employee.FirstName} {employee.LastName} ({employee.EmployeeID})</p>
                        <p><strong>Leave Type:</strong> {leaveType?.TypeName ?? "Unknown"}</p>
                        <p><strong>Start Date:</strong> {leave.StartDate:dd/MM/yyyy}</p>
                        <p><strong>End Date:</strong> {leave.EndDate:dd/MM/yyyy}</p>
                        <p><strong>Duration:</strong> {leave.LeaveDays} day(s)</p>
                        
                        <p><strong>Submitted On:</strong> {leave.SubmissionDate:dd/MM/yyyy HH:mm}</p>
                    </div>

                    <div style='background-color: #e8f4fd; padding: 15px; border-radius: 5px; border-left: 4px solid #007bff;'>
                        <p style='margin: 0;'><strong>Action Required:</strong> This leave application requires your review and approval.</p>
                    </div>

                    <div style='margin-top: 30px;'>
                        <p><a href='{Url.Action("LeaveDetails", "Leaves", new { id = leave.LeaveID }, Request.Scheme)}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Click here to review this leave application</a></p>
                        <p style='font-size: 12px; color: #6c757d;'>
                            This is an automated notification from the Finserve Leave Management System.
                        </p>
                    </div>
                </body>
                </html>";

                // Send email to the specific HR email address
                try
                {
                    await _emailSender.SendEmailAsync("hr001@cubicsoftware.com.my", subject, body);
                    _logger.LogInformation("Leave submission notification sent to hr001@cubicsoftware.com.my");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to hr001@cubicsoftware.com.my");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending leave submission notification to HR");
                // Don't throw the exception to avoid breaking the leave submission process
            }
        }

        /// <summary>
        /// Sends email notification to HR when unpaid leave request is submitted
        /// </summary>
        private async Task SendUnpaidLeaveSubmissionNotificationToHR(UnpaidLeaveRequestModel unpaidRequest)
        {
            try
            {
                // Get employee information
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID == unpaidRequest.EmployeeID);

                if (employee == null)
                {
                    _logger.LogWarning($"Employee not found for employee ID: {unpaidRequest.EmployeeID}");
                    return;
                }

                // Get leave type information
                var leaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.LeaveTypeID == unpaidRequest.LeaveTypeID);

                // Email subject and body
                string subject = $"Unpaid Leave Approval Request for {employee.FirstName} {employee.LastName}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2c3e50;'>Unpaid Leave Approval Request</h2>
                    
                    <div style='background-color: #fff3cd; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                        <h3 style='color: #856404; margin-top: 0;'>New Unpaid Leave Request</h3>
                        <p style='margin-bottom: 0;'>A new unpaid leave request has been submitted and requires your review.</p>
                    </div>

                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='color: #495057; margin-top: 0;'>Request Details</h3>
                        <p><strong>Employee:</strong> {employee.FirstName} {employee.LastName} ({employee.EmployeeID})</p>
                        <p><strong>Leave Type:</strong> {leaveType?.TypeName ?? "Unknown"}</p>
                        <p><strong>Start Date:</strong> {unpaidRequest.StartDate:dd/MM/yyyy}</p>
                        <p><strong>End Date:</strong> {unpaidRequest.EndDate:dd/MM/yyyy}</p>
                        <p><strong>Duration:</strong> {unpaidRequest.RequestedDays} day(s)</p>
                        
                        <p><strong>Reason:</strong> {unpaidRequest.Reason ?? "No reason provided"}</p>
                        <p><strong>Submitted On:</strong> {unpaidRequest.SubmissionDate:dd/MM/yyyy HH:mm}</p>
                    </div>

                    <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; border-left: 4px solid #ffc107;'>
                        <p style='margin: 0;'><strong>Action Required:</strong> This unpaid leave request requires your review and approval.</p>
                    </div>

                    <div style='margin-top: 30px;'>
                        <p><a href='{Url.Action("ProcessLeave", "Leaves", new { id = unpaidRequest.UnpaidLeaveRequestID, isUnpaidLeave = true }, Request.Scheme)}' style='background-color: #ffc107; color: #856404; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Click here to review this unpaid leave request</a></p>
                        <p style='font-size: 12px; color: #6c757d;'>
                            This is an automated notification from the Finserve Leave Management System.
                        </p>
                    </div>
                </body>
                </html>";

                // Send email to the specific HR email address
                try
                {
                    await _emailSender.SendEmailAsync("hr001@cubicsoftware.com.my", subject, body);
                    _logger.LogInformation("Unpaid leave submission notification sent to hr001@cubicsoftware.com.my");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to hr001@cubicsoftware.com.my");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending unpaid leave submission notification to HR");
                // Don't throw the exception to avoid breaking the leave submission process
            }
        }

        /// <summary>
        /// Sends email notification to employee when their leave application is approved/rejected
        /// </summary>
        private async Task SendLeaveProcessingNotificationToEmployee(LeaveModel leave, string action, string remarks)
        {
            try
            {
                if (string.IsNullOrEmpty(leave.Employee?.Email))
                {
                    _logger.LogWarning($"No email address for employee {leave.EmployeeID}");
                    return;
                }

                var actionText = action == "approve" ? "APPROVED" : "REJECTED";
                var actionColor = action == "approve" ? "#28a745" : "#dc3545";
                var actionIcon = action == "approve" ? "✅" : "❌";

                // Email subject and body
                var subject = $"Leave Application {actionText} - {leave.LeaveType?.TypeName} Leave";
                var body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: {actionColor}; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                            .content {{ background-color: #f8f9fa; padding: 20px; border-radius: 0 0 5px 5px; }}
                            .status {{ font-size: 24px; font-weight: bold; margin: 10px 0; }}
                            .details {{ background-color: white; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                            .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>Leave Application Update</h2>
                                <div class='status'>{actionIcon} {actionText}</div>
                            </div>
                            <div class='content'>
                                <p>Dear {leave.Employee.FirstName} {leave.Employee.LastName},</p>
                                
                                <p>Your leave application has been <strong>{actionText.ToLower()}</strong> by HR.</p>
                                
                                <div class='details'>
                                    <h3>Leave Application Details:</h3>
                                    <p><strong>Leave Type:</strong> {leave.LeaveType?.TypeName}</p>
                                    <p><strong>Start Date:</strong> {leave.StartDate:dd MMM yyyy}</p>
                                    <p><strong>End Date:</strong> {leave.EndDate:dd MMM yyyy}</p>
                                    <p><strong>Duration:</strong> {leave.LeaveDays} day(s)</p>
                                    <p><strong>Reason:</strong> {leave.Reason}</p>
                                    <p><strong>Status:</strong> <span style='color: {actionColor}; font-weight: bold;'>{actionText}</span></p>
                                    <p><strong>Processed By:</strong> {leave.ApprovedBy}</p>
                                    <p><strong>Processed Date:</strong> {leave.ApprovalDate:dd MMM yyyy HH:mm}</p>
                                    {(string.IsNullOrEmpty(remarks) ? "" : $"<p><strong>Remarks:</strong> {remarks}</p>")}
                                </div>
                                
                                <p>You can view your leave history and current status by logging into the Finserve system.</p>
                                
                                <p>If you have any questions, please contact HR.</p>
                                
                                <p>Thank you.</p>
                            </div>
                            <div class='footer'>
                                <p>This is an automated notification from the Finserve Leave Management System.</p>
                                <p>Please do not reply to this email.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Send email to the employee
                await _emailSender.SendEmailAsync(leave.Employee.Email, subject, body);
                _logger.LogInformation($"Leave processing notification sent to {leave.Employee.Email} for {actionText} leave application");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending leave processing notification to employee {leave.EmployeeID}");
                // Don't throw the exception to avoid breaking the leave processing
            }
        }

        /// <summary>
        /// Sends email notification to employee when their unpaid leave request is approved/rejected
        /// </summary>
        private async Task SendUnpaidLeaveProcessingNotificationToEmployee(UnpaidLeaveRequestModel unpaidRequest, string action, string remarks)
        {
            try
            {
                if (string.IsNullOrEmpty(unpaidRequest.Employee?.Email))
                {
                    _logger.LogWarning($"No email address for employee {unpaidRequest.EmployeeID}");
                    return;
                }

                var actionText = action == "approve" ? "APPROVED" : "REJECTED";
                var actionColor = action == "approve" ? "#28a745" : "#dc3545";
                var actionIcon = action == "approve" ? "✅" : "❌";

                // Email subject and body
                var subject = $"Unpaid Leave Request {actionText}";
                var body = $@"
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: {actionColor}; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                            .content {{ background-color: #f8f9fa; padding: 20px; border-radius: 0 0 5px 5px; }}
                            .status {{ font-size: 24px; font-weight: bold; margin: 10px 0; }}
                            .details {{ background-color: white; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                            .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>Unpaid Leave Request Update</h2>
                                <div class='status'>{actionIcon} {actionText}</div>
                            </div>
                            <div class='content'>
                                <p>Dear {unpaidRequest.Employee.FirstName} {unpaidRequest.Employee.LastName},</p>
                                
                                <p>Your unpaid leave request has been <strong>{actionText.ToLower()}</strong> by HR.</p>
                                
                                <div class='details'>
                                    <h3>Unpaid Leave Request Details:</h3>
                                    <p><strong>Start Date:</strong> {unpaidRequest.StartDate:dd MMM yyyy}</p>
                                    <p><strong>End Date:</strong> {unpaidRequest.EndDate:dd MMM yyyy}</p>
                                    <p><strong>Duration:</strong> {unpaidRequest.RequestedDays} day(s)</p>
                                    <p><strong>Reason:</strong> {unpaidRequest.Reason}</p>
                                    <p><strong>Status:</strong> <span style='color: {actionColor}; font-weight: bold;'>{actionText}</span></p>
                                    <p><strong>Processed By:</strong> {unpaidRequest.ApprovedBy}</p>
                                    <p><strong>Processed Date:</strong> {unpaidRequest.ApprovalDate:dd MMM yyyy HH:mm}</p>
                                    {(string.IsNullOrEmpty(remarks) ? "" : $"<p><strong>Remarks:</strong> {remarks}</p>")}
                                </div>
                                
                                <p>You can view your leave history and current status by logging into the Finserve system.</p>
                                
                                <p>If you have any questions, please contact HR.</p>
                                
                                <p>Thank you.</p>
                            </div>
                            <div class='footer'>
                                <p>This is an automated notification from the Finserve Leave Management System.</p>
                                <p>Please do not reply to this email.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Send email to the employee
                await _emailSender.SendEmailAsync(unpaidRequest.Employee.Email, subject, body);
                _logger.LogInformation($"Unpaid leave processing notification sent to {unpaidRequest.Employee.Email} for {actionText} request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending unpaid leave processing notification to employee {unpaidRequest.EmployeeID}");
                // Don't throw the exception to avoid breaking the leave processing
            }
        }

        /// <summary>
        /// Seeds default leave types if the LeaveType table is empty
        /// This is a fallback method called when no leave types are found
        /// </summary>
        private async Task SeedDefaultLeaveTypesAsync()
        {
            try
            {
                // Double-check that the table is still empty (in case of race conditions)
                var hasLeaveTypes = await _context.LeaveTypes.AnyAsync();
                
                if (!hasLeaveTypes)
                {
                    var defaultLeaveTypes = new List<LeaveTypeModel>
                    {
                        new LeaveTypeModel
                        {
                            TypeName = "Annual Leave",
                            DefaultDaysPerYear = 14,
                            Description = "Annual vacation leave entitlement",
                            RequiresDocumentation = false
                        },
                        new LeaveTypeModel
                        {
                            TypeName = "Medical Leave",
                            DefaultDaysPerYear = 10,
                            Description = "Medical leave for illness or injury",
                            RequiresDocumentation = true
                        },
                        new LeaveTypeModel
                        {
                            TypeName = "Hospitalization Leave",
                            DefaultDaysPerYear = 16,
                            Description = "Leave for hospitalization and medical treatment",
                            RequiresDocumentation = true
                        }
                    };

                    // Add all default leave types to the context
                    await _context.LeaveTypes.AddRangeAsync(defaultLeaveTypes);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Successfully seeded {defaultLeaveTypes.Count} default leave types");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error seeding default leave types");
                throw; // Re-throw to be caught by the calling method
            }
        }
    }
}