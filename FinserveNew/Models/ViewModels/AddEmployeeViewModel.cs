// Models/ViewModels/AddEmployeeViewModel.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinserveNew.Models.ViewModels
{
    public class AddEmployeeViewModel
    {
        // 1. Personal & Contact Details
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "IC Number is required.")]
        [StringLength(20)]
        [Display(Name = "IC Number")]
        public string IC { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nationality is required.")]
        [StringLength(50)]
        public string Nationality { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telephone number is required.")]
        [StringLength(20)]
        [Display(Name = "Telephone Number")]
        public string TelephoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateOnly DateOfBirth { get; set; }

        // 2. Employment & Position Details
        //public string? EmployeeID { get; set; } // Auto-generated 

        [Required(ErrorMessage = "Position is required.")]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [Required(ErrorMessage = "Join Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Join Date")]
        public DateOnly JoinDate { get; set; }

        [Required(ErrorMessage = "Confirmation status is required.")]
        [Display(Name = "Confirmation Status")]
        public string ConfirmationStatus { get; set; } = string.Empty;

        //[DataType(DataType.Date)]
        //[Display(Name = "Resignation Date")]
        //public DateOnly? ResignationDate { get; set; }

        // 3. Compensation & Statutory
        //[Required, Range(0.01, double.MaxValue)]
        //public decimal Salary { get; set; }
        [Required(ErrorMessage = "Bank name is required.")]
        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank type is required.")]
        [StringLength(50)]
        [Display(Name = "Bank Type")]
        public string BankType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank account number is required.")]
        [StringLength(50)]
        [Display(Name = "Bank Account Number")]
        public string BankAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Income tax number is required.")]
        [StringLength(50)]
        public string IncomeTaxNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "EPF number is required.")]
        [StringLength(50)]
        public string EPFNumber { get; set; } = string.Empty;

        // 4. Emergency Contact
        [Required(ErrorMessage = "Emergency contact name is required.")]
        [StringLength(100)]
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
        [Display(Name = "IC Photo")]
        public IFormFile? ICFile { get; set; }
        [Display(Name = "Resume")]
        public IFormFile? ResumeFile { get; set; }
        [Display(Name = "Offer Letter")]
        public IFormFile? OfferLetterFile { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public int RoleID { get; set; }

        public List<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();


        // Dropdowns
        public string[] Nationalities { get; set; } = Array.Empty<string>();
        public string[] BankNames { get; set; } = Array.Empty<string>();
        public string[] BankTypes { get; set; } = Array.Empty<string>();
        public IEnumerable<string> ConfirmationStatuses { get; set; } = new List<string> { "Probation", "Confirmed", "Terminated" };
        public IEnumerable<string> Relationships { get; set; } = new List<string> { "Spouse", "Parent", "Sibling", "Friend" };
    }
}
