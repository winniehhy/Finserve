using Microsoft.AspNetCore.Identity;

namespace FinserveNew.Models
{
    // This class extends IdentityUser to include any custom fields for authentication
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Reference to Employee profile - this should match the Employee's primary key
        public string? EmployeeID { get; set; }

        // Default account tracking properties
        public bool IsDefaultAccount { get; set; } = false;
        public bool IsDeactivated { get; set; } = false;
        public DateTime? DeactivatedAt { get; set; }
        public string? DeactivationReason { get; set; }

        // Navigation property - removed to avoid conflicts
        // public Employee? Employee { get; set; }
    }
}
