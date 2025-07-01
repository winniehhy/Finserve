using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class LeaveModel
    {
        [Key]
        public int LeaveID { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateOnly EndDate { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        [Display(Name = "Reason")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Default to Pending

        [Display(Name = "Approved By")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        // Foreign Keys
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string EmployeeID { get; set; } = null!;

        [Required(ErrorMessage = "Leave type is required")]
        [Display(Name = "Leave Type")]
        public int LeaveTypeID { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeID")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("LeaveTypeID")]
        public virtual LeaveTypeModel? LeaveType { get; set; }
    }

   
}