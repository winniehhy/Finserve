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

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalAmount { get; set; }

        [MaxLength(3)]
        public string? OriginalCurrency { get; set; }

        [Column(TypeName = "decimal(10,6)")]
        public decimal? ExchangeRate { get; set; }

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

        public bool IsOCRProcessed { get; set; } = false;

        // ⚠️ Changed from varchar(10000) to LONGTEXT
        [Column(TypeName = "LONGTEXT")]
        public string? OCRRawText { get; set; }

        [Range(0, 100)]
        public int? OCRConfidence { get; set; }

        // ⚠️ Changed from varchar(5000) to TEXT
        [Column(TypeName = "TEXT")]
        public string? OCRPriceAnalysis { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OCRDetectedAmount { get; set; }

        [MaxLength(3)]
        public string? OCRDetectedCurrency { get; set; }

        public bool OCRAmountVerified { get; set; } = false;

        public DateTime? OCRProcessedDate { get; set; }

        public virtual ICollection<ClaimDetails>? ClaimDetails { get; set; }

        // Navigation property for Employee
        [ForeignKey("EmployeeID")]
        public virtual Employee? Employee { get; set; }

        // --- [NotMapped] properties stay unchanged ---
        [NotMapped] public List<IFormFile>? UploadedFiles { get; set; }

        [NotMapped]
        public string DisplayAmount => $"{(Currency == "USD" ? "$" : Currency == "MYR" ? "RM" : Currency)} {ClaimAmount:N2}";

        [NotMapped]
        public string? ConversionInfo
        {
            get
            {
                if (OriginalAmount.HasValue && !string.IsNullOrEmpty(OriginalCurrency) &&
                    OriginalCurrency != Currency && ExchangeRate.HasValue)
                {
                    var symbol = OriginalCurrency switch
                    {
                        "USD" => "$",
                        "MYR" => "RM",
                        _ => OriginalCurrency
                    };
                    return $"Original: {symbol} {OriginalAmount:N2} (Rate: {ExchangeRate:N4})";
                }
                return null;
            }
        }

        [NotMapped]
        public int DocumentCount => (ClaimDetails?.Any() == true) ? ClaimDetails.Count : (!string.IsNullOrEmpty(SupportingDocumentPath) ? 1 : 0);

        [NotMapped] public bool HasDocuments => DocumentCount > 0;

        [NotMapped] public string FormattedClaimDate => ClaimDate.ToString("dd/MM/yyyy");

        [NotMapped]
        public string OCRStatus => !IsOCRProcessed ? "Not Processed" : OCRAmountVerified ? "Verified" : "Pending Verification";

        [NotMapped]
        public string OCRConfidenceDisplay => OCRConfidence.HasValue ? $"{OCRConfidence}%" : "N/A";
    }
}
