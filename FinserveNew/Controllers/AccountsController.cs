using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using ISO3166;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FinserveNew.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(
            AppDbContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountsController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _logger = logger;
        }

        // --------------------------- HR Side ----------------------------- //
        // GET: Accounts/AllAccounts
        [Authorize(Roles = "HR, Senior HR")]
        public async Task<IActionResult> AllAccounts(
            int page = 1,
            int pageSize = 10,
            string? search = null,
            string? position = null,
            string? status = null,
            DateTime? joinDateStart = null,
            DateTime? joinDateEnd = null)
        {
            var query = _context.Employees
                .Include(e => e.Role)
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .AsQueryable();

            // Search logic
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    e.FirstName.Contains(search) ||
                    e.LastName.Contains(search) ||
                    e.Position.Contains(search) ||
                    e.EmployeeID.Contains(search)
                );
            }

            // Position filter
            if (!string.IsNullOrWhiteSpace(position))
            {
                query = query.Where(e => e.Position == position);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.ConfirmationStatus == status);
            }

            // Join date range filter - convert DateTime to DateOnly
            if (joinDateStart.HasValue)
            {
                var startDate = DateOnly.FromDateTime(joinDateStart.Value);
                query = query.Where(e => e.JoinDate >= startDate);
            }

            if (joinDateEnd.HasValue)
            {
                var endDate = DateOnly.FromDateTime(joinDateEnd.Value);
                query = query.Where(e => e.JoinDate <= endDate);
            }

            var totalRecords = await query.CountAsync();
            var employees = await query
                .OrderBy(e => e.EmployeeID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pass pagination info and filter values to the view
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.PageSizes = new[] { 10, 25, 50, 100 };
            ViewBag.Search = search;
            ViewBag.Position = position;
            ViewBag.Status = status;
            ViewBag.JoinDateStart = joinDateStart;
            ViewBag.JoinDateEnd = joinDateEnd;

            return View("~/Views/HR/Accounts/AllAccounts.cshtml", employees);
        }

        // GET: Accounts/ViewDetails/5
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> ViewDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Role)
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .Include(e => e.EmployeeDocuments)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null)
                return NotFound();

            // Get the user's system role
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeID == id);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
            }

            // Get bank names from JSON
            var banks = GetBanksFromJson();
            var bankNames = banks.Select(b => b["name"]).ToArray();

            var viewModel = new EmployeeDetailsViewModel
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
                // Security check for ViewDetails (HR view) - populate RoleName only for display
                RoleName = employee.Role?.RoleName ?? "Unknown", // Only for display

                Documents = employee.EmployeeDocuments?.ToList() ?? new List<EmployeeDocument>()
            };

            // Use consistent method for populating dropdown data
            PopulateEmployeeDetailsDropdowns(viewModel);

            return View("~/Views/HR/Accounts/ViewDetails.cshtml", viewModel); 
        }

        // GET: Accounts/Add
        [Authorize(Roles = "HR")]
        [HttpGet]
        public async Task<IActionResult> AddAsync()
        {
            var roles = await _context.Roles.ToListAsync();

            // Get all countries using ISO3166 library
            var countries = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray();

            // Get bank names from JSON
            var banks = GetBanksFromJson();
            var bankNames = banks.Select(b => b["name"]).ToArray();

            var vm = new AddEmployeeViewModel
            {
                Nationalities = countries,
                BankNames = bankNames,
                BankTypes = new[] { "Savings", "Current" },
                
                // Set default values
                Nationality = "Malaysia", // Default nationality to Malaysia
                JoinDate = DateOnly.FromDateTime(DateTime.Today), // Default join date to today

                AvailableRoles = roles.Select(r => new SelectListItem
                {
                    Value = r.RoleID.ToString(),
                    Text = r.RoleName
                }).ToList()
            };
            return View("~/Views/HR/Accounts/Add.cshtml", vm);
        }

        // POST: Accounts/Add
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddEmployeeViewModel vm)
        {
            // Additional business rules validation
            if (!ValidateEmployeeBusinessRules(vm))
            {
                await PopulateViewModelDropdowns(vm);
                return View("~/Views/HR/Accounts/Add.cshtml", vm);
            }

            // Uniqueness checks
            // Validate based on nationality
            if (vm.Nationality == "Malaysia")
            {
                if (string.IsNullOrEmpty(vm.IC))
                {
                    ModelState.AddModelError("IC", "IC Number is required for Malaysian citizens.");
                }
                else if (await _context.Employees.AnyAsync(e => e.IC == vm.IC))
                {
                    ModelState.AddModelError("IC", "IC already exists.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(vm.PassportNumber))
                {
                    ModelState.AddModelError("PassportNumber", "Passport Number is required for non-Malaysian citizens.");
                }
                else if (await _context.Employees.AnyAsync(e => e.PassportNumber == vm.PassportNumber && !string.IsNullOrEmpty(e.PassportNumber)))
                {
                    ModelState.AddModelError("PassportNumber", "Passport Number already exists.");
                }
            }

            // Check for duplicate email
            if (await _context.Employees.AnyAsync(e => e.Email == vm.Email))
                ModelState.AddModelError("Email", "Email already exists.");

            // Check for duplicate phone number
            if (await _context.Employees.AnyAsync(e => e.TelephoneNumber == vm.TelephoneNumber))
                ModelState.AddModelError("TelephoneNumber", "Phone number already exists.");

            // Check for duplicate EPF number
            if (await _context.Employees.AnyAsync(e => e.EPFNumber == vm.EPFNumber))
                ModelState.AddModelError("EPFNumber", "EPF number already exists.");

            // Check for duplicate Income Tax number
            if (await _context.Employees.AnyAsync(e => e.IncomeTaxNumber == vm.IncomeTaxNumber))
                ModelState.AddModelError("IncomeTaxNumber", "Income Tax number already exists.");

            // Check for duplicate bank account number (within same bank)
            if (await _context.BankInformations.AnyAsync(b => b.BankName == vm.BankName && b.BankAccountNumber == vm.BankAccountNumber))
                ModelState.AddModelError("BankAccountNumber", "Bank account number already exists for this bank.");

            if (!ModelState.IsValid)
            {
                await PopulateViewModelDropdowns(vm);
                return View("~/Views/HR/Accounts/Add.cshtml", vm);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get the selected role to determine employee ID format
                var selectedRole = await _context.Roles.FindAsync(vm.RoleID);
                string roleName = selectedRole?.RoleName.ToLower() ?? "";
                string idPrefix;
                string newEmployeeId;

                // Determine ID prefix based on the role
                if (roleName.Contains("hr") || roleName.Contains("human resource"))
                {
                    idPrefix = "HR";

                    // Find the last HR employee ID
                    var lastHREmployee = await _context.Employees
                        .Where(e => e.EmployeeID.StartsWith("HR"))
                        .OrderByDescending(e => e.EmployeeID)
                        .FirstOrDefaultAsync();

                    if (lastHREmployee != null)
                    {
                        int lastNumber = int.Parse(lastHREmployee.EmployeeID.Substring(2));
                        newEmployeeId = $"HR{(lastNumber + 1):D3}";
                    }
                    else
                    {
                        newEmployeeId = "HR001";
                    }
                }
                else if (roleName.Contains("admin") || roleName.Contains("director") ||
                 roleName.Contains("manager") || roleName.Contains("ceo"))
                {
                    idPrefix = "ADMIN";

                    // Find the last ADMIN employee ID
                    var lastAdminEmployee = await _context.Employees
                        .Where(e => e.EmployeeID.StartsWith("ADMIN"))
                        .OrderByDescending(e => e.EmployeeID)
                        .FirstOrDefaultAsync();

                    if (lastAdminEmployee != null)
                    {
                        int lastNumber = int.Parse(lastAdminEmployee.EmployeeID.Substring(5));
                        newEmployeeId = $"ADMIN{(lastNumber + 1):D3}";
                    }
                    else
                    {
                        newEmployeeId = "ADMIN001";
                    }
                }
                else
                {
                    idPrefix = "EM";

                    // Find the last EM employee ID
                    var lastEmployee = await _context.Employees
                        .Where(e => e.EmployeeID.StartsWith("EM"))
                        .OrderByDescending(e => e.EmployeeID)
                        .FirstOrDefaultAsync();

                    if (lastEmployee != null)
                    {
                        int lastNumber = int.Parse(lastEmployee.EmployeeID.Substring(2));
                        newEmployeeId = $"EM{(lastNumber + 1):D3}";
                    }
                    else
                    {
                        newEmployeeId = "EM001";
                    }
                }
                              
                string username = newEmployeeId;

                string basePasswordPart = vm.FirstName.Replace(" ", "").ToLower();
                string rawPassword = $"{basePasswordPart}#1234";
                rawPassword = char.ToUpper(rawPassword[0]) + rawPassword.Substring(1);

                // Create Bank Information (Don't save yet - keep in transaction)
                var bankInfo = new BankInformation
                {
                    BankName = vm.BankName,
                    BankType = vm.BankType,
                    BankAccountNumber = vm.BankAccountNumber
                };
                _context.BankInformations.Add(bankInfo);

                // Create Emergency Contact (Don't save yet - keep in transaction)
                var emergencyContact = new EmergencyContact
                {
                    Name = vm.EmergencyContactName,
                    TelephoneNumber = vm.EmergencyContactPhone,
                    Relationship = vm.EmergencyContactRelationship
                };
                _context.EmergencyContacts.Add(emergencyContact);

                // Create Employee (Don't save yet - keep in transaction)
                var employee = new Employee
                {
                    EmployeeID = newEmployeeId,
                    Username = username,
                    Password = HashPassword(rawPassword),
                    FirstName = vm.FirstName,
                    LastName = vm.LastName,
                    IC = vm.Nationality == "Malaysia" ? vm.IC : null,
                    PassportNumber = vm.Nationality != "Malaysia" ? vm.PassportNumber : null,
                    Nationality = vm.Nationality,
                    Email = vm.Email,
                    TelephoneNumber = vm.TelephoneNumber,
                    DateOfBirth = vm.DateOfBirth,
                    JoinDate = vm.JoinDate,
                    ConfirmationStatus = vm.ConfirmationStatus,
                    Position = vm.Position,
                    IncomeTaxNumber = vm.IncomeTaxNumber,
                    EPFNumber = vm.EPFNumber,
                    BankInformation = bankInfo, // Use navigation property instead of ID
                    EmergencyContact = emergencyContact, // Use navigation property instead of ID
                    RoleID = vm.RoleID
                };
                _context.Employees.Add(employee);

                // Store uploaded files info for later processing (Don't save files yet)
                var documentsToAdd = new List<(IFormFile file, string docType, string employeeId)>();

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
                            if (allowedExtensions.Contains(ext) && file.Length <= 5 * 1024 * 1024)
                            {
                                documentsToAdd.Add((file, docType, employee.EmployeeID));
                            }
                        }
                    }
                }

                // Create ASP.NET Identity user account
                var user = new ApplicationUser
                {
                    UserName = newEmployeeId,
                    Email = vm.Email,
                    EmailConfirmed = true,
                    FirstName = vm.FirstName,
                    LastName = vm.LastName,
                    EmployeeID = newEmployeeId
                };

                var result = await _userManager.CreateAsync(user, rawPassword);

                if (result.Succeeded)
                {
                    // Determine system role based on selected role name
                    string systemRole = "Employee"; // Default

                    // Logic to determine which system role to assign based on the employee role
                    if (roleName.Contains("senior hr") || roleName.Contains("senior human resource"))
                    {
                        systemRole = "Senior HR";
                    }
                    else if (roleName.Contains("hr") || roleName.Contains("human resource"))
                    {
                        systemRole = "HR";
                    }
                    else if (idPrefix == "ADMIN")
                    {
                        systemRole = "Admin";
                    }

                    // Add user to the appropriate system role
                    var roleResult = await _userManager.AddToRoleAsync(user, systemRole);
                    if (!roleResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        var roleErrorMessage = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        _logger.LogError($"Failed to assign role {systemRole} to user: {roleErrorMessage}");
                        ModelState.AddModelError(string.Empty, $"User account created but role assignment failed: {roleErrorMessage}");
                        await PopulateViewModelDropdowns(vm);
                        return View("~/Views/HR/Accounts/Add.cshtml", vm);
                    }

                    // Save all database changes together
                    await _context.SaveChangesAsync();

                    // Check if we just created an HR user and handle default account deactivation
                    if (systemRole == "HR" || systemRole == "Senior HR")
                    {
                        await DbInitializer.CheckAndDeactivateDefaultAccountAsync(HttpContext.RequestServices);
                    }

                    // Process and save documents only after all database operations succeed
                    var savedDocuments = new List<string>(); // Track saved files for cleanup if needed
                    try
                    {
                        foreach (var (file, docType, employeeId) in documentsToAdd)
                        {
                            var filePath = await SaveFile(file, "documents", docType);
                            savedDocuments.Add(filePath);
                            
                            var document = new EmployeeDocument
                            {
                                EmployeeID = employeeId,
                                DocumentType = docType,
                                FilePath = filePath,
                                FileName = file.FileName,
                                UploadDate = DateTime.UtcNow
                            };
                            _context.EmployeeDocuments.Add(document);
                        }

                        // Save document records
                        if (documentsToAdd.Any())
                        {
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();

                        TempData["Success"] = $"Employee {vm.FirstName} {vm.LastName} added successfully with {systemRole} system access and ID {newEmployeeId}!";
                        _logger.LogInformation($"Employee {newEmployeeId} created with {systemRole} role and user account {vm.Email}");
                    }
                    catch (Exception docEx)
                    {
                        // Clean up any files that were saved before the error
                        foreach (var filePath in savedDocuments)
                        {
                            try
                            {
                                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, filePath.TrimStart('/'));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogError(cleanupEx, "Failed to cleanup file {FilePath}", filePath);
                            }
                        }

                        await transaction.RollbackAsync();
                        _logger.LogError(docEx, "Error saving documents for employee {EmployeeID}", newEmployeeId);
                        ModelState.AddModelError(string.Empty, "Employee and user account created but document upload failed. Please try uploading documents later.");
                        await PopulateViewModelDropdowns(vm);
                        return View("~/Views/HR/Accounts/Add.cshtml", vm);
                    }
                }
                else
                {
                    // Roll back if user creation fails
                    await transaction.RollbackAsync();

                    var errorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to create user account: {errorMessage}");

                    ModelState.AddModelError(string.Empty, $"Employee record was created but user account creation failed: {errorMessage}");

                    await PopulateViewModelDropdowns(vm);
                    return View("~/Views/HR/Accounts/Add.cshtml", vm);
                }
                return RedirectToAction(nameof(AllAccounts));
            }
            catch (Exception ex)
            {
                // Roll back transaction if anything fails
                await transaction.RollbackAsync();

                _logger.LogError(ex, "Error creating employee and user account");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the employee. Please try again.");

                await PopulateViewModelDropdowns(vm);
                return View("~/Views/HR/Accounts/Add.cshtml", vm);
            }
        }

        // POST: Accounts/UpdateEmployee
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployee(EmployeeDetailsViewModel vm, bool confirmTermination = false)
        {
            if (!ModelState.IsValid)
            {
                PopulateEmployeeDetailsDropdowns(vm);
                                
                // Log the errors to the console
                foreach (var modelStateEntry in ModelState.Values)
                {
                    foreach (var error in modelStateEntry.Errors)
                    {
                        // You can also use ILogger here
                        _logger.LogError("Validation Error: {ErrorMessage}", error.ErrorMessage);
                    }
                }

                return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
            }

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == vm.EmployeeID);

            if (employee == null)
                return NotFound();

            // Store original values to check for changes
            string originalEmail = employee.Email;
            string originalConfirmationStatus = employee.ConfirmationStatus;

            // Check if confirmation status is being changed to "Terminated"
            bool isBeingTerminated = originalConfirmationStatus != "Terminated" && vm.ConfirmationStatus == "Terminated";
            
            if (isBeingTerminated && !confirmTermination)
            {
                // Store the form data in TempData for resubmission after confirmation
                TempData["PendingEmployeeUpdate"] = System.Text.Json.JsonSerializer.Serialize(vm);
                TempData["TerminationWarning"] = $"You are about to terminate employee {vm.FirstName} {vm.LastName} (ID: {vm.EmployeeID}). This will deactivate their system account and they will no longer be able to access the system.";
                TempData["RequireConfirmation"] = true;
                
                PopulateEmployeeDetailsDropdowns(vm);
                return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update employee details
                employee.FirstName = vm.FirstName;
                employee.LastName = vm.LastName;
                if (vm.Nationality == "Malaysia")
                {
                    employee.IC = vm.IC;
                    employee.PassportNumber = null;
                }
                else
                {
                    employee.IC = null;
                    employee.PassportNumber = vm.PassportNumber;
                }
                employee.Nationality = vm.Nationality;
                employee.Email = vm.Email;
                employee.TelephoneNumber = vm.TelephoneNumber;
                employee.DateOfBirth = vm.DateOfBirth;
                employee.JoinDate = vm.JoinDate;
                employee.ResignationDate = vm.ResignationDate;
                employee.ConfirmationStatus = vm.ConfirmationStatus;
                employee.Position = vm.Position;
                employee.IncomeTaxNumber = vm.IncomeTaxNumber;
                employee.EPFNumber = vm.EPFNumber;

                // Update bank information
                if (employee.BankInformation != null)
                {
                    employee.BankInformation.BankName = vm.BankName;
                    employee.BankInformation.BankType = vm.BankType;
                    employee.BankInformation.BankAccountNumber = vm.BankAccountNumber;
                }

                // Update emergency contact
                if (employee.EmergencyContact != null)
                {
                    employee.EmergencyContact.Name = vm.EmergencyContactName;
                    employee.EmergencyContact.TelephoneNumber = vm.EmergencyContactPhone;
                    employee.EmergencyContact.Relationship = vm.EmergencyContactRelationship;
                }

                // Prepare document uploads (Don't save files yet)
                var documentsToAdd = new List<(IFormFile file, string docType, string employeeId)>();

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
                            if (allowedExtensions.Contains(ext) && file.Length <= 5 * 1024 * 1024)
                            {
                                documentsToAdd.Add((file, docType, vm.EmployeeID));
                            }
                        }
                    }
                }

                // Save employee changes first
                await _context.SaveChangesAsync();

                // Handle account deactivation if employee is being terminated
                if (isBeingTerminated)
                {
                    var user = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeID == vm.EmployeeID);
                    if (user != null)
                    {
                        // Deactivate the user account
                        user.IsDeactivated = true;
                        user.DeactivatedAt = DateTime.UtcNow;
                        user.DeactivationReason = $"Account deactivated due to employee termination by HR user {User.Identity.Name}";
                        
                        var deactivationResult = await _userManager.UpdateAsync(user);
                        if (!deactivationResult.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError("Failed to deactivate user account for terminated employee {EmployeeID}: {Errors}", 
                                vm.EmployeeID, string.Join(", ", deactivationResult.Errors.Select(e => e.Description)));
                            
                            ModelState.AddModelError(string.Empty, "Employee was updated but failed to deactivate user account. Please contact system administrator.");
                            PopulateEmployeeDetailsDropdowns(vm);
                            return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
                        }
                        else
                        {
                            _logger.LogInformation("Successfully deactivated user account for terminated employee {EmployeeID} by {HRUser}", 
                                vm.EmployeeID, User.Identity.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find ASP.NET Identity user account for terminated employee {EmployeeID}", vm.EmployeeID);
                        // Continue without failing - the employee record is still terminated
                    }
                }

                // Update ASP.NET Identity user email if it changed
                if (originalEmail != vm.Email)
                {
                    var user = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeID == vm.EmployeeID);
                    if (user != null)
                    {
                        user.Email = vm.Email;
                        user.UserName = vm.Email;
                        user.NormalizedEmail = vm.Email.ToUpper();
                        user.NormalizedUserName = vm.Email.ToUpper();

                        var updateResult = await _userManager.UpdateAsync(user);
                        if (!updateResult.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError("Failed to update user email: {Errors}", 
                                string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                            
                            ModelState.AddModelError(string.Empty, "Failed to update user email. Please try again.");
                            PopulateEmployeeDetailsDropdowns(vm);
                            return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
                        }
                        else
                        {
                            _logger.LogInformation("Successfully updated email for user {EmployeeID} from {OldEmail} to {NewEmail}", 
                                vm.EmployeeID, originalEmail, vm.Email);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find ASP.NET Identity user for employee {EmployeeID}", vm.EmployeeID);
                        // Continue without failing - this is not critical
                    }
                }

                // Process and save documents only after all database operations succeed
                var savedDocuments = new List<string>(); // Track saved files for cleanup if needed
                try
                {
                    foreach (var (file, docType, employeeId) in documentsToAdd)
                    {
                        var filePath = await SaveFile(file, "documents", docType);
                        savedDocuments.Add(filePath);
                        
                        var document = new EmployeeDocument
                        {
                            EmployeeID = employeeId,
                            DocumentType = docType,
                            FilePath = filePath,
                            FileName = file.FileName,
                            UploadDate = DateTime.UtcNow
                        };
                        _context.EmployeeDocuments.Add(document);
                    }

                    // Save document records
                    if (documentsToAdd.Any())
                    {
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    // Clear any pending update data from TempData
                    TempData.Remove("PendingEmployeeUpdate");
                    TempData.Remove("TerminationWarning");
                    TempData.Remove("RequireConfirmation");

                    // Set appropriate success message
                    if (isBeingTerminated)
                    {
                        TempData["Success"] = $"Employee {vm.FirstName} {vm.LastName} has been terminated and their account has been deactivated successfully.";
                    }
                    else
                    {
                        TempData["Success"] = "Employee updated successfully!";
                    }

                    return RedirectToAction(nameof(ViewDetails), new { id = vm.EmployeeID });
                }
                catch (Exception docEx)
                {
                    // Clean up any files that were saved before the error
                    foreach (var filePath in savedDocuments)
                    {
                        try
                        {
                            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, filePath.TrimStart('/'));
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogError(cleanupEx, "Failed to cleanup file {FilePath}", filePath);
                        }
                    }

                    await transaction.RollbackAsync();
                    _logger.LogError(docEx, "Error saving documents for employee {EmployeeID}", vm.EmployeeID);
                    ModelState.AddModelError(string.Empty, "Employee updated but document upload failed. Please try uploading documents later.");
                    PopulateEmployeeDetailsDropdowns(vm);
                    return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                if (!EmployeeExists(vm.EmployeeID))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating employee {EmployeeID}", vm.EmployeeID);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the employee. Please try again.");
                PopulateEmployeeDetailsDropdowns(vm);
                return View("~/Views/HR/Accounts/ViewDetails.cshtml", vm);
            }
        }

        // POST: Accounts/ConfirmTermination - Handle the confirmation of employee termination
        [Authorize(Roles = "HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTermination()
        {
            // Retrieve the pending employee update data from TempData
            var pendingUpdateJson = TempData["PendingEmployeeUpdate"] as string;
            if (string.IsNullOrEmpty(pendingUpdateJson))
            {
                TempData["Error"] = "No pending termination found. Please try again.";
                return RedirectToAction(nameof(AllAccounts));
            }

            try
            {
                var vm = System.Text.Json.JsonSerializer.Deserialize<EmployeeDetailsViewModel>(pendingUpdateJson);
                if (vm == null)
                {
                    TempData["Error"] = "Invalid termination data. Please try again.";
                    return RedirectToAction(nameof(AllAccounts));
                }

                // Clear TempData flags
                TempData.Remove("TerminationWarning");
                TempData.Remove("RequireConfirmation");

                // Resubmit with confirmation
                return await UpdateEmployee(vm, confirmTermination: true);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize pending employee termination data");
                TempData["Error"] = "Failed to process termination confirmation. Please try again.";
                return RedirectToAction(nameof(AllAccounts));
            }
        }
              


        // ------------- HELPER METHOD ----------------- //
        // Helper method to get bank names from JSON file
        private List<Dictionary<string, string>> GetBanksFromJson()
        {
            try
            {
                // Define path to banks.json file
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, "json", "banks.json");

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Banks JSON file not found at {FilePath}", filePath);
                    return new List<Dictionary<string, string>>();
                }

                // Read file content
                string jsonString = System.IO.File.ReadAllText(filePath);

                // Deserialize JSON
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var banks = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonString, options);

                // Sort alphabetically by name and return
                return banks?.OrderBy(b => b.ContainsKey("name") ? b["name"] : "").ToList() ?? new List<Dictionary<string, string>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading banks from JSON");
                return new List<Dictionary<string, string>>();
            }
        }

        // ------------- BUSINESS LOGIC ----------------- //
        // Business rules validation for employee addition
        private bool ValidateEmployeeBusinessRules(AddEmployeeViewModel vm)
        {
            var isValid = true;

            // Age validation
            if (!vm.IsValidAge())
            {
                ModelState.AddModelError("DateOfBirth", "Employee must be between 18 and 80 years old.");
                isValid = false;
            }

            // Join date validation
            if (!vm.IsValidJoinDate())
            {
                ModelState.AddModelError("JoinDate", "Join date cannot be more than 7 days in the future or more than 50 years in the past.");
                isValid = false;
            }

            // Identification validation
            if (!vm.IsValidIdentification())
            {
                if (vm.Nationality == "Malaysia" || vm.Nationality == "Malaysian")
                {
                    ModelState.AddModelError("IC", "Malaysian citizens must provide IC number only.");
                    ModelState.AddModelError("PassportNumber", "Please leave passport number empty for Malaysian citizens.");
                }
                else
                {
                    ModelState.AddModelError("PassportNumber", "Non-Malaysian citizens must provide passport number only.");
                    ModelState.AddModelError("IC", "Please leave IC number empty for non-Malaysian citizens.");
                }
                isValid = false;
            }

            // Emergency contact validation - ensure it's not the same as employee
            if (vm.EmergencyContactPhone == vm.TelephoneNumber)
            {
                ModelState.AddModelError("EmergencyContactPhone", "Emergency contact phone number cannot be the same as employee's phone number.");
                isValid = false;
            }

            // Document validation
            if (vm.NewDocuments != null && vm.NewDocumentTypes != null)
            {
                if (vm.NewDocuments.Count != vm.NewDocumentTypes.Count)
                {
                    ModelState.AddModelError("NewDocuments", "Please select document type for each document.");
                    isValid = false;
                }

                for (int i = 0; i < vm.NewDocuments.Count; i++)
                {
                    var file = vm.NewDocuments[i];
                    if (file != null)
                    {
                        // File size validation (5MB max)
                        if (file.Length > 5 * 1024 * 1024)
                        {
                            ModelState.AddModelError($"NewDocuments[{i}]", "File size cannot exceed 5MB.");
                            isValid = false;
                        }

                        // File type validation
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                        var ext = Path.GetExtension(file.FileName).ToLower();
                        if (!allowedExtensions.Contains(ext))
                        {
                            ModelState.AddModelError($"NewDocuments[{i}]", "Only PDF, JPG, PNG, DOC, and DOCX files are allowed.");
                            isValid = false;
                        }
                    }
                }
            }

            return isValid;
        }

        // Populate dropdowns and other necessary data for the view model
        private async Task PopulateViewModelDropdowns(AddEmployeeViewModel vm)
        {
            var roles = await _context.Roles.ToListAsync();
            vm.AvailableRoles = roles.Select(r => new SelectListItem
            {
                Value = r.RoleID.ToString(),
                Text = r.RoleName
            }).ToList();

            vm.Nationalities = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            
            var banks = GetBanksFromJson();
            vm.BankNames = banks.Select(b => b.ContainsKey("name") ? b["name"] : "").Where(name => !string.IsNullOrEmpty(name)).ToArray();
            vm.BankTypes = new[] { "Savings", "Current" };
            
            // Preserve default values if not set
            if (string.IsNullOrEmpty(vm.Nationality))
                vm.Nationality = "Malaysia";
            if (vm.JoinDate == DateOnly.MinValue)
                vm.JoinDate = DateOnly.FromDateTime(DateTime.Today);
        }

        // Helper method to populate dropdown data for EmployeeDetailsViewModel
        private void PopulateEmployeeDetailsDropdowns(EmployeeDetailsViewModel vm)
        {
            vm.Nationalities = ISO3166.Country.List.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            
            var banks = GetBanksFromJson();
            vm.BankNames = banks.Select(b => b.ContainsKey("name") ? b["name"] : "").Where(name => !string.IsNullOrEmpty(name)).ToArray();
            vm.BankTypes = new[] { "Savings", "Current" };
        }

        // Helper method to handle rollback scenarios and ensure all dropdown data is properly restored
        private async Task HandleAddEmployeeRollback(AddEmployeeViewModel vm, string errorMessage, string logMessage = null)
        {
            if (!string.IsNullOrEmpty(logMessage))
            {
                _logger.LogError(logMessage);
            }

            ModelState.AddModelError(string.Empty, errorMessage);
            await PopulateViewModelDropdowns(vm);
        }

        // Helper method to clean up uploaded files in case of transaction rollback
        private void CleanupUploadedFiles(List<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, filePath.TrimStart('/'));
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            _logger.LogInformation("Cleaned up file: {FilePath}", fullPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cleanup file {FilePath}", filePath);
                }
            }
        }

        // Helper method to validate file before processing
        private bool IsValidFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "File is empty or null";
                return false;
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            
            if (!allowedExtensions.Contains(ext))
            {
                errorMessage = $"File type {ext} is not allowed";
                return false;
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB
            {
                errorMessage = "File size exceeds 5MB limit";
                return false;
            }

            return true;
        }

        // POST: Accounts/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            var document = await _context.EmployeeDocuments.FindAsync(documentId);
            if (document == null)
                return NotFound();

            // Delete the physical file
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.EmployeeDocuments.Remove(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted successfully!";
            return RedirectToAction(nameof(ViewDetails), new { id = document.EmployeeID });
        }

        // Helper method to save uploaded files
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

        private void AddEmployeeDocumentToContext(string employeeId, string documentType, string filePath, string fileName)
        {
            var document = new EmployeeDocument
            {
                EmployeeID = employeeId,
                DocumentType = documentType,
                FilePath = filePath,
                FileName = fileName,
                UploadDate = DateTime.UtcNow
            };

            _context.EmployeeDocuments.Add(document);
            // Note: Does not call SaveChangesAsync - must be saved within transaction context
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool EmployeeExists(string id)
        {
            return _context.Employees.Any(e => e.EmployeeID == id);
        }
    }
}
