using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "Role name is required")]
        [Display(Name = "Role Name")]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(200)]
        public string? Description { get; set; }

        // Navigation Property
        public virtual ICollection<EmployeeModel> Employees { get; set; } = new List<EmployeeModel>();
    }
}