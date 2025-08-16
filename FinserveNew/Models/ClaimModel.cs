using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Claim
    {
        // Primary key for the Claim
        [Key]
        public int Id { get; set; }

        // The type/category of the claim (e.g. Travel, Medical, etc.)
        [Required(ErrorMessage = "Claim type is required")]
        [Display(Name = "Claim Type")]
        [MaxLength(50)]
        public string ClaimType { get; set; } = string.Empty;

        // FIXED: Date when the expense/claim occurred - Remove default value
        [Required(ErrorMessage = "Claim date is required")]
        [Display(Name = "Claim Date")]
        [DataType(DataType.Date)]
        public DateTime ClaimDate { get; set; }

        // The amount being claimed
        [Required(ErrorMessage = "Claim amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Claim amount must be greater than 0")]
        [Display(Name = "Claim Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        // Currency used for the claim amount
        [Display(Name = "Currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = "MYR";

        // Original amount in the selected currency (before any conversion)
        [Display(Name = "Original Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalAmount { get; set; }

        // Original currency if different from base currency
        [Display(Name = "Original Currency")]
        [MaxLength(3)]
        public string? OriginalCurrency { get; set; }

        // Exchange rate used for conversion (if applicable)
        [Display(Name = "Exchange Rate")]
        [Column(TypeName = "decimal(10,6)")]
        public decimal? ExchangeRate { get; set; }

        // Optional description for additional details
        [Display(Name = "Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        // Date when the claim was created (automatically set to current date)
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // File path where the uploaded document is stored (KEEP THIS for backward compatibility)
        [Display(Name = "Supporting Document Path")]
        [MaxLength(500)]
        public string? SupportingDocumentPath { get; set; }

        // Original name of the uploaded document (KEEP THIS for backward compatibility)
        [Display(Name = "Supporting Document Name")]
        [MaxLength(255)]
        public string? SupportingDocumentName { get; set; }

        // Current status of the claim (e.g. Pending, Approved, Rejected)
        [Required]
        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        // Employee ID who created the claim
        [Required(ErrorMessage = "Employee ID is required")]
        [Display(Name = "Employee ID")]
        [MaxLength(255)]
        public string EmployeeID { get; set; } = string.Empty;

        // Date the claim was submitted
        [Display(Name = "Submission Date")]
        [DataType(DataType.Date)]
        public DateTime? SubmissionDate { get; set; }

        // Date the claim was approved/rejected
        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        // User ID of the person who approved/rejected (HR/Admin)
        [Display(Name = "Approved By")]
        [MaxLength(255)]
        public string? ApprovedBy { get; set; }

        // Optional remarks from the approver
        [Display(Name = "Approval Remarks")]
        [MaxLength(1000)]
        public string? ApprovalRemarks { get; set; }

        // Optional total amount field (for grouped claims)
        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        // Navigation property to ClaimDetails (if you use it)
        public virtual ICollection<ClaimDetails>? ClaimDetails { get; set; }

        // ===== NotMapped PROPERTIES FOR FORM HANDLING =====

        // For handling multiple file uploads in forms (NOT stored in database)
        [NotMapped]
        public List<IFormFile>? UploadedFiles { get; set; }

        // For currency conversion display
        [NotMapped]
        public string DisplayAmount
        {
            get
            {
                var symbol = Currency switch
                {
                    "USD" => "$",
                    "MYR" => "RM",
                    _ => Currency
                };
                return $"{symbol} {ClaimAmount:N2}";
            }
        }

        // Show conversion info if applicable
        [NotMapped]
        public string? ConversionInfo
        {
            get
            {
                if (OriginalAmount.HasValue && !string.IsNullOrEmpty(OriginalCurrency) &&
                    OriginalCurrency != Currency && ExchangeRate.HasValue)
                {
                    var originalSymbol = OriginalCurrency switch
                    {
                        "USD" => "$",
                        "MYR" => "RM",
                        _ => OriginalCurrency
                    };
                    return $"Original: {originalSymbol} {OriginalAmount:N2} (Rate: {ExchangeRate:N4})";
                }
                return null;
            }
        }

        // Get document count from ClaimDetails if using that approach, or just check main document
        [NotMapped]
        public int DocumentCount
        {
            get
            {
                if (ClaimDetails != null && ClaimDetails.Any())
                    return ClaimDetails.Count;

                return !string.IsNullOrEmpty(SupportingDocumentPath) ? 1 : 0;
            }
        }

        // Check if has any documents
        [NotMapped]
        public bool HasDocuments
        {
            get
            {
                return DocumentCount > 0;
            }
        }

        // Formatted claim date
        [NotMapped]
        public string FormattedClaimDate
        {
            get
            {
                return ClaimDate.ToString("dd/MM/yyyy");
            }
        }
    }
}