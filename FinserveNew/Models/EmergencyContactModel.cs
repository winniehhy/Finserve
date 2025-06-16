using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class EmergencyContact
    {
        [Key]
        public int EmergencyID { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Relationship is required")]
        [Display(Name = "Relationship")]
        [MaxLength(50)]
        public string Relationship { get; set; } = string.Empty;

        // Navigation Property
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}