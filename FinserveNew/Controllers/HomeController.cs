using FinserveNew.Data;
using FinserveNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace FinserveNew.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
          
        }

        // Default Index - redirects to appropriate dashboard based on role
        [Authorize]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("AdminDashboard");
            }
            else if (User.IsInRole("HR"))
            {
                return RedirectToAction("HRDashboard");
            }
            else if (User.IsInRole("Employee"))
            {
                return RedirectToAction("EmployeeDashboard");
            }
            // Fallback for users without proper roles
            return RedirectToAction("Login", "Account");
        }

        // Admin Dashboard - Only accessible by Admin
        [Authorize(Roles = "Admin")]
        public IActionResult AdminDashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            ViewData["UserRole"] = "Admin";
            return View("~/Views/Admins/Dashboard.cshtml");
        }

        // HR Dashboard - Only accessible by HR
        [Authorize(Roles = "HR")]
        public IActionResult HRDashboard()
        {
            ViewData["Title"] = "HR Dashboard";
            ViewData["UserRole"] = "HR";
            return View("~/Views/HR/Dashboard.cshtml");
        }

        // Employee Dashboard - Only accessible by Employee
        [Authorize(Roles = "Employee")]
        public IActionResult EmployeeDashboard()
        {
            // direct used leavecontroller dashboard
            return RedirectToAction("Dashboard", "Leaves");
        }

        // Helper method to get date range
        private List<DateOnly> GetDateRange(DateOnly startDate, DateOnly endDate)
        {
            var dates = new List<DateOnly>();
            var current = startDate;

            while (current <= endDate)
            {
                dates.Add(current);
                current = current.AddDays(1);
            }

            return dates;
        }

        // Privacy page - accessible to all authenticated users
        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        // Error page - accessible to all
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}