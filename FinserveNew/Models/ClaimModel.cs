using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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

        // The amount being claimed
        [Required(ErrorMessage = "Claim amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Claim amount must be greater than 0")]
        [Display(Name = "Claim Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        // Optional description for additional details
        [Display(Name = "Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        // Date when the claim was created (automatically set to current date)
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // File path where the uploaded document is stored
        [Display(Name = "Supporting Document Path")]
        [MaxLength(500)]
        public string? SupportingDocumentPath { get; set; }

        // Original name of the uploaded document
        [Display(Name = "Supporting Document Name")]
        [MaxLength(255)]
        public string? SupportingDocumentName { get; set; }

        // Current status of the claim (e.g. Pending, Approved, Rejected)
        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        // Foreign key linking to the employee who submitted the claim
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string EmployeeID { get; set; } = "E001";

        // Navigation property - links the claim to a single Employee
        // This allows access to employee details from a claim
        [ForeignKey("EmployeeID")]
        [ValidateNever] // Skip validation for this related model
        public virtual Employee Employee { get; set; } = null!;

        // Optional total amount field (e.g. for grouped claims)
        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        // Optional date the claim was submitted
        [Display(Name = "Submission Date")]
        [DataType(DataType.Date)]
        public DateTime? SubmissionDate { get; set; }

        // Optional date the claim was approved
        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        // Optional foreign key linking to approval record
        [Display(Name = "Approval")]
        public int? ApprovalID { get; set; }

        // Navigation property - links the claim to a single Approval entity
        // This gives access to approval details related to this claim
        [ForeignKey("ApprovalID")]
        [ValidateNever]
        public virtual Approval? Approval { get; set; }

        // Collection navigation property - links to multiple ClaimDetails
        // A single Claim can have multiple related ClaimDetails records
        [ValidateNever]
        public virtual ICollection<ClaimDetails> ClaimDetails { get; set; } = new List<ClaimDetails>();
    }
}
