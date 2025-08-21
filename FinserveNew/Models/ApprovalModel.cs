using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinserveNew.Models;

namespace FinserveNew.Models
{
    public class Approval
    {
        [Key]
        public string ApprovalID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Approval date is required")]
        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime ApprovalDate { get; set; }

        [Required(ErrorMessage = "Action is required")]
        [Display(Name = "Action")]
        [MaxLength(500)]
        public string Action { get; set; } = string.Empty;

        [Required(ErrorMessage = "Action by is required")]
        [Display(Name = "Action By")]
        [MaxLength(100)]
        public string ActionBy { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? Status { get; set; }

        [MaxLength(1000)]
        public string? Remarks { get; set; }

        // Optional link to a specific Payroll record
        public string? PayrollID { get; set; }

        // Foreign Key - Based on  ERD, Approval links to Employee
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string? EmployeeID { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; } = null!;

        [ForeignKey("PayrollID")]
        public virtual Payroll? Payroll { get; set; }

    
        // public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}