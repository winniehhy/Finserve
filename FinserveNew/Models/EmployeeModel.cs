using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace FinserveNew.Models
{
    public class Employee
    {
        [Key]
        public string? EmployeeID { get; set; } = null!;

        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        [MaxLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9_]{3,50}$", ErrorMessage = "Username must be 3-50 characters long and contain only letters, numbers, and underscores")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [Display(Name = "Password")]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        [RegularExpression(@"^[a-zA-Z\s'/-]{1,100}$", ErrorMessage = "First name can only contain letters, spaces, hyphens, slash, and apostrophes")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        [RegularExpression(@"^[a-zA-Z\s'/-]{1,100}$", ErrorMessage = "Last name can only contain letters, spaces, hyphens, slash, and apostrophes")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "IC Number")]
        [MaxLength(12)]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "IC Number must be in 12 digits")]
        public string? IC { get; set; } 

        [Display(Name = "Passport Number")]
        [MaxLength(20)]
        [RegularExpression(@"^[A-Z0-9]{6,20}$", ErrorMessage = "Passport number must be 6-20 characters with letters and numbers only")]
        public string? PassportNumber { get; set; }

        [Required(ErrorMessage = "Nationality is required")]
        [Display(Name = "Nationality")]
        [MaxLength(50)]
        public string Nationality { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email")]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telephone number is required")]
        [Display(Name = "Telephone Number")]
        [MaxLength(20)]
        public string TelephoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Join date is required")]
        [Display(Name = "Join Date")]
        [DataType(DataType.Date)]
        public DateOnly JoinDate { get; set; }

        [Display(Name = "Resignation Date")]
        [DataType(DataType.Date)]
        public DateOnly? ResignationDate { get; set; }

        [Required(ErrorMessage = "Confirmation status is required")]
        [Display(Name = "Confirmation Status")]
        [MaxLength(20)]
        [RegularExpression("^(Pending|Probation|Confirmed|Terminated)$", 
            ErrorMessage = "Confirmation status must be: Pending, Probation, Confirmed, or Terminated")]
        public string ConfirmationStatus { get; set; } = "Pending";

        [Required(ErrorMessage = "Position is required")]
        [Display(Name = "Position")]
        [MaxLength(100)]
        public string Position { get; set; } = string.Empty;

        // Foreign Keys for Bank, Emergency Contact, Role
        [Required(ErrorMessage = "Bank is required")]
        [Display(Name = "Bank")]
        public int BankID { get; set; }

        [Required(ErrorMessage = "Emergency contact is required")]
        [Display(Name = "Emergency Contact")]
        public int EmergencyID { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "Income Tax Number is required")]
        [Display(Name = "Income Tax Number")]
        [MaxLength(15)]
        [RegularExpression(@"^[A-Z0-9]{8,15}$", ErrorMessage = "Income Tax Number must be 8-15 characters with letters and numbers only")]
        public string IncomeTaxNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "EPF Number is required")]
        [Display(Name = "EPF Number")]
        [MaxLength(15)]
        [RegularExpression(@"^[A-Z0-9]{8,15}$", ErrorMessage = "EPF Number must be 8-15 characters with letters and numbers only")]
        public string EPFNumber { get; set; } = string.Empty;

        // Navigation Properties for Foreign Keys
        [ForeignKey("BankID")]
        public virtual BankInformation? BankInformation { get; set; }

        [ForeignKey("EmergencyID")]
        public virtual EmergencyContact? EmergencyContact { get; set; }

        [ForeignKey("RoleID")]
        public virtual JobRole? Role { get; set; }

        // One Employee can have many Claims
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();

        // One Employee can have many Salary records
        public virtual ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();

        // One Employee can have many Documents
        public virtual ICollection<EmployeeDocument> EmployeeDocuments { get; set; } = new List<EmployeeDocument>();

        // Business rule validation methods
        public bool IsValidAge()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - DateOfBirth.Year;
            
            // Adjust if birthday hasn't occurred this year
            if (DateOfBirth > today.AddYears(-age))
                age--;
                
            // Valid working age range (18-80)
            return age >= 18 && age <= 80;
        }

        public bool IsValidJoinDate()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            // Join date shouldn't be in the future (more than 7 days ahead)
            if (JoinDate > today.AddDays(7))
                return false;
                
            // Join date shouldn't be too far in the past (more than 50 years)
            if (JoinDate < today.AddYears(-50))
                return false;
                
            return true;
        }

        public bool IsValidResignationDate()
        {
            if (!ResignationDate.HasValue)
                return true; // Null is valid (employee hasn't resigned)
                
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            // Resignation date must be after join date
            if (ResignationDate.Value <= JoinDate)
                return false;
                
            // Resignation date shouldn't be too far in the future
            if (ResignationDate.Value > today.AddYears(1))
                return false;
                
            return true;
        }

        public bool IsValidIdentification()
        {
            if (Nationality == "Malaysia" || Nationality == "Malaysian")
            {
                // Malaysian citizens must have IC
                return !string.IsNullOrEmpty(IC) && string.IsNullOrEmpty(PassportNumber);
            }
            else
            {
                // Non-Malaysian must have passport
                return !string.IsNullOrEmpty(PassportNumber) && string.IsNullOrEmpty(IC);
            }
        }

        // Full name property for display
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        // Age calculation property
        [NotMapped]
        public int Age
        {
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - DateOfBirth.Year;
                if (DateOfBirth > today.AddYears(-age))
                    age--;
                return age;
            }
        }
    }
}
