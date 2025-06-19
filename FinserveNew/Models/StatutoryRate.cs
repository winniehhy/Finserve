using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class StatutoryRate
    {
        [Key]
        public int StatutoryRateId { get; set; }
        [Required]
        public string Name { get; set; } // e.g., "EPF", "SOCSO", "EIS"
        [Required]
        public decimal Rate { get; set; } // e.g., 0.11 for 11%
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
} 