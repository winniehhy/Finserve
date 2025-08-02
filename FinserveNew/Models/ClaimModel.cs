
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
    }
}