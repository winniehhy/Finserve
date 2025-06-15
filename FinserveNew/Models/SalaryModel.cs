using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Salary
    {
        [Key]
        public int SalaryID { get; set; }

        [Required(ErrorMessage = "Income tax number is required")]
        [Display(Name = "Income Tax Number")]
        [MaxLength(50)]
        public string IncomeTaxNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "EPF number is required")]
        [Display(Name = "EPF Number")]
        [MaxLength(50)]
        public string EPFNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Month is required")]
        [Display(Name = "Month")]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int Month { get; set; }

        [Required(ErrorMessage = "Year is required")]
        [Display(Name = "Year")]
        [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        [Display(Name = "Project Name")]
        [MaxLength(100)]
        public string ProjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Basic salary is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Basic salary must be greater than 0")]
        [Display(Name = "Basic Salary")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicSalary { get; set; }

        [Required(ErrorMessage = "Allowance is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Allowance must be 0 or greater")]
        [Display(Name = "Allowance")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Allowance { get; set; }

        [Required(ErrorMessage = "Deduction is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Deduction must be 0 or greater")]
        [Display(Name = "Deduction")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Deduction { get; set; }

        [Required(ErrorMessage = "Net salary is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Net salary must be 0 or greater")]
        [Display(Name = "Net Salary")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetSalary { get; set; }

        [Required(ErrorMessage = "Payment status is required")]
        [Display(Name = "Payment Status")]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "Pending";

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime? PaymentDate { get; set; }

        // Foreign Keys
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string EmployeeID { get; set; }

        [Required(ErrorMessage = "Approval is required")]
        [Display(Name = "Approval")]
        public int ApprovalID { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeID")]
        public virtual EmployeeModel Employee { get; set; } = null!;

        [ForeignKey("ApprovalID")]
        public virtual Approval Approval { get; set; } = null!;
    }
}