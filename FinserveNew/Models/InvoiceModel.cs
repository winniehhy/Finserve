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
        public string Status { get; set; } = "Pending";

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

        // Soft delete flag
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted Date")]
        public DateTime? DeletedDate { get; set; }

        [Display(Name = "Deleted By")]
        public string? DeletedBy { get; set; }

        // CHANGED: Navigation property for invoice items - Use List instead of ICollection
        public virtual List<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

        // Computed Properties for Display
        public string StatusBadgeClass => Status switch
        {
            "Pending" => "bg-warning",
            "Sent" => "bg-info",
            "Paid" => "bg-success",
            "Overdue" => "bg-dark",
            "Cancelled" => "bg-danger",
            _ => "bg-secondary"
        };

        public string FormattedAmount => Currency switch
        {
            "USD" => $"${TotalAmount:F2}",
            "MYR" => $"RM {TotalAmount:F2}",
            _ => $"{Currency} {TotalAmount:F2}"
        };

        public bool CanEdit => Status == "Pending" && !IsDeleted;
        public bool CanDelete => Status == "Pending" && !IsDeleted;
        public bool IsOverdue => (Status == "Sent") && DueDate < DateTime.Now;
        public bool CanSend => Status == "Pending" && !IsDeleted;
        public bool CanMarkPaid => (Status == "Sent" || Status == "Overdue") && !IsDeleted;

        // Method to calculate total from items
        public void CalculateTotalFromItems()
        {
            TotalAmount = InvoiceItems?.Sum(item => item.LineTotal) ?? 0;
        }
    }
}