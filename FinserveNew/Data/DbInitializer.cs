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

        // Create ONLY ONE default HR user
        var defaultHrUser = await userManager.FindByEmailAsync("hr@finserve.com");
        if (defaultHrUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = "hr@finserve.com",
                Email = "hr@finserve.com",
                EmailConfirmed = true,
                IsDefaultAccount = true, // Mark as default account
                FirstName = "Default",
                LastName = "HR"
            };
            await userManager.CreateAsync(user, "Test@123");
            await userManager.AddToRoleAsync(user, "HR");
        }
        else if (!defaultHrUser.IsDefaultAccount)
        {
            // Update existing account to be marked as default
            defaultHrUser.IsDefaultAccount = true;
            await userManager.UpdateAsync(defaultHrUser);
            await EnsureSingleRoleAsync(userManager, defaultHrUser, "HR");
        }
    }

    /// Check if there are other active HR accounts and deactivate the default account if conditions are met
    public static async Task CheckAndDeactivateDefaultAccountAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        
        // Find the default HR account
        var defaultAccount = await userManager.Users
            .FirstOrDefaultAsync(u => u.IsDefaultAccount && !u.IsDeactivated);
        
        if (defaultAccount == null) return;

        // Count other active HR accounts (excluding the default account)
        var otherHrUsers = await userManager.GetUsersInRoleAsync("HR");
        var activeOtherHrCount = otherHrUsers.Count(u => 
            !u.IsDefaultAccount && 
            !u.IsDeactivated && 
            u.Id != defaultAccount.Id);

        // If there's at least one other HR account, deactivate the default account
        if (activeOtherHrCount > 0)
        {
            defaultAccount.IsDeactivated = true;
            defaultAccount.DeactivatedAt = DateTime.UtcNow;
            defaultAccount.DeactivationReason = "Deactivated automatically after creation of alternative HR accounts";
            
            await userManager.UpdateAsync(defaultAccount);
        }
    }

    /// <summary>
    /// Get a message about default account deactivation for display to users
    /// </summary>
    public static async Task<string?> GetDefaultAccountMessageAsync(IServiceProvider serviceProvider, string currentUserId)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        
        var currentUser = await userManager.FindByIdAsync(currentUserId);
        if (currentUser?.IsDefaultAccount == true && !currentUser.IsDeactivated)
        {
            var otherHrUsers = await userManager.GetUsersInRoleAsync("HR");
            var activeOtherHrCount = otherHrUsers.Count(u => 
                !u.IsDefaultAccount && 
                !u.IsDeactivated && 
                u.Id != currentUser.Id);

            if (activeOtherHrCount > 0)
            {
                return "Notice: This is the default HR account. It will be automatically deactivated when you log out, as other HR accounts are now available. Please ensure you have access to an alternative HR account.";
            }
            else
            {
                return "You are using the default HR account. Please create additional HR accounts through the employee management system.";
            }
        }

        return null;
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
            "Senior HR" => "Senior Human Resources with approval permissions",
            "Admin" => "Administrator with invoice and report viewing",
            _ => $"System role: {roleName}"
        };
    }
}