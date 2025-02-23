using System.ComponentModel.DataAnnotations;
namespace Finserve3.Models
{
    public class Employee
    {
        [Key]  // Add this attribute
        public string? EmployeeId { get; set; }

        [Required]  // It's good practice to add this for required fields
        public string? Name { get; set; }

        public string? Department { get; set; }

        // Navigation property - plural name is better for collections
        public virtual ICollection<Claim> Claims { get; set; }  // Changed from Claim to Claims
    }
}