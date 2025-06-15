using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation; // Add this for ValidateNever

namespace FinserveNew.Models
{
    public class Claim
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Claim type is required")]
        [Display(Name = "Claim Type")]
        [MaxLength(50)]
        public string ClaimType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Claim amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Claim amount must be greater than 0")]
        [Display(Name = "Claim Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        [Display(Name = "Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Supporting Document Path")]
        [MaxLength(500)]
        public string? SupportingDocumentPath { get; set; }

        [Display(Name = "Supporting Document Name")]
        [MaxLength(255)]
        public string? SupportingDocumentName { get; set; }

        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        // Foreign Key - This is what gets validated
        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public string EmployeeId { get; set; } = "E001";

        // Navigation Property - REMOVE validation attributes and make it non-required for model binding
        [ForeignKey("EmployeeId")]
        [ValidateNever] // This tells ASP.NET Core to skip validation for this property
        public virtual EmployeeModel Employee { get; set; } = null!;

        // Optional properties
        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        [Display(Name = "Submission Date")]
        [DataType(DataType.Date)]
        public DateTime? SubmissionDate { get; set; }

        [Display(Name = "Approval Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalDate { get; set; }

        [Display(Name = "Approval")]
        public int? ApprovalID { get; set; }

        // Navigation Properties
        [ForeignKey("ApprovalID")]
        [ValidateNever]
        public virtual Approval? Approval { get; set; }

        // Collection navigation properties
        [ValidateNever]
        public virtual ICollection<ClaimDetails> ClaimDetails { get; set; } = new List<ClaimDetails>();
    }
}