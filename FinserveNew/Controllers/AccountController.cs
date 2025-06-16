using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using FinserveNew.Data;
using System.Threading.Tasks;
using System.Linq;

namespace FinserveNew.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Replace with your actual user lookup and password check
            var user = _context.Users.FirstOrDefault(u => u.Name == email /* && u.Password == password */);

            if (user != null)
            {
                //var claims = new List<Claim>
                //{
                //    new Claim(ClaimTypes.Name, user.Name),
                //    new Claim(ClaimTypes.Role, user.Role)
                //};

                //var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");
                //var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                //await HttpContext.SignInAsync("MyCookieAuth", claimsPrincipal);

                // Redirect based on role
                if (user.Role == "Admin")
                    return RedirectToAction("AdminDashboard", "Home");
                else
                    return RedirectToAction("EmployeeDashboard", "Home");
            }

            ViewBag.Error = "Invalid login attempt";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MyCookieAuth");
            return RedirectToAction("Login");
        }
    }
}
