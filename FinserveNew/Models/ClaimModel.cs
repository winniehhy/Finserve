using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Claim
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string ClaimType { get; set; } = string.Empty;

        [Required, DataType(DataType.Date)]
        public DateTime ClaimDate { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "MYR";

        // ⚠️ Changed from varchar(1000) to TEXT
        [Column(TypeName = "TEXT")]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? SupportingDocumentPath { get; set; }

        [MaxLength(255)]
        public string? SupportingDocumentName { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Required, MaxLength(255)]
        public string EmployeeID { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? SubmissionDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        [MaxLength(255)]
        public string? ApprovedBy { get; set; }

        // ⚠️ Changed from varchar(1000) to TEXT
        [Column(TypeName = "TEXT")]
        public string? ApprovalRemarks { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        // Soft delete field
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedDate { get; set; }

        public virtual ICollection<ClaimDetails>? ClaimDetails { get; set; }

        // Navigation property for Employee
        [ForeignKey("EmployeeID")]
        public virtual Employee? Employee { get; set; }

        // --- [NotMapped] properties stay unchanged ---
        [NotMapped] public List<IFormFile>? UploadedFiles { get; set; }

        [NotMapped]
        public string DisplayAmount => $"{(Currency == "USD" ? "$" : Currency == "MYR" ? "RM" : Currency)} {ClaimAmount:N2}";

        [NotMapped]
        public string StatusBadgeClass => Status switch
        {
            "Pending" => "bg-warning",
            "Approved" => "bg-success",
            "Rejected" => "bg-danger",
            _ => "bg-secondary"
        };

        [NotMapped]
        public string StatusDisplayText => Status switch
        {
            "Pending" => "Pending",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            _ => Status
        };

        [NotMapped]
        public string FormattedClaimDate => ClaimDate.ToString("dd/MM/yyyy");

        [NotMapped]
        public string FormattedSubmissionDate => SubmissionDate?.ToString("dd/MM/yyyy") ?? "Not submitted";

        [NotMapped]
        public string FormattedApprovalDate => ApprovalDate?.ToString("dd/MM/yyyy") ?? "Not approved";

        [NotMapped]
        public string FormattedCreatedDate => CreatedDate.ToString("dd/MM/yyyy HH:mm");

        [NotMapped]
        public string FormattedTotalAmount => TotalAmount?.ToString("N2") ?? ClaimAmount.ToString("N2");

        [NotMapped]
        public bool HasSupportingDocuments => !string.IsNullOrEmpty(SupportingDocumentPath) || (ClaimDetails != null && ClaimDetails.Any());

        [NotMapped]
        public int SupportingDocumentCount => ClaimDetails?.Count ?? 0;

        [NotMapped]
        public string DisplayClaimId => $"#CLM-{Id:D3}";
    }
}
