using Microsoft.AspNetCore.Identity;
using FinserveNew.Models;

public class DbInitializer
{
    public static async Task SeedUsersAndRolesAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Create roles
        string[] roles = { "Employee", "HR", "Admin" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create HR user (HR staff cannot access employee claims directly)
        var hrUser = await userManager.FindByEmailAsync("hr@finserve.com");
        if (hrUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = "hr@finserve.com",
                Email = "hr@finserve.com",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Test@123");
            await userManager.AddToRoleAsync(user, "HR");
        }

        // Create regular Employee user
        var employeeUser = await userManager.FindByEmailAsync("employee@finserve.com");
        if (employeeUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = "employee@finserve.com",
                Email = "employee@finserve.com",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Test@123");
            await userManager.AddToRoleAsync(user, "Employee");
        }

  

        // Create Admin user (Admins have all roles including Employee)
        var adminUser = await userManager.FindByEmailAsync("admin@finserve.com");
        if (adminUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = "admin@finserve.com",
                Email = "admin@finserve.com",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Test@123");
            await userManager.AddToRoleAsync(user, "Admin");
            await userManager.AddToRoleAsync(user, "Employee"); // Admin can access everything
        }
    }
}