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

        [Required(ErrorMessage = "Default days allocation is required")]
        [Display(Name = "Default Days Per Year")]
        public int DefaultDaysPerYear { get; set; }

        [Display(Name = "Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        // Navigation Property
        public virtual ICollection<LeaveModel>? Leaves { get; set; }
    }
}