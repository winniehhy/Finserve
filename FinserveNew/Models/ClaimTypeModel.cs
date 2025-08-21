using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class ClaimType
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Claim type name is required")]
        [Display(Name = "Claim Type")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        // NEW: Optional maximum amount limit
        [Display(Name = "Maximum Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxAmount { get; set; }

        // NEW: Whether this type requires approval
        [Display(Name = "Requires Approval")]
        public bool RequiresApproval { get; set; } = true;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation property (if you use ClaimDetails)
        public virtual ICollection<ClaimDetails>? ClaimDetails { get; set; }

        // ===== NotMapped HELPER PROPERTIES =====

        [NotMapped]
        public string FormattedMaxAmount
        {
            get
            {
                return MaxAmount.HasValue ? $"RM {MaxAmount:N2}" : "No limit";
            }
        }

        [NotMapped]
        public string StatusText
        {
            get
            {
                return IsActive ? "Active" : "Inactive";
            }
        }

        // Helper method to check if amount exceeds limit
        public bool ExceedsMaxAmount(decimal amount)
        {
            return MaxAmount.HasValue && amount > MaxAmount.Value;
        }
    }
}