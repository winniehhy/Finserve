using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class LeaveTypeModel
    {
        [Key]
        public int LeaveTypeID { get; set; }

        [Required(ErrorMessage = "Type name is required")]
        [Display(Name = "Type Name")]
        [MaxLength(100)]
        public string TypeName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Maximum days allowed is required")]
        [Display(Name = "Maximum Days Allowed")]
        [Range(1, 365, ErrorMessage = "Maximum days must be between 1 and 365")]
        public int MaxDaysAllowed { get; set; }

        // Navigation Property
        public virtual ICollection<LeaveModel>? Leaves { get; set; }
    }
}

