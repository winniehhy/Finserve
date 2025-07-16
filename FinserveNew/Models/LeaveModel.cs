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

        [Display(Name = "Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Default to Pending

        [Display(Name = "Leave Days")]
        public int LeaveDays { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Submission Date")]
        public DateTime SubmissionDate { get; set; }

        [Display(Name = "Approval Date")]
        public DateTime? ApprovalDate { get; set; }

        [Display(Name = "Approved By")]
        [MaxLength(450)] // Standard length for Identity UserId
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approval Remarks")]
        [MaxLength(1000)]
        public string? ApprovalRemarks { get; set; }

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

        // Calculated property for leave duration (as backup)
        [NotMapped]
        public int CalculatedLeaveDays => EndDate.DayNumber - StartDate.DayNumber + 1;
    }
}