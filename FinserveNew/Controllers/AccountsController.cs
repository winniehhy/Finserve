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
                //query = query.Where(e =>
                //    e.FullName.Contains(search) ||
                //    e.Position.Contains(search) ||
                //    e.Department.Contains(search) ||
                //    e.Id.ToString().Contains(search)
                //);
            }

            var totalRecords = await query.CountAsync();
            var employees = await query
                .OrderBy(e => e.EmployeeId)
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
    }
}
