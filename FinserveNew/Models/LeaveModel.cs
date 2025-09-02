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
        [Range(0.5, double.MaxValue, ErrorMessage = "Leave days must be at least 0.5")]
        public double LeaveDays { get; set; } // Changed from int to double

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

        // Add navigation property for LeaveDetails
        public virtual ICollection<LeaveDetailsModel>? LeaveDetails { get; set; }

        // Calculated property for leave duration (as backup) - Updated to return double
        [NotMapped]
        public double CalculatedLeaveDays => EndDate.DayNumber - StartDate.DayNumber + 1;

        [NotMapped]
        public string DisplayLeaveId => $"#LVE-{LeaveID:D3}";
    }

    public class UnpaidLeaveRequestModel
    {
        [Key]
        public int UnpaidLeaveRequestID { get; set; }

        [Required]
        public string EmployeeID { get; set; }

        [Required]
        public int LeaveTypeID { get; set; }

        [DataType(DataType.Date)]
        public DateOnly StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateOnly EndDate { get; set; }

        public double RequestedDays { get; set; }
        public double ExcessDays { get; set; } // Days exceeding balance

        [Required(ErrorMessage = "Reason is required")]
        [Display(Name = "Reason")]
        [MaxLength(1000)]
        public string Reason { get; set; }

        [Display(Name = "Justification Reason")]
        [MaxLength(1000)]
        public string? JustificationReason { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime SubmissionDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ApprovalRemarks { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("LeaveTypeID")]
        public virtual LeaveTypeModel LeaveType { get; set; }
    }
}