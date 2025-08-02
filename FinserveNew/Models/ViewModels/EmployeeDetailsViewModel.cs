using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FinserveNew.Models.ViewModels
{
    public class EmployeeDetailsViewModel
    {
        // Basic Info
        public string EmployeeID { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string IC { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }

        // Contact Info
        public string Email { get; set; } = string.Empty;
        public string TelephoneNumber { get; set; } = string.Empty;

        // Employment Info
        public string Position { get; set; } = string.Empty;
        public DateOnly JoinDate { get; set; }
        public DateOnly? ResignationDate { get; set; }
        public string ConfirmationStatus { get; set; } = string.Empty;

        [Display(Name = "Role")]
        public string RoleName { get; set; }

        [Display(Name = "System Access")]
        public string SystemRole { get; set; }

        // Bank Info
        public string BankName { get; set; } = string.Empty;
        public string BankType { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;

        // Emergency Contact
        public string EmergencyContactName { get; set; } = string.Empty;
        public string EmergencyContactPhone { get; set; } = string.Empty;
        public string EmergencyContactRelationship { get; set; } = string.Empty;

        // Documents
        public List<EmployeeDocument> Documents { get; set; } = new();

        // Dropdown options
        public string[] Nationalities { get; set; } = Array.Empty<string>();
        public string[] BankNames { get; set; } = Array.Empty<string>();
        public string[] BankTypes { get; set; } = Array.Empty<string>();
        public string[] ConfirmationStatuses { get; set; } = new[] { "Probation", "Confirmed", "Terminated" };
        public string[] Relationships { get; set; } = new[] { "Spouse", "Parent", "Sibling", "Friend" };

        // New document upload
        public IFormFile? NewDocument { get; set; }
        public string? NewDocumentType { get; set; }
    }

    //public class DocumentViewModel
    //{
    //    public int DocumentID { get; set; }
    //    public string DocumentType { get; set; } = string.Empty;
    //    public string FilePath { get; set; } = string.Empty;
    //    // public DateTime UploadDate { get; set; }
    //}
} 