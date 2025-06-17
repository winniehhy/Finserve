using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using System.Threading.Tasks;
using System.Linq;
using FinserveNew.Models.ViewModels;

namespace FinserveNew.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Accounts/AllAccounts
        //public async Task<IActionResult> AllAccounts()
        //{
        //    var employees = await _context.Employees.ToListAsync();
        //    return View(employees);
        //}

        // Get all employee accounts
        public async Task<IActionResult> AllAccounts(int page = 1, int pageSize = 10, string? search = null)
        {
            var query = _context.Employees.AsQueryable();

            // Optional: Search logic
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    e.FirstName.Contains(search) ||
                    e.LastName.Contains(search) ||
                    e.Position.Contains(search) ||
                    e.EmployeeID.ToString().Contains(search)
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

            return View(employees);
        }

        // View employee details
        public async Task<IActionResult> ViewDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Role)
                //.Include(e => e.BankInformation)
                //.Include(e => e.EmergencyContact)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // Add new employee
        [HttpGet]
        public IActionResult Add()
        {
            var vm = new AddEmployeeViewModel
            {
                Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" },
                BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" },
                BankTypes = new[] { "Savings", "Current" }
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddEmployeeViewModel vm)
        {
            // Uniqueness checks (IC, Email, BankAccountNumber, etc.)
            if (_context.Employees.Any(e => e.IC == vm.IC))
                ModelState.AddModelError("IC", "IC already exists.");
            if (_context.Employees.Any(e => e.Email == vm.Email))
                ModelState.AddModelError("Email", "Email already exists.");
            // ... more checks

            if (!ModelState.IsValid)
            {
                vm.Nationalities = new[] { "Malaysia", "Singapore", "Indonesia", "Thailand" };
                vm.BankNames = new[] { "Maybank", "CIMB", "RHB", "Public Bank" };
                vm.BankTypes = new[] { "Savings", "Current" };
                return View(vm);
            }

            // Generate EmployeeID, save Employee, Bank, Emergency, etc.
            // Save files (ICFile, ResumeFile, OfferLetterFile) to disk/db and reference in EmployeeDocument table

            TempData["Success"] = $"Employee {vm.FirstName} {vm.LastName} added successfully!";
            return RedirectToAction("AllAccounts");
        }


    }
}
