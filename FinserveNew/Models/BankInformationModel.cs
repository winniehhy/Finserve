using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class BankInformation
    {
        [Key]
        public int BankID { get; set; }

        [Required(ErrorMessage = "Bank name is required")]
        [Display(Name = "Bank Name")]
        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank account number is required")]
        [Display(Name = "Bank Account Number")]
        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank type is required")]
        [Display(Name = "Bank Type")]
        [MaxLength(50)]
        public string BankType { get; set; } = string.Empty;

        // Navigation Property
        public virtual ICollection<EmployeeModel> Employees { get; set; } = new List<EmployeeModel>();
    }
}