using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using System.Threading.Tasks;

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

        public async Task<IActionResult> View(string id)
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

    }
}
