using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;

namespace FinserveNew.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger,
            AppDbContext context,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User {Email} logged in.", model.Email);

                        //await _signInManager.RefreshSignInAsync(user);

                        // Redirect based on user role
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        return await RedirectBasedOnRole(user);
                    }
                    else if (result.IsLockedOut)
                    {
                        ModelState.AddModelError(string.Empty, "Account is locked out.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task<IActionResult> RedirectBasedOnRole(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Admin"))
            {
                return RedirectToAction("AdminDashboard", "Home");
            }
            else if (roles.Contains("Senior HR"))
            {
                return RedirectToAction("SeniorHRDashboard", "Home");
            }
            else if (roles.Contains("HR"))
            {
                return RedirectToAction("HRDashboard", "Home");
            }
            else if (roles.Contains("Employee"))
            {
                return RedirectToAction("EmployeeDashboard", "Home");
            }

            // Default fallback
            return RedirectToAction("Index", "Home");
        }


        // ------------------- FORGOT PASSWORD ------------------- // 
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find the user by email
            var user = await _userManager.FindByEmailAsync(model.Email);

            // Don't reveal that the user does not exist
            if (user == null)
            {
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // Generate password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Encode and send in URL
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Build the reset URL
            var callbackUrl = Url.Action(
                action: "ResetPassword",
                controller: "Account",
                values: new { email = model.Email, token = encodedToken },
                protocol: Request.Scheme);

            // Use your existing email service to send the email
            await _context.SaveChangesAsync();

            // Use the existing SmtpEmailSender
            var emailSender = HttpContext.RequestServices.GetRequiredService<IEmailSender>();
            await emailSender.SendEmailAsync(
                model.Email,
                "Reset Your Password",
                $@"<h1>Reset Your Password</h1>
        <p>Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.</p>
        <p>If you didn't request this, please ignore this email.</p>");

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email = null, string token = null)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                return BadRequest("A reset token and email are required for password reset.");
            }

            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            // Decode token from URL
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));

            // Reset the password using ASP.NET Identity
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
            if (result.Succeeded)
            {
                // Log the successful password reset
                _logger.LogInformation($"Password reset successful for user {user.Id}");

                // If there's an Employee record with this email, update the password hash there too
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == model.Email);
                if (employee != null)
                {
                    employee.Password = _userManager.PasswordHasher.HashPassword(user, model.Password);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // -------------- PROFILE RELATED METHODS -------------- //
        // GET: Account/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var employee = await _context.Employees
                .Include(e => e.Role)
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .Include(e => e.EmployeeDocuments)
                .FirstOrDefaultAsync(e => e.EmployeeID == user.EmployeeID);

            if (employee == null)
                return NotFound();

            // Get bank names from JSON
            var banks = GetBanksFromJson();
            var bankNames = banks.Select(b => b["name"]).ToArray();

            var vm = new EmployeeDetailsViewModel
            {
                EmployeeID = employee.EmployeeID,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                IC = employee.IC ?? string.Empty,
                PassportNumber = employee.PassportNumber ?? string.Empty,
                Nationality = employee.Nationality,
                Email = employee.Email,
                TelephoneNumber = employee.TelephoneNumber,
                DateOfBirth = employee.DateOfBirth,
                JoinDate = employee.JoinDate,
                ResignationDate = employee.ResignationDate,
                ConfirmationStatus = employee.ConfirmationStatus,
                Position = employee.Position,
                IncomeTaxNumber = employee.IncomeTaxNumber,
                EPFNumber = employee.EPFNumber,
                BankName = employee.BankInformation?.BankName ?? string.Empty,
                BankType = employee.BankInformation?.BankType ?? string.Empty,
                BankAccountNumber = employee.BankInformation?.BankAccountNumber ?? string.Empty,
                EmergencyContactName = employee.EmergencyContact?.Name ?? string.Empty,
                EmergencyContactPhone = employee.EmergencyContact?.TelephoneNumber ?? string.Empty,
                EmergencyContactRelationship = employee.EmergencyContact?.Relationship ?? string.Empty,
                RoleID = employee.RoleID,
                RoleName = employee.Role?.RoleName ?? "Unknown",
                Documents = employee.EmployeeDocuments?.ToList() ?? new List<EmployeeDocument>(),
                Nationalities = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray(),
                BankNames = bankNames,
                BankTypes = new[] { "Savings", "Current" },
            };

            return View("~/Views/Account/Profile.cshtml", vm);
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EmployeeDetailsViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.EmployeeID != vm.EmployeeID)
                return Unauthorized();

            var banks = GetBanksFromJson();

            if (!ModelState.IsValid)
            {
                vm.Nationalities = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
                vm.BankNames = banks.Select(b => b["name"]).ToArray();
                vm.BankTypes = new[] { "Savings", "Current" };

                return View("~/Views/Account/Profile.cshtml", vm);
            }

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == vm.EmployeeID);

            if (employee == null)
                return NotFound();

            // Enforce nationality-specific ID rules and required passport for non-Malaysian
            if (vm.Nationality == "Malaysia")
            {
                // Malaysian: IC required, passport cleared
                employee.IC = vm.IC;
                employee.PassportNumber = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(vm.PassportNumber))
                {
                    ModelState.AddModelError("PassportNumber", "Passport number is required for non-Malaysian nationality.");
                    vm.Nationalities = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
                    vm.BankNames = banks.Select(b => b["name"]).ToArray();
                    vm.BankTypes = new[] { "Savings", "Current" };
                    return View("~/Views/Account/Profile.cshtml", vm);
                }
                employee.IC = null;
                employee.PassportNumber = vm.PassportNumber;
            }

            // Update allowed fields
            employee.FirstName = vm.FirstName;
            employee.LastName = vm.LastName;
            employee.Nationality = vm.Nationality;
            employee.TelephoneNumber = vm.TelephoneNumber;
            employee.DateOfBirth = vm.DateOfBirth;
            employee.IncomeTaxNumber = vm.IncomeTaxNumber;
            employee.EPFNumber = vm.EPFNumber;
            //employee.Email = vm.Email;

            if (employee.BankInformation != null)
            {
                employee.BankInformation.BankName = vm.BankName;
                employee.BankInformation.BankType = vm.BankType;
                employee.BankInformation.BankAccountNumber = vm.BankAccountNumber;
            }

            if (employee.EmergencyContact != null)
            {
                employee.EmergencyContact.Name = vm.EmergencyContactName;
                employee.EmergencyContact.TelephoneNumber = vm.EmergencyContactPhone;
                employee.EmergencyContact.Relationship = vm.EmergencyContactRelationship;
            }

            // Handle multi-file document uploads from the profile page (optional)
            if (vm.NewDocuments != null && vm.NewDocumentTypes != null && vm.NewDocuments.Count == vm.NewDocumentTypes.Count)
            {
                for (int i = 0; i < vm.NewDocuments.Count; i++)
                {
                    var file = vm.NewDocuments[i];
                    var docType = vm.NewDocumentTypes[i];

                    if (file != null && !string.IsNullOrEmpty(docType))
                    {
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var ext = Path.GetExtension(file.FileName).ToLower();
                        if (!allowedExtensions.Contains(ext) || file.Length > 5 * 1024 * 1024)
                            continue;

                        var filePath = await SaveFile(file, "documents", docType);
                        await AddEmployeeDocument(vm.EmployeeID, docType, filePath, file.FileName);
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction(nameof(Profile));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(vm.EmployeeID))
                    return NotFound();
                else
                    throw;
            }
        }

        private bool EmployeeExists(string id)
        {
            return _context.Employees.Any(e => e.EmployeeID == id);
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                return Json(new { success = false, message = "All fields are required" });
            }

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "New password and confirmation don't match" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!changePasswordResult.Succeeded)
            {
                string errorMessage = "Failed to change password";

                if (changePasswordResult.Errors.Any())
                {
                    var firstError = changePasswordResult.Errors.First();
                    errorMessage = firstError.Description;
                }

                return Json(new { success = false, message = errorMessage });
            }

            _logger.LogInformation("User {UserId} changed their password successfully", user.Id);
            return Json(new { success = true, message = "Password changed successfully!" });
        }

        // Helper method to get bank names from JSON file
        private List<Dictionary<string, string>> GetBanksFromJson()
        {
            try
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, "json", "banks.json");
                if (!System.IO.File.Exists(filePath))
                    return new List<Dictionary<string, string>>();

                string jsonString = System.IO.File.ReadAllText(filePath);

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var banks = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonString, options);
                banks = banks?.OrderBy(b => b["name"]).ToList() ?? new List<Dictionary<string, string>>();
                return banks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading banks from JSON");
                return new List<Dictionary<string, string>>();
            }
        }

        // ---------------- Documents (Profile) ---------------- //
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var document = await _context.EmployeeDocuments.FindAsync(documentId);
            if (document == null)
                return NotFound();

            // ensure the document belongs to the current user
            if (document.EmployeeID != user.EmployeeID)
                return Forbid();

            // Delete the physical file
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.EmployeeDocuments.Remove(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully!";
            return RedirectToAction(nameof(Profile), new { tab = "#documents" });
        }

        private async Task<string> SaveFile(IFormFile file, string baseFolder, string documentType)
        {
            if (file == null || file.Length == 0)
                return null;

            // Convert document type to a valid folder name (lowercase, replace spaces with hyphens)
            string folderName = documentType.ToLower().Replace(" ", "-").Replace("/", "-");

            // Create path structure: baseFolder/documentType/
            string folderPath = Path.Combine(baseFolder, folderName);
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, folderPath);

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Create unique filename
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return the web-accessible path
            return $"/{folderPath}/{uniqueFileName}";
        }

        private async Task AddEmployeeDocument(string employeeId, string documentType, string filePath, string fileName)
        {
            var document = new EmployeeDocument
            {
                EmployeeID = employeeId,
                DocumentType = documentType,
                FilePath = filePath,
                FileName = fileName
            };

            _context.EmployeeDocuments.Add(document);
            await _context.SaveChangesAsync();
        }
    }
}