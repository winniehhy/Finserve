// Models/ViewModels/AddEmployeeViewModel.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models.ViewModels
{
    public class AddEmployeeViewModel
    {
        // 1. Personal & Contact Details
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = "";
        [Required, MaxLength(100)]
        public string LastName { get; set; } = "";
        [Required, MaxLength(20)]
        public string IC { get; set; } = "";
        [Required]
        public string Nationality { get; set; } = "Malaysia";
        [Required, DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; } = "";
        [Required]
        public string TelephoneNumber { get; set; } = "";

        // 2. Employment & Position Details
        public string? EmployeeID { get; set; } // Auto-generated
        [Required]
        public string Position { get; set; } = "";
        [Required, DataType(DataType.Date)]
        public DateTime JoinDate { get; set; }
        [Required]
        public string ConfirmationStatus { get; set; } = "Probation";
        public DateTime? ResignationDate { get; set; }

        // 3. Compensation & Statutory
        [Required, Range(0.01, double.MaxValue)]
        public decimal Salary { get; set; }
        [Required]
        public string BankName { get; set; } = "";
        [Required]
        public string BankAccountNumber { get; set; } = "";
        [Required]
        public string BankType { get; set; } = "";
        [Required]
        public string IncomeTaxNumber { get; set; } = "";
        [Required]
        public string EPFNumber { get; set; } = "";

        // 4. Emergency Contact
        [Required]
        public string EmergencyName { get; set; } = "";
        [Required]
        public string EmergencyPhone { get; set; } = "";
        [Required]
        public string EmergencyRelationship { get; set; } = "";

        // 5. Documents
        public IFormFile? ICFile { get; set; }
        public IFormFile? ResumeFile { get; set; }
        public IFormFile? OfferLetterFile { get; set; }

        // 6. System Login
        [Required]
        public string Username { get; set; } = "";
        [Required, MinLength(8)]
        public string Password { get; set; } = "";
        [Required, Compare("Password")]
        public string ConfirmPassword { get; set; } = "";

        // Dropdowns
        public IEnumerable<string> Nationalities { get; set; } = new List<string>();
        public IEnumerable<string> BankNames { get; set; } = new List<string>();
        public IEnumerable<string> BankTypes { get; set; } = new List<string>();
        public IEnumerable<string> ConfirmationStatuses { get; set; } = new List<string> { "Probation", "Confirmed", "Terminated" };
        public IEnumerable<string> Relationships { get; set; } = new List<string> { "Spouse", "Parent", "Sibling", "Friend" };
    }
}
