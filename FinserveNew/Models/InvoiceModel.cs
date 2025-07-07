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
        public string InvoiceNumber { get; set; }

        [Required]
        public DateTime IssueDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public int Year { get; set; }

        [StringLength(500)]
        public string Remark { get; set; }

        [StringLength(255)]
        public string FilePath { get; set; }

        // Foreign Key
        [Required]
        public string EmployeeID { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; }

        // Computed Properties for Display
        public string StatusBadgeClass => Status switch
        {
            "Pending" => "bg-warning",
            "Approved" => "bg-success",
            "Paid" => "bg-primary",
            "Cancelled" => "bg-danger",
            _ => "bg-secondary"
        };

        public string FormattedAmount => $"RM {TotalAmount:F2}";

        public bool CanEdit => Status == "Pending";

        public bool CanDelete => Status == "Pending";
    }
}