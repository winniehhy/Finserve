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
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [Display(Name = "Password")]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "IC is required")]
        [Display(Name = "IC Number")]
        [MaxLength(20)]
        public string IC { get; set; } = string.Empty;

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

        //[Required(ErrorMessage = "Resignation date is required")]
        [Display(Name = "Resignation Date")]
        [DataType(DataType.Date)]
        public DateOnly? ResignationDate { get; set; }

        [Required(ErrorMessage = "Confirmation status is required")]
        [Display(Name = "Confirmation Status")]
        [MaxLength(20)]
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

        // One Employee can have many Approvals
        //public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

        // One Employee can have many Documents
        public virtual ICollection<EmployeeDocument> EmployeeDocuments { get; set; } = new List<EmployeeDocument>();

        // Link to ApplicationUser (Identity)
        //[Required(ErrorMessage = "Application User is required")]
        //[ForeignKey("ApplicationUserId")]
        public string? ApplicationUserId { get; set; } // FK to AspNetUsers table

        public virtual ApplicationUser ApplicationUser { get; set; }  // Navigation property
    }
}
