using Finserve3.Models;
using Finserve3.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;

namespace Finserve3.Controllers
{
    public class ClaimController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ClaimController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Claim/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(); // Renders Create.cshtml
        }

        // POST: Claim/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateClaimViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // If validation fails, re-show form
                return View(model);
            }

            // 1. Handle file upload (if any)
            string uniqueFileName = null;
            if (model.Document != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder); // Ensure folder exists

                // Generate unique filename
                uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.Document.FileName);

                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    model.Document.CopyTo(fileStream);
                }
            }

            // 2. Map ViewModel to Entity
            var newClaim = new Claim
            {
                EmployeeId = model.EmployeeId,
                ClaimType = model.ClaimType,
                ClaimAmount = model.ClaimAmount,
                DocumentPath = uniqueFileName,
                SubmissionDate = DateTime.Now, // e.g. default to current date
                Status = "Pending" // e.g. default status
            };

            // 3. Save to database
            _context.Claims.Add(newClaim);
            _context.SaveChanges();

            // 4. Redirect to Index
            return RedirectToAction(nameof(Index));
        }

        // Your existing Index action
        public IActionResult Index()
        {
            var claims = _context.Claims.ToList();
            return View(claims);
        }
    }
}
