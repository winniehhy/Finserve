using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;
using FinserveNew.Data;

public class DbInitializer
{
    public static async Task SeedUsersAndRolesAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        // Create roles in both Identity and custom Roles table
        string[] roles = { "Employee", "HR", "Senior HR", "Admin" };
        
        foreach (var roleName in roles)
        {
            // Create Identity role if it doesn't exist
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            // Create custom JobRole if it doesn't exist
            var existingJobRole = await context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == roleName);
            
            if (existingJobRole == null)
            {
                var jobRole = new JobRole
                {
                    RoleName = roleName,
                    Description = GetRoleDescription(roleName)
                };
                context.Roles.Add(jobRole);
            }
        }

        // Save changes to the custom Roles table
        await context.SaveChangesAsync();

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

        // Create Senior HR user
        var seniorHrUser = await userManager.FindByEmailAsync("seniorhr@finserve.com");
        if (seniorHrUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = "seniorhr@finserve.com",
                Email = "seniorhr@finserve.com",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Test@123");
            await userManager.AddToRoleAsync(user, "Senior HR");
            await userManager.AddToRoleAsync(user, "HR");
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
            await userManager.AddToRoleAsync(user, "HR");
            await userManager.AddToRoleAsync(user, "Employee"); // Admin can access everything
        }
    }

    private static string GetRoleDescription(string roleName)
    {
        return roleName switch
        {
            "Employee" => "Standard employee with basic access permissions",
            "HR" => "Human Resources staff with employee management capabilities",
            "Senior HR" => "Senior Human Resources with advanced management and approval permissions",
            "Admin" => "Administrator with full system access and control",
            _ => $"System role: {roleName}"
        };
    }
}