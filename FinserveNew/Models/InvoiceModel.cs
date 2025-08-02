using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        [Required]
        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        [Required]
        [Display(Name = "Client/Customer Name")]
        [StringLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [Display(Name = "Client Company")]
        [StringLength(200)]
        public string? ClientCompany { get; set; }

        [Display(Name = "Client Email")]
        [EmailAddress]
        [StringLength(100)]
        public string? ClientEmail { get; set; }

        [Required]
        [Display(Name = "Issue Date")]
        public DateTime IssueDate { get; set; }

        [Required]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        [Required]
        public int Year { get; set; }

        [StringLength(1000)]
        public string? Remark { get; set; }

        [StringLength(255)]
        [Display(Name = "File Path")]
        public string? FilePath { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        //If you want to link to Employee, add these properties:
        [Display(Name = "Employee ID")]
        public string? EmployeeID { get; set; }

        //Navigation property(if you have Employee model)
        [ForeignKey("EmployeeID")]
        public virtual ApplicationUser? Employee { get; set; }

        //Computed Properties for Display
        public string StatusBadgeClass => Status switch
        {
            "Draft" => "bg-secondary",
            "Pending" => "bg-warning",
            "Approved" => "bg-info",
            "Sent" => "bg-primary",
            "Paid" => "bg-success",
            "Overdue" => "bg-danger",
            "Cancelled" => "bg-dark",
            _ => "bg-secondary"
        };

        public string FormattedAmount => Currency switch
        {
            "USD" => $"${TotalAmount:F2}",
            "MYR" => $"RM {TotalAmount:F2}",
            "SGD" => $"S${TotalAmount:F2}",
            "EUR" => $"€{TotalAmount:F2}",
            _ => $"{Currency} {TotalAmount:F2}"
        };

        public bool CanEdit => Status == "Draft" || Status == "Pending";
        public bool CanDelete => Status == "Draft";
        public bool IsOverdue => (Status == "Sent" || Status == "Approved") && DueDate < DateTime.Now;
    }
}