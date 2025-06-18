// Models/ViewModels/AddEmployeeViewModel.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace FinserveNew.Models.ViewModels
{
    public class AddEmployeeViewModel
    {
        // 1. Personal & Contact Details
        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;
        [Required]
        [StringLength(20)]
        [Display(Name = "IC Number")]
        public string IC { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string Nationality { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;
        [Required]
        [StringLength(20)]
        [Display(Name = "Telephone Number")]
        public string TelephoneNumber { get; set; } = string.Empty;
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateOnly DateOfBirth { get; set; }

        // 2. Employment & Position Details
        public string? EmployeeID { get; set; } // Auto-generated
        [Required]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Join Date")]
        public DateOnly JoinDate { get; set; }
        [Required]
        [Display(Name = "Confirmation Status")]
        public string ConfirmationStatus { get; set; } = string.Empty;
        [DataType(DataType.Date)]
        [Display(Name = "Resignation Date")]
        public DateOnly? ResignationDate { get; set; }

        // 3. Compensation & Statutory
        //[Required, Range(0.01, double.MaxValue)]
        //public decimal Salary { get; set; }
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
        [Required]
        [StringLength(50)]
        public string IncomeTaxNumber { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string EPFNumber { get; set; } = string.Empty;

        // 4. Emergency Contact
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

        // 5. Documents
        [Display(Name = "IC Photo")]
        public IFormFile? ICFile { get; set; }
        [Display(Name = "Resume")]
        public IFormFile? ResumeFile { get; set; }
        [Display(Name = "Offer Letter")]
        public IFormFile? OfferLetterFile { get; set; }

        // 6. System Login
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        //[Required, Compare("Password")]
        //public string ConfirmPassword { get; set; } = string.Empty;

        // Dropdowns
        public string[] Nationalities { get; set; } = Array.Empty<string>();
        public string[] BankNames { get; set; } = Array.Empty<string>();
        public string[] BankTypes { get; set; } = Array.Empty<string>();
        public IEnumerable<string> ConfirmationStatuses { get; set; } = new List<string> { "Probation", "Confirmed", "Terminated" };
        public IEnumerable<string> Relationships { get; set; } = new List<string> { "Spouse", "Parent", "Sibling", "Friend" };
    }
}
