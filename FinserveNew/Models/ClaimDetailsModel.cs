using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace FinserveNew.Models
{
    public class ClaimDetails
    {
        [Key]
        public int ClaimID { get; set; }

        [Key]
        public int ClaimTypeID { get; set; }

        [Required(ErrorMessage = "Comment is required")]
        [Display(Name = "Comment")]
        [MaxLength(500)]
        public string Comment { get; set; } = string.Empty;

        [Required(ErrorMessage = "Document path is required")]
        [Display(Name = "Document Path")]
        [MaxLength(255)]
        public string DocumentPath { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("ClaimID")]
        public virtual Claim Claim { get; set; } = null!;

        [ForeignKey("ClaimTypeID")]
        public virtual ClaimType ClaimType { get; set; } = null!;
    }
}