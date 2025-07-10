using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using System.Threading.Tasks;
using FinserveNew.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace FinserveNew.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountsController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Accounts/AllAccounts
        public async Task<IActionResult> AllAccounts(int page = 1, int pageSize = 10, string? search = null)
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

            var totalRecords = await query.CountAsync();
            var employees = await query
                .OrderBy(e => e.EmployeeID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pass pagination info to the view
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.PageSizes = new[] { 10, 25, 50, 100 };
            ViewBag.Search = search;

            return View("~/Views/HR/Accounts/AllAccounts.cshtml", employees); // winnie changed path here -- remove it once u saw haha
        }

        // GET: Accounts/ViewDetails/5
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

            var viewModel = new EmployeeDetailsViewModel
            {
                EmployeeID = employee.EmployeeID,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                IC = employee.IC,
                Nationality = employee.Nationality,
                Email = employee.Email,
                TelephoneNumber = employee.TelephoneNumber,
                DateOfBirth = employee.DateOfBirth,
                JoinDate = employee.JoinDate,
                ResignationDate = employee.ResignationDate,
                ConfirmationStatus = employee.ConfirmationStatus,
                Position = employee.Position,
                BankName = employee.BankInformation?.BankName ?? string.Empty,
                BankType = employee.BankInformation?.BankType ?? string.Empty,
                BankAccountNumber = employee.BankInformation?.BankAccountNumber ?? string.Empty,
                EmergencyContactName = employee.EmergencyContact?.Name ?? string.Empty,
                EmergencyContactPhone = employee.EmergencyContact?.TelephoneNumber ?? string.Empty,
                EmergencyContactRelationship = employee.EmergencyContact?.Relationship ?? string.Empty,
                // Documents = employee.EmployeeDocuments?.Select(d => new DocumentViewModel
                // {
                //     DocumentID = d.DocumentID,
                //     DocumentType = d.DocumentType,
                //     FilePath = d.FilePath,
                //     //UploadDate = d.UploadDate
                // }).ToList() ?? new List<DocumentViewModel>(),
                Documents = employee.EmployeeDocuments?.ToList() ?? new List<EmployeeDocument>(),
                Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" },
                BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" },
                BankTypes = new[] { "Savings", "Current" }
            };

            return View("~/Views/HR/Accounts/ViewDetails.cshtml", viewModel); // winnie changed path here -- remove it once u saw haha
        }

        // GET: Accounts/Add
        [HttpGet]
        public IActionResult Add()
        {
            var vm = new AddEmployeeViewModel
            {
                Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" },
                BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" },
                BankTypes = new[] { "Savings", "Current" }
            };
            return View("~/Views/HR/Accounts/Add.cshtml", vm);
        }

        // POST: Accounts/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddEmployeeViewModel vm)
        {
            // Uniqueness checks
            if (await _context.Employees.AnyAsync(e => e.IC == vm.IC))
                ModelState.AddModelError("IC", "IC already exists.");
            if (await _context.Employees.AnyAsync(e => e.Email == vm.Email))
                ModelState.AddModelError("Email", "Email already exists.");
            //if (await _context.Employees.AnyAsync(e => e.Username == vm.Username))
            //    ModelState.AddModelError("Username", "Username already exists.");

            if (!ModelState.IsValid)
            {
                vm.Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" };
                vm.BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" };
                vm.BankTypes = new[] { "Savings", "Current" };
                return View("~/Views/HR/Accounts/Add.cshtml",vm);
            }

            // Generate EmployeeID
            var lastEmployee = await _context.Employees
                .OrderByDescending(e => e.EmployeeID)
                .FirstOrDefaultAsync();
            
            string newEmployeeId = "EM001";
            if (lastEmployee != null)
            {
                var lastNumber = int.Parse(lastEmployee.EmployeeID.Substring(2));
                newEmployeeId = $"EM{(lastNumber + 1):D3}";
            }

            string username = newEmployeeId;
            string rawPassword = $"{vm.FirstName.Trim().ToLower()}#1234";

            //// Generate BankID
            //var lastBank = await _context.BankInformations
            //    .OrderByDescending(b => b.BankID)
            //    .FirstOrDefaultAsync();

            //string newBankID = "B001";
            //if (lastBank != null)
            //{
            //    //var lastNumber = int.Parse(lastBank.BankID.Substring(1));
            //    //newBankID = $"B{(lastNumber + 1):D3}";
            //    var lastNumber = lastBank.BankID; // currently the id is put as 1, havnt give proper format
            //    newBankID = $"B{(lastNumber + 1):D3}";
            //}

            //// Generate EmergencyID
            //var lastContact = await _context.EmergencyContacts
            //    .OrderByDescending(ec => ec.EmergencyID)
            //    .FirstOrDefaultAsync();

            //string newEmergencyID = "EC001";
            //if (lastContact != null)
            //{
            //    //var lastNumber = int.Parse(lastBank.BankID.Substring(1));
            //    //newBankID = $"B{(lastNumber + 1):D3}";
            //    var lastNumber = lastContact.EmergencyID;
            //    newEmergencyID = $"EC{(lastNumber + 1):D3}";
            //}

            // Create Bank Information
            var bankInfo = new BankInformation
            {
                BankName = vm.BankName,
                BankType = vm.BankType,
                BankAccountNumber = vm.BankAccountNumber
            };
            _context.BankInformations.Add(bankInfo);
            await _context.SaveChangesAsync();

            // Create Emergency Contact
            var emergencyContact = new EmergencyContact
            {
                Name = vm.EmergencyContactName,
                TelephoneNumber = vm.EmergencyContactPhone,
                Relationship = vm.EmergencyContactRelationship
            };
            _context.EmergencyContacts.Add(emergencyContact);
            await _context.SaveChangesAsync();

            // Create Employee
            var employee = new Employee
            {
                EmployeeID = newEmployeeId,
                Username = username,
                Password = HashPassword(rawPassword),
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                IC = vm.IC,
                Nationality = vm.Nationality,
                Email = vm.Email,
                TelephoneNumber = vm.TelephoneNumber,
                DateOfBirth = vm.DateOfBirth,
                JoinDate = vm.JoinDate,
                //ResignationDate = vm.ResignationDate,
                ConfirmationStatus = vm.ConfirmationStatus,
                Position = vm.Position,
                BankID = bankInfo.BankID,
                EmergencyID = emergencyContact.EmergencyID,
                RoleID = 1 // Default role for new employees
            };
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // Handle file uploads
            if (vm.ICFile != null)
            {
                var icPath = await SaveFile(vm.ICFile, "ic_photos");
                await AddEmployeeDocument(employee.EmployeeID, "IC Photo", icPath);
            }

            if (vm.ResumeFile != null)
            {
                var resumePath = await SaveFile(vm.ResumeFile, "resumes");
                await AddEmployeeDocument(employee.EmployeeID, "Resume", resumePath);
            }

            if (vm.OfferLetterFile != null)
            {
                var offerLetterPath = await SaveFile(vm.OfferLetterFile, "offer_letters");
                await AddEmployeeDocument(employee.EmployeeID, "Offer Letter", offerLetterPath);
            }

            TempData["Success"] = $"Employee {vm.FirstName} {vm.LastName} added successfully!";
            return RedirectToAction(nameof(AllAccounts));
        }

        // GET: Accounts/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null)
                return NotFound();

            var vm = new EditEmployeeViewModel
            {
                EmployeeID = employee.EmployeeID,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                IC = employee.IC,
                Nationality = employee.Nationality,
                Email = employee.Email,
                TelephoneNumber = employee.TelephoneNumber,
                DateOfBirth = employee.DateOfBirth,
                JoinDate = employee.JoinDate,
                ResignationDate = employee.ResignationDate,
                ConfirmationStatus = employee.ConfirmationStatus,
                Position = employee.Position,
                BankName = employee.BankInformation?.BankName,
                BankType = employee.BankInformation?.BankType,
                BankAccountNumber = employee.BankInformation?.BankAccountNumber,
                EmergencyContactName = employee.EmergencyContact?.Name,
                EmergencyContactPhone = employee.EmergencyContact?.TelephoneNumber,
                EmergencyContactRelationship = employee.EmergencyContact?.Relationship,
                Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" },
                BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" },
                BankTypes = new[] { "Savings", "Current" }
            };

            return View(vm);
        }

        // POST: Accounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditEmployeeViewModel vm)
        {
            if (id != vm.EmployeeID)
                return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" };
                vm.BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" };
                vm.BankTypes = new[] { "Savings", "Current" };
                return View(vm);
            }

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null)
                return NotFound();

            // Update employee details
            employee.FirstName = vm.FirstName;
            employee.LastName = vm.LastName;
            employee.IC = vm.IC;
            employee.Nationality = vm.Nationality;
            employee.Email = vm.Email;
            employee.TelephoneNumber = vm.TelephoneNumber;
            employee.DateOfBirth = vm.DateOfBirth;
            employee.JoinDate = vm.JoinDate;
            employee.ResignationDate = vm.ResignationDate;
            employee.ConfirmationStatus = vm.ConfirmationStatus;
            employee.Position = vm.Position;

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

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Employee updated successfully!";
                return RedirectToAction(nameof(AllAccounts));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id))
                    return NotFound();
                else
                    throw;
            }
        }

        // GET: Accounts/Documents/5
        public async Task<IActionResult> Documents(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.EmployeeDocuments)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // POST: Accounts/Documents/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(string id, IFormFile file, string documentType)
        {
            if (string.IsNullOrEmpty(id) || file == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            var filePath = await SaveFile(file, "documents");
            await AddEmployeeDocument(id, documentType, filePath);

            TempData["Success"] = "Document uploaded successfully!";
            return RedirectToAction(nameof(Documents), new { id });
        }

        // POST: Accounts/UpdateEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployee(EmployeeDetailsViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" };
                vm.BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" };
                vm.BankTypes = new[] { "Savings", "Current" };
                return View("ViewDetails", vm);
            }

            var employee = await _context.Employees
                .Include(e => e.BankInformation)
                .Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == vm.EmployeeID);

            if (employee == null)
                return NotFound();

            // Update employee details
            employee.FirstName = vm.FirstName;
            employee.LastName = vm.LastName;
            employee.IC = vm.IC;
            employee.Nationality = vm.Nationality;
            employee.Email = vm.Email;
            employee.TelephoneNumber = vm.TelephoneNumber;
            employee.DateOfBirth = vm.DateOfBirth;
            employee.JoinDate = vm.JoinDate;
            employee.ResignationDate = vm.ResignationDate;
            employee.ConfirmationStatus = vm.ConfirmationStatus;
            employee.Position = vm.Position;

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

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Employee updated successfully!";
                return RedirectToAction(nameof(ViewDetails), new { id = vm.EmployeeID });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(vm.EmployeeID))
                    return NotFound();
                else
                    throw;
            }
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

        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, folder);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/" + folder + "/" + uniqueFileName;
        }

        private async Task AddEmployeeDocument(string employeeId, string documentType, string filePath)
        {
            var document = new EmployeeDocument
            {
                EmployeeID = employeeId,
                DocumentType = documentType,
                FilePath = filePath,
                // UploadDate = DateTime.UtcNow
            };

            _context.EmployeeDocuments.Add(document);
            await _context.SaveChangesAsync();
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
