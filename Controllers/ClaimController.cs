using Microsoft.AspNetCore.Mvc;
using Finserve3.Models;
using System.Linq;

namespace Finserve3.Controllers
{
    public class ClaimController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClaimController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var claim = _context.Claims.ToList();
            return View(claim);
        }
    }
}
