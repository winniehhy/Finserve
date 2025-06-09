using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Claim
    {
        public int Id { get; set; }

        [Required]
        public string ClaimType { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimAmount { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? SupportingDocumentPath { get; set; }

        public string? SupportingDocumentName { get; set; }

        public string Status { get; set; } = "Pending";

        // Foreign Key
        [Required]
        public int UserId { get; set; }

        // Navigation Property
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}