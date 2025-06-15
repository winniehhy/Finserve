using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class ClaimType
    {
        [Key]
        public int ClaimTypeID { get; set; }

        [Required(ErrorMessage = "Type name is required")]
        [Display(Name = "Type Name")]
        [MaxLength(100)]
        public string TypeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Max amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Max amount must be greater than 0")]
        [Display(Name = "Max Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxAmount { get; set; }

        // Navigation Property
        public virtual ICollection<ClaimDetails> ClaimDetails { get; set; } = new List<ClaimDetails>();
    }
}