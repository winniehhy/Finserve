using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinserveNew.Models.ViewModels
{
    public class EmployeeDetailsViewModel
    {
        // Basic Info
        [Required]
        public string EmployeeID { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(12)]
        [Display(Name = "IC Number")]
        public string? IC { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Passport Number")]
        public string? PassportNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Nationality { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date Of Birth")]
        public DateOnly DateOfBirth { get; set; }

        // Contact Info
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Telephone Number")]
        public string TelephoneNumber { get; set; } = string.Empty;

        // Employment Info - DISPLAY ONLY (not editable from profile)
        [Required]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Join Date")]
        public DateOnly JoinDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Resignation Date")]
        public DateOnly? ResignationDate { get; set; }

        [Required]
        [Display(Name = "Confirmation Status")]
        public string ConfirmationStatus { get; set; } = string.Empty;

        // Role info for DISPLAY ONLY - NO EDITING from profile
        public string RoleName { get; set; } = string.Empty;

        // Bank Info
        [Required]
        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Bank Type")]
        public string BankType { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Bank Account Number")]
        public string BankAccountNumber { get; set; } = string.Empty;

        // Statutory Information
        [Required]
        [StringLength(15)]
        [Display(Name = "Income Tax Number")]
        public string IncomeTaxNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        [Display(Name = "EPF Number")]
        public string EPFNumber { get; set; } = string.Empty;

        // Emergency Contact
        [Required]
        [StringLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Emergency Contact Relationship")]
        public string EmergencyContactRelationship { get; set; } = string.Empty;

        // Documents
        public List<EmployeeDocument> Documents { get; set; } = new();

        // Dropdown options
        public string[] Nationalities { get; set; } = Array.Empty<string>();
        public string[] BankNames { get; set; } = Array.Empty<string>();
        public string[] BankTypes { get; set; } = Array.Empty<string>();
        public string[] ConfirmationStatuses { get; set; } = new[] { "Probation", "Confirmed", "Terminated" };
        
        // Enhanced relationship options with comprehensive list
        public string[] Relationships { get; set; } = new[] { 
            "Spouse", 
            "Parent", 
            "Father",
            "Mother", 
            "Child",
            "Son",
            "Daughter",
            "Sibling", 
            "Brother",
            "Sister",
            "Grandparent",
            "Grandfather",
            "Grandmother",
            "In-Law",
            "Father-in-Law",
            "Mother-in-Law",
            "Partner",
            "Friend", 
            "Relative",
            "Guardian",
            "Colleague",
            "Neighbor",
            "Other"
        };

        // Multiple new documents upload
        public List<IFormFile>? NewDocuments { get; set; }
        public List<string>? NewDocumentTypes { get; set; }
    }
}