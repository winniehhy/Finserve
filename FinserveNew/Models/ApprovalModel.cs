using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Approval
    {
        [Key]
        public int ApprovalID { get; set; }

        [Required(ErrorMessage = "Approval date is required")]
        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime ApprovalDate { get; set; }

        [Required(ErrorMessage = "Purpose is required")]
        [Display(Name = "Purpose")]
        [MaxLength(500)]
        public string Purpose { get; set; } = string.Empty;

        [Required(ErrorMessage = "Approved by is required")]
        [Display(Name = "Approved By")]
        [MaxLength(100)]
        public string ApprovedBy { get; set; } = string.Empty;

        // Foreign Key - Based on  ERD, Approval links to Employee
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string? EmployeeID { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; } = null!;

    
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        public virtual ICollection<Salary> Salaries { get; set; } = new List<Salary>();
    }
}