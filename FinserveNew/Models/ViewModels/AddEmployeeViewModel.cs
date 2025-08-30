// Models/ViewModels/AddEmployeeViewModel.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using FinserveNew.Models.ValidationAttributes;

namespace FinserveNew.Models.ViewModels
{
    public class AddEmployeeViewModel
    {
        // 1. Personal & Contact Details
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'/-]{2,50}$", ErrorMessage = "First name can only contain letters, spaces, hyphens, slash, and apostrophes")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'/-]{2,50}$", ErrorMessage = "Last name can only contain letters, spaces, hyphens, slash, and apostrophes")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(12)]
        [MalaysianIC]
        [ConditionalRequired("Nationality", "Malaysia", ErrorMessage = "IC Number is required for Malaysian citizens.")]
        [Display(Name = "IC Number")]
        public string? IC { get; set; } = string.Empty;

        [StringLength(20, MinimumLength = 6, ErrorMessage = "Passport number must be between 6 and 20 characters.")]
        [RegularExpression(@"^[A-Z0-9]{6,20}$", ErrorMessage = "Passport number must contain only uppercase letters and numbers")]
        [Display(Name = "Passport Number")]
        public string? PassportNumber { get; set; }

        [Required(ErrorMessage = "Nationality is required.")]
        [StringLength(50)]
        public string Nationality { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(100, ErrorMessage = "Email address cannot exceed 100 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telephone number is required.")]
        [StringLength(20)]
        [Display(Name = "Telephone Number")]
        public string TelephoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [AgeRange(18, 80, ErrorMessage = "Employee must be between 18 and 80 years old.")]
        [Display(Name = "Date of Birth")]
        public DateOnly DateOfBirth { get; set; }

        // 2. Employment & Position Details
        //public string? EmployeeID { get; set; } // Auto-generated 

        [Required(ErrorMessage = "Position is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Position must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-/&().,]{2,100}$", ErrorMessage = "Position contains invalid characters")]
        public string Position { get; set; } = string.Empty;

        [Required(ErrorMessage = "Join Date is required.")]
        [DataType(DataType.Date)]
        [FutureDateValidation(7, ErrorMessage = "Join date cannot be more than 7 days in the future.")]
        [Display(Name = "Join Date")]
        public DateOnly JoinDate { get; set; }

        [Required(ErrorMessage = "Confirmation status is required.")]
        [RegularExpression("^(Pending|Probation|Confirmed|Terminated)$", 
            ErrorMessage = "Please select a valid confirmation status")]
        [Display(Name = "Confirmation Status")]
        public string ConfirmationStatus { get; set; } = string.Empty;

        // 3. Compensation & Statutory
        //[Required, Range(0.01, double.MaxValue)]
        //public decimal Salary { get; set; }
        [Required(ErrorMessage = "Bank name is required.")]
        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank type is required.")]
        [StringLength(50)]
        [RegularExpression("^(Savings|Current)$", ErrorMessage = "Bank type must be either Savings or Current")]
        [Display(Name = "Bank Type")]
        public string BankType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank account number is required.")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Bank account number must be between 8 and 50 characters.")]
        [RegularExpression(@"^[0-9\-]{8,50}$", ErrorMessage = "Bank account number must be 8-50 characters and contain only numbers and hyphens")]
        [Display(Name = "Bank Account Number")]
        public string BankAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Income tax number is required.")]
        [StringLength(15, MinimumLength = 8, ErrorMessage = "Income tax number must be between 8 and 15 characters.")]
        [RegularExpression(@"^[A-Z0-9]{8,15}$", ErrorMessage = "Income tax number must be 8-15 characters and contain only uppercase letters and numbers")]
        public string IncomeTaxNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "EPF number is required.")]
        [StringLength(15, MinimumLength = 8, ErrorMessage = "EPF number must be between 8 and 15 characters.")]
        [RegularExpression(@"^[A-Z0-9]{8,15}$", ErrorMessage = "EPF number must be 8-15 characters and contain only uppercase letters and numbers")]
        public string EPFNumber { get; set; } = string.Empty;

        // 4. Emergency Contact
        [Required(ErrorMessage = "Emergency contact name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Emergency contact name must be between 1 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'/-]{2,100}$", ErrorMessage = "Emergency contact name can only contain letters, spaces, hyphens, slash, and apostrophes")]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Emergency contact phone is required.")]
        [StringLength(20)]
        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Relationship is required.")]
        [StringLength(50)]
        [Display(Name = "Emergency Contact Relationship")]
        public string EmergencyContactRelationship { get; set; } = string.Empty;

        // 5. Documents
        public List<IFormFile>? NewDocuments { get; set; }
        public List<string>? NewDocumentTypes { get; set; }


        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public int RoleID { get; set; }

        public List<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();


        // Dropdowns
        public string[] Nationalities { get; set; } = Array.Empty<string>();
        public string[] BankNames { get; set; } = Array.Empty<string>();
        public string[] BankTypes { get; set; } = Array.Empty<string>();
        public IEnumerable<string> ConfirmationStatuses { get; set; } = new List<string> { "Probation", "Confirmed", "Terminated" };
        public IEnumerable<string> Relationships { get; set; } = new List<string> { 
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

        // Custom validation methods
        public bool IsValidAge()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - DateOfBirth.Year;
            
            if (DateOfBirth > today.AddYears(-age))
                age--;
                
            return age >= 18 && age <= 80;
        }

        public bool IsValidJoinDate()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            // Join date shouldn't be more than 7 days in the future
            if (JoinDate > today.AddDays(7))
                return false;
                
            // Join date shouldn't be more than 50 years in the past
            if (JoinDate < today.AddYears(-50))
                return false;
                
            return true;
        }

        public bool IsValidIdentification()
        {
            if (Nationality == "Malaysia" || Nationality == "Malaysian")
            {
                return !string.IsNullOrEmpty(IC) && string.IsNullOrEmpty(PassportNumber);
            }
            else
            {
                return !string.IsNullOrEmpty(PassportNumber) && string.IsNullOrEmpty(IC);
            }
        }

        public bool IsEmergencyContactSameAsEmployee()
        {
            return EmergencyContactPhone == TelephoneNumber;
        }

        public bool AreDocumentsValid()
        {
            if (NewDocuments == null || NewDocumentTypes == null)
                return true; // No documents provided is valid

            if (NewDocuments.Count != NewDocumentTypes.Count)
                return false;

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
            const long maxFileSize = 5 * 1024 * 1024; // 5MB

            foreach (var file in NewDocuments)
            {
                if (file == null) continue;

                // Check file size
                if (file.Length > maxFileSize)
                    return false;

                // Check file extension
                var ext = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(ext))
                    return false;
            }

            return true;
        }
    }
}
