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

        // Create HR user (HR staff with single role only)
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
        else
        {
            // Ensure HR user has only HR role
            await EnsureSingleRoleAsync(userManager, hrUser, "HR");
        }

        // Create regular Employee user (Employee role only)
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
        else
        {
            // Ensure Employee user has only Employee role
            await EnsureSingleRoleAsync(userManager, employeeUser, "Employee");
        }

        // Create Senior HR user (Senior HR role only)
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
        }
        else
        {
            // Ensure Senior HR user has only Senior HR role
            await EnsureSingleRoleAsync(userManager, seniorHrUser, "Senior HR");
        }

        // Create Admin user (Admin role only)
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
        }
        else
        {
            // Ensure Admin user has only Admin role
            await EnsureSingleRoleAsync(userManager, adminUser, "Admin");
        }
    }

    /// <summary>
    /// Helper method to ensure a user has only one specific role
    /// </summary>
    private static async Task EnsureSingleRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string targetRole)
    {
        // Get all current roles for the user
        var currentRoles = await userManager.GetRolesAsync(user);
        
        // Remove all existing roles
        if (currentRoles.Any())
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        
        // Add only the target role
        if (!await userManager.IsInRoleAsync(user, targetRole))
        {
            await userManager.AddToRoleAsync(user, targetRole);
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