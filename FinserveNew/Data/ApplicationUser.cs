using Microsoft.AspNetCore.Identity;

namespace FinserveNew.Models
{
    // This class extends IdentityUser to include any custom fields for authentication
    public class ApplicationUser : IdentityUser
    {
        
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Optional – If you want to reference back to the Employee profile
        public string? EmployeeID { get; set; }

        // Navigation property (optional but useful)
        public Employee? Employee { get; set; }
    }
}
